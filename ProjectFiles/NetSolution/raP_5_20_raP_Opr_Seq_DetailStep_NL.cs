#region Using directives
using System;
using System.Linq;
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
using System.Threading.Tasks;
using FTOptix.InfluxDBStore;
using FTOptix.InfluxDBStoreLocal;
using FTOptix.SerialPort;
#endregion

public class raP_5_20_raP_Opr_Seq_DetailStep_NL : BaseNetLogic
{

    const string Step_Num = "StepNum";
    const string AbsStep_Num = "AbsStepNum";
    const string StepsInState_Num = "NumStepsInCurrState";
    const string StepIndex = "_StepIndex";
//    const string isFirstStep = "_IsTheFirstStep";
    const int MaxStepsOnPage = 9;
    int itemsNum, startAbsStepNum;

    public override void Start()
    {
    }

    public override void Stop()
    { }

    [ExportMethod]
    public void Init(NodeId panelLoaderNodeId)
    {
        try
        {
            var panelLoader = LogicObject.Context.GetNode(panelLoaderNodeId) as PanelLoader;
            if (panelLoader == null)
            {
                Log.Warning("DetailStep_NL", "PanelLoader not found");
                return;
            }

            var panelInstance = panelLoader.Children.FirstOrDefault();

            if (panelInstance == null)
            {
                Log.Info("DetailStep_NL", "Panel not loaded yet, waiting...");
                WaitForPanelAndInit(panelLoader, 0);
                return;
            }

 
            DoInit(panelLoader);
        }
        catch (Exception ex)
        {
            Log.Error("Detail Step Initialization", $"Failed to Init: {ex.Message}");
        }
    }

    private void WaitForPanelAndInit(PanelLoader panelLoader, int retryCount)
    {

        if (retryCount >= 10)
        {
            Log.Warning("DetailStep_NL", "Panel load timeout");
            return;
        }

        var delayedTask = new DelayedTask(() =>
        {
            var panelInstance = panelLoader.Children.FirstOrDefault();
            if (panelInstance != null)
            {
                Log.Info("DetailStep_NL", $"Panel loaded after {(retryCount + 1) * 100}ms");
                DoInit(panelLoader);
            }
            else
            {
                WaitForPanelAndInit(panelLoader, retryCount + 1);
            }
        }, 100, LogicObject);

        delayedTask.Start();
    }

    private void DoInit(PanelLoader panelLoader)
    {
        try
        {
            var panelInstance = panelLoader.Children.FirstOrDefault();
            if (panelInstance == null)
            {
                Log.Warning("DetailStep_NL", "Panel still not loaded");
                return;
            }

            var parent = panelInstance.Get("VerticalLayout1/grp_Inputs/VerticalLayout1");
            if (parent == null)
            {
                Log.Warning("DetailStep_NL", "Parent container not found");
                return;
            }

            ClearOldItems(parent);

            var parent2 = panelInstance.Get("VerticalLayout1/grp_Inputs/VerticalLayout2");
            if (parent2 == null)
            {
                Log.Warning("DetailStep_NL", "Parent container not found");
                return;
            }

            ClearOldItems(parent2);


            int stepNum = Owner.GetVariable(Step_Num).Value;
            int absStepNum = Owner.GetVariable(AbsStep_Num).Value;
            int stepsInState = Owner.GetVariable(StepsInState_Num).Value;

            var msdStsEStateNode = panelInstance.Get("VerticalLayout1/HorizontalLayout1/msd_Sts_eState");
            if (msdStsEStateNode != null)
            {
                var msdStepIndexVar = msdStsEStateNode.GetVariable(StepIndex);
                if (msdStepIndexVar != null)
                {
                    msdStepIndexVar.Value = absStepNum;
                }
                else
                {
                    Log.Warning("DetailStep_NL", $"Variable '{StepIndex}' not found on msd_Sts_eState");
                }
            }
            else
            {
                Log.Warning("DetailStep_NL", "Node 'VerticalLayout1/HorizontalLayout1/msd_Sts_eState' not found");
            }

            if (stepsInState <= MaxStepsOnPage)
            {
                startAbsStepNum = absStepNum - stepNum + 1;
                itemsNum = stepsInState;
            }
            else
            {
                if (stepNum - 2 > 1)
                {
                    startAbsStepNum = absStepNum - 2;
                    if (stepsInState - stepNum + 3 > MaxStepsOnPage)
                    {
                        itemsNum = MaxStepsOnPage;
                    }
                    else
                    {
                        itemsNum = stepsInState - stepNum + 3;
                    }
                }
                else
                {
                    startAbsStepNum = absStepNum - stepNum + 1;
                    itemsNum = MaxStepsOnPage;
                }
            }

            CreateInstance(parent, startAbsStepNum, itemsNum);
            CreateInstance2(parent2, startAbsStepNum);

            Log.Info("DetailStep_NL", $"Created {itemsNum} instances starting from step {startAbsStepNum}");
        }
        catch (Exception ex)
        {
            Log.Error("DetailStep_NL", $"DoInit failed: {ex.Message}");
        }
    }

    private void ClearOldItems(IUANode parent)
    {
        var children = parent.Children.ToList();
        foreach (var item in children)
        {
            parent.Remove(item);
        }
    }
    private void CreateInstance(IUANode parent, int startStep, int stepsOnPage)
    {

        for (int j = 0; j < stepsOnPage; j++)
        {
            var instance = InformationModel.MakeObject<raP_5_20_raP_Opr_Seq_grp_InputStepAndIndicator>($"InputStepAndIndicator{j}");            
            instance.GetVariable(StepIndex).Value = startStep + j;
            parent.Add(instance);
        }

    }

    //To create InputStepAndIndicatorActive instance in Detail-Step By Kate
    private void CreateInstance2(IUANode parent, int startStep)
    {

        var instance = InformationModel.MakeObject<raP_5_20_raP_Opr_Seq_grp_InputStepAndIndicatorActive>($"InputStepAndIndicatorActive");
        instance.GetVariable(StepIndex).Value = startStep;
        parent.Add(instance);

    }

}
