#region Using directives
using System;
using UAManagedCore;
using OpcUa = UAManagedCore.OpcUa;
using FTOptix.HMIProject;
using FTOptix.RAEtherNetIP;
using FTOptix.NativeUI;
using FTOptix.UI;
using FTOptix.CoreBase;
using FTOptix.OPCUAServer;
using FTOptix.Retentivity;
using FTOptix.CommunicationDriver;
using FTOptix.NetLogic;
using FTOptix.AuditSigning;
using FTOptix.EventLogger;
using FTOptix.Store;
using FTOptix.Core;
using System.Collections.Generic;
using System.Linq;
using FTOptix.Alarm;
using System.Runtime.CompilerServices;
using FTOptix.SQLiteStore;
using FTOptix.System;
using FTOptix.DataLogger;
using FTOptix.InfluxDBStore;
using FTOptix.InfluxDBStoreLocal;
using FTOptix.SerialPort;
#endregion

public class raP_5_20_PINTLK_NL : BaseNetLogic
{
    private LongRunningTask loadTask;

    // Keep a reference to launchObj so Stop() can clean up Ref_Tag variables
    private IUAObject launchObjRef;

    // Track which bank indices were created so Stop() can remove exactly those
    private List<int> createdBankIndices = new List<int>();

    public override void Start()
    {
        // Use LongRunningTask so the UI renders first, then data loads in background
        loadTask = new LongRunningTask(CreateBanks, LogicObject);
        loadTask.Start();
    }

    public override void Stop()
    {
        // Cancel the long running task if still active
        loadTask?.Dispose();
        loadTask = null;

        // Clean up Ref_Tag variables from launchObj (it persists across dialog opens)
        if (launchObjRef != null)
        {
            foreach (int idx in createdBankIndices)
            {
                try
                {
                    var refVar = launchObjRef.Find($"Ref_Tag_{idx}");
                    if (refVar != null)
                        launchObjRef.Remove(refVar);
                }
                catch { }
            }
            launchObjRef = null;
            createdBankIndices.Clear();
        }
    }

    public void CreateBanks()
    {
        string BasePath = "";
        IUAObject launchObj = null;
        int valBankMap = 0;

        try
        {
            // Step 1: Get the Alias Node to determine the Launch Object
            var aliasNode = Owner.GetAlias("raSDK1_DialogBox");
            launchObj = InformationModel.GetObject(aliasNode.NodeId);

            // Step 2: Get the Ref_Tag from the Alias
            var refTag = InformationModel.Get(launchObj.GetVariable("Ref_Tag").Value);
            string topContainer = "";
            string refTagBrowsePath = GetOptixPathByNode(refTag, topContainer);
            Log.Info("Ref_Tag browse path:  " + refTagBrowsePath);

            // Step 3: Get base path — strip trailing bank index "0"
            string tag_0_BrowsePath = "CommDrivers" + refTagBrowsePath.Split("CommDrivers")[1];
            BasePath = tag_0_BrowsePath.Substring(0, tag_0_BrowsePath.Length - 1);

            // Save reference for cleanup in Stop()
            launchObjRef = launchObj;
            createdBankIndices.Clear();

            // Step 4: Create Intlk_0 and read bank map (contains the single RemoteRead)
            valBankMap = this.AddToLaunchObject(launchObj, BasePath, 0);
            createdBankIndices.Add(0);

            // Step 5: Add the rest of the banks
            for (int i = 1; i < 8; i++)
            {
                if ((valBankMap & (1 << i)) != 0)
                {
                    this.AddToLaunchObject(launchObj, BasePath, i);
                    createdBankIndices.Add(i);
                }
            }

            // Force Home tab refresh — destroys stale Home content and re-creates with all Ref_Tag data ready
            var navPanel = Owner.Find("NavigationPanel");
            if (navPanel != null)
                ((NavigationPanel)navPanel).ChangePanelByTabName("Home");
        }
        catch
        {
            if (launchObj == null)
                Log.Error("Interlock Dialog Box startup", "Error getting Alias Node Id");
            else if (BasePath == "")
                Log.Error("Interlock Dialog Box startup", "CommDrivers folder not found");
            else if (valBankMap == 0)
                Log.Error("Interlock Dialog Box startup", "Error reading Val_BankMap");
            else
                Log.Error("Interlock Dialog Box startup", "Unknown error occured");
        }
    }

    // Creates a Ref_Tag_N variable on launchObj pointing to the Logix bank tag at basePath + idx
    public int AddToLaunchObject(IUAObject launchObj, string basePath, int idx)
    {
        int valBankMap = 0;
        string varName = $"Ref_Tag_{idx}";

        IUANode logixBankTag = Project.Current.Get(basePath + idx.ToString());

        // Remove existing variable if present (dialog reopened — launchObj persists across opens)
        var existing = launchObj.Find(varName);
        if (existing != null)
            launchObj.Remove(existing);

        // Make new variable for each bank
        IUAVariable newBankVar = InformationModel.MakeVariable(varName, OpcUa.DataTypes.NodeId);
        newBankVar.Value = logixBankTag.NodeId;
        launchObj.Add(newBankVar);

        // Return the bank map for the first instance only (single RemoteRead — the slowest call)
        if (idx == 0)
            valBankMap = (int)logixBankTag.Children.GetVariable("Val_BankMap").RemoteRead().Value;

        return valBankMap;
    }

    public string GetOptixPathByNode(IUANode inputNode, string topContainer)
    {
        List<string> pathToVar = new List<string>();
        FindBrowsePath(inputNode);
        if (pathToVar.Count > 0)
        {
            var launchAliasPath = ConstructBrowsePath();
            return launchAliasPath;
        }
        else
        {
            return null;
        }

        string ConstructBrowsePath()
        {
            string outStr = topContainer;
            for (long i = (pathToVar.LongCount() - 1); i >= 0; i--)
            {
                outStr = outStr + "/" + pathToVar[(int)i];
            }
            pathToVar = new List<string>();
            return outStr;
        }

        void FindBrowsePath(IUANode inputNode)
        {
            if (inputNode.Owner != null)
            {
                if (inputNode.BrowseName == topContainer)
                {
                    return;
                }
                pathToVar.Add(inputNode.BrowseName);
                FindBrowsePath(inputNode.Owner);
            }
        }
    }
}
