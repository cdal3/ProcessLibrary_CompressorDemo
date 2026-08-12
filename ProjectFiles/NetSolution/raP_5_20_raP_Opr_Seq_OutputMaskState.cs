#region Using directives
using System;
using UAManagedCore;
using OpcUa = UAManagedCore.OpcUa;
using FTOptix.UI;
using FTOptix.DataLogger;
using FTOptix.HMIProject;
using FTOptix.CoreBase;
using FTOptix.System;
using FTOptix.NativeUI;
using FTOptix.SQLiteStore;
using FTOptix.Store;
using FTOptix.OPCUAServer;
using FTOptix.RAEtherNetIP;
using FTOptix.NetLogic;
using FTOptix.Retentivity;
using FTOptix.CommunicationDriver;
using FTOptix.AuditSigning;
using FTOptix.EventLogger;
using FTOptix.Core;
using System.Collections.Generic;
using System.Linq;
using FTOptix.InfluxDBStore;
using FTOptix.InfluxDBStoreLocal;
using FTOptix.SerialPort;
#endregion


public class raP_5_20_raP_Opr_Seq_OutputMaskState : BaseNetLogic
{

    private const string STEPS_SUFFIX = "_Steps";
    private const string INPUTS_NODE = "Outputs";
    private const string CFG_INPUTMASK = "Cfg_OutputMask";
    private const string CFG_INPUTSTATE = "Cfg_OutputState";
    private IUAVariable _refTag;           // Main device tag reference
    private IUAVariable _stepIndex;        // Step index (#5)
    private IUAVariable _bankIndex;        // Input Bank number (#101)
    private IUAVariable _bitIndex;         // Input bit number (#102)

    public override void Start()
    {
        // Get configuration variables from Button
        _refTag = Owner.GetVariable("Ref_Tag");
        _stepIndex = Owner.GetVariable("StepIndex");
        _bankIndex = Owner.GetVariable("BankIndex");
        _bitIndex = Owner.GetVariable("BitIndex");

        // Validate required variables exist
        if (_refTag == null || _stepIndex == null || _bankIndex == null || _bitIndex == null)
        {
            Log.Warning("ToggleInputQualify", "Missing required variables on Button. Please create: Ref_Tag, StepIndex, BankIndex, BitIndex");
        }
    }

    public override void Stop()
    {
        _refTag = null;
        _stepIndex = null;
        _bankIndex = null;
        _bitIndex = null;
    }

    [ExportMethod]
    public void ToggleInput()
    {
        try
        {
            // Validate variables
            if (_refTag == null || _stepIndex == null || _bankIndex == null || _bitIndex == null)
            {
                Log.Error("ToggleInputQualify", "Required variables not found on Button");
                return;
            }

            // Get parameter values
            var refTagNodeId = _refTag.Value;
            if (refTagNodeId == null || refTagNodeId.Value == null)
            {
                Log.Error("ToggleInputQualify", "Ref_Tag is not set");
                return;
            }

            int stepIdx = Convert.ToInt32(_stepIndex.Value.Value);
            int bankIdx = Convert.ToInt32(_bankIndex.Value.Value);
            int bitIdx = Convert.ToInt32(_bitIndex.Value.Value);

            // Get base tag node
            var baseTag = InformationModel.Get(refTagNodeId);
            if (baseTag == null)
            {
                Log.Error("ToggleInputQualify", "Cannot find base tag from Ref_Tag");
                return;
            }

            // Build full path string using GetOptixPathByNode pattern
            string fullPath = GetOptixPathByNode(baseTag);
            // Extract path starting from CommDrivers (like project pattern)
            string basePath = "CommDrivers" + fullPath.Split("CommDrivers")[1];

            // PlantPAx 5000 structure: {BasePath}_Steps/n/Inputs/m
            string inputsPath = $"{basePath}{STEPS_SUFFIX}/{stepIdx}/{INPUTS_NODE}/{bankIdx}";
            var inputsNode = Project.Current.Get(inputsPath);
            if (inputsNode == null)
            {
                Log.Error("ToggleInputQualify", $"Inputs node not found: {inputsPath}");
                return;
            }

            // Get Cfg_InputMask and Cfg_InputState variables
            var maskVar = inputsNode.Children.GetVariable(CFG_INPUTMASK);
            var stateVar = inputsNode.Children.GetVariable(CFG_INPUTSTATE);

            if (maskVar == null || stateVar == null)
            {
                Log.Error("ToggleInputQualify", "Cfg_InputMask or Cfg_InputState not found");
                return;
            }

            // Read current DINT values
            int maskDint = Convert.ToInt32(maskVar.RemoteRead().Value);
            int stateDint = Convert.ToInt32(stateVar.RemoteRead().Value);

            // Extract corresponding bit values
            bool maskBit = ((maskDint >> bitIdx) & 1) == 1;
            bool stateBit = ((stateDint >> bitIdx) & 1) == 1;

            Log.Info("ToggleInputQualify", $"Current values - Mask[{bitIdx}]={maskBit}, State[{bitIdx}]={stateBit}");

            if (maskBit != stateBit)
            {
                // Case 1: Mask ≠ State - Sync State first, then enable Mask
                Log.Info("ToggleInputQualify", "Mask != State: Syncing State to Mask, then setting Mask=1");

                // Step A: Set State bit = current Mask bit
                int newStateDint = maskBit
                    ? (stateDint | (1 << bitIdx))      // Set bit to 1
                    : (stateDint & ~(1 << bitIdx));    // Set bit to 0
                stateVar.RemoteWrite(newStateDint);
                Log.Info("ToggleInputQualify", $"State[{bitIdx}] set to {maskBit}");

                // Step B: Set Mask bit = 1 (enabled)
                int newMaskDint = maskDint | (1 << bitIdx);
                maskVar.RemoteWrite(newMaskDint);
                Log.Info("ToggleInputQualify", $"Mask[{bitIdx}] set to 1 (enabled)");
            }
            else
            {
                // Case 2: Mask = State - Normal Toggle Mask
                Log.Info("ToggleInputQualify", "Mask == State: Toggling Mask bit");

                int newMaskDint = maskBit
                    ? (maskDint & ~(1 << bitIdx))   // Toggle: 1 → 0
                    : (maskDint | (1 << bitIdx));   // Toggle: 0 → 1
                maskVar.RemoteWrite(newMaskDint);
                Log.Info("ToggleInputQualify", $"Mask[{bitIdx}] toggled to {!maskBit}");
            }

            Log.Info("ToggleInputQualify", "ToggleInputQualify completed successfully");
        }
        catch (Exception ex)
        {
            Log.Error("ToggleInputQualify", $"ToggleInputQualify error: {ex.Message}");
        }
    }
    private string GetOptixPathByNode(IUANode inputNode)
    {
        var pathToVar = new System.Collections.Generic.List<string>();

        void FindBrowsePath(IUANode node)
        {
            if (node?.Owner != null)
            {
                pathToVar.Add(node.BrowseName);
                FindBrowsePath(node.Owner);
            }
        }

        FindBrowsePath(inputNode);

        if (pathToVar.Count == 0)
            return inputNode.BrowseName;

        // Build path from root to node
        var sb = new System.Text.StringBuilder();
        for (int i = pathToVar.Count - 1; i >= 0; i--)
        {
            if (sb.Length > 0)
                sb.Append("/");
            sb.Append(pathToVar[i]);
        }
        return sb.ToString();
    }


}
