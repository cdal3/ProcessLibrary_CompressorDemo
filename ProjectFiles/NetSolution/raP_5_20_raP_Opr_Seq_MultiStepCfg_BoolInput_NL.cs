#region Using directives
using System;
using System.Linq;
using UAManagedCore;
using FTOptix.UI;
using FTOptix.NetLogic;
using FTOptix.Core;
using FTOptix.CoreBase;
using FTOptix.HMIProject;
using FTOptix.InfluxDBStore;
using FTOptix.InfluxDBStoreLocal;
using FTOptix.SerialPort;
#endregion

public class raP_5_20_raP_Opr_Seq_MultiStepCfg_BoolInput_NL : BaseNetLogic
{
    // -------- Init lifecycle & debounce control --------
    private long _initToken = 0;          // identifies the latest Init() request
    private bool _isStopping = false;     // set when NetLogic is stopping
    private DelayedTask _debounceTask;    // debounce Init() calls
    const int InitDebounceMs = 100;

    // Owner (source/tag-side) variable names
    const string StepStartAbs_Num = "Val_StepStartAbs";
    const string StepsInState_Num = "Val_NumStepsInState";
    const string SetEditStepRel = "Set_EditStepRel";
    const string CurrState = "_CurrState";

    // Loading (UI-side) node/variable (NEW SOURCE)
    const string LoadingNodePath = "VerticalLayout1/ScrollView1/R_Loading";
    const string LoadingVisibleVarName = "_LoadingVisible";

    // CommonHeader and Instance (UI-side) variable names
    const string CurrentStep = "_Step";
    const string CurrentState = "_State";
    const string CurrentIndex = "_Index";

    const string TopMargin = "_TopMargin";

    const int MaxStepsOnPage = 11;

    int itemsNum, AbsMinIndex, AbsMaxIndex, AbsNextIndex;

    public override void Start() { }

    public override void Stop()
    {
        // Stop all pending async logic
        _isStopping = true;
        _initToken++;

        _debounceTask?.Dispose();
        _debounceTask = null;
    }

    [ExportMethod]
    public void Init(NodeId panelLoaderNodeId)
    {
        if (_isStopping)
            return;

        // New Init request -> new token
        long myToken = ++_initToken;
        Log.Info("MultiStepCfg_BoolInput_NL", $"Init token={myToken}");

        // Debounce frequent Init() calls
        _debounceTask?.Dispose();

        _debounceTask = new DelayedTask(() =>
        {
            if (_isStopping || myToken != _initToken)
                return;

            try
            {
                var panelLoader = LogicObject.Context.GetNode(panelLoaderNodeId) as PanelLoader;
                if (panelLoader == null)
                {
                    Log.Warning("MultiStepCfg_BoolInput_NL", "PanelLoader not found");
                    return;
                }

                var panelInstance = panelLoader.Children.FirstOrDefault();
                if (panelInstance == null)
                {
                    Log.Info("MultiStepCfg_BoolInput_NL", "Panel not loaded yet, waiting...");
                    WaitForPanelAndInit(panelLoader, myToken, 0);
                    return;
                }

                DoInit(panelLoader, myToken);
            }
            catch (Exception ex)
            {
                Log.Error("MultiStepCfg_BoolInput_NL", $"Failed to Init: {ex}");
            }

        }, InitDebounceMs, LogicObject);

        _debounceTask.Start();
    }

    private void WaitForPanelAndInit(PanelLoader panelLoader, long token, int retryCount)
    {
        if (_isStopping || token != _initToken)
            return;

        if (retryCount >= 10)
        {
            Log.Warning("MultiStepCfg_BoolInput_NL", "Panel load timeout");
            return;
        }

        var delayedTask = new DelayedTask(() =>
        {
            if (_isStopping || token != _initToken)
                return;

            var panelInstance = panelLoader.Children.FirstOrDefault();
            if (panelInstance != null)
            {
                Log.Info("MultiStepCfg_BoolInput_NL", $"Panel loaded after {(retryCount + 1) * 100}ms");
                DoInit(panelLoader, token);
            }
            else
            {
                WaitForPanelAndInit(panelLoader, token, retryCount + 1);
            }
        }, 100, LogicObject);

        delayedTask.Start();
    }

    private void DoInit(PanelLoader panelLoader, long token)
    {
        IUAVariable loadingVisibleVar = null;

        try
        {
            if (_isStopping || token != _initToken)
                return;

            var panelInstance = panelLoader.Children.FirstOrDefault();
            if (panelInstance == null)
            {
                Log.Warning("MultiStepCfg_BoolInput_NL", "Panel still not loaded");
                return;
            }

            // Resolve loading indicator and keep it ON until PLC confirmed
            var loadingNode = panelInstance.Get(LoadingNodePath);
            loadingVisibleVar = loadingNode?.GetVariable(LoadingVisibleVarName);
            if (loadingVisibleVar != null) loadingVisibleVar.Value = true;

            // Container that holds the dynamic list of step rows (keep old UI until confirmed)
            var parent = panelInstance.Get("VerticalLayout1/ScrollView1/VerticalLayout1");
            if (parent == null)
            {
                Log.Warning("MultiStepCfg_BoolInput_NL", "Parent container not found");
                if (loadingVisibleVar != null) loadingVisibleVar.Value = false;
                return;
            }

            // ----- Read required Owner variables (KEEP) -----
            var vStepStartAbs = Owner.GetVariable(StepStartAbs_Num);
            var vStepsInState = Owner.GetVariable(StepsInState_Num);
            var vSetEditStepRel = Owner.GetVariable(SetEditStepRel);
            var vCurrState = Owner.GetVariable(CurrState);

            if (vStepStartAbs == null || vStepsInState == null || vSetEditStepRel == null || vCurrState == null)
            {
                Log.Warning("MultiStepCfg_BoolInput_NL",
                    $"Owner vars missing: {StepStartAbs_Num}={(vStepStartAbs != null)}, " +
                    $"{StepsInState_Num}={(vStepsInState != null)}, {SetEditStepRel}={(vSetEditStepRel != null)}, " +
                    $"{CurrState}={(vCurrState != null)}");

                if (loadingVisibleVar != null) loadingVisibleVar.Value = false;
                return;
            }

            int StepStartAbsNum = (int)vStepStartAbs.Value;
            int StepsInStateNum = (int)vStepsInState.Value;
            int stepInputNum = (int)vSetEditStepRel.Value;
            int CurrStateNum = (int)vCurrState.Value;

            AbsMinIndex = StepStartAbsNum;
            AbsMaxIndex = StepsInStateNum + AbsMinIndex - 1;

            AbsNextIndex = stepInputNum + AbsMinIndex - 1;

            if (AbsNextIndex + MaxStepsOnPage - 1 > AbsMaxIndex)
                AbsNextIndex = AbsMaxIndex - MaxStepsOnPage + 1;

            if (AbsNextIndex < AbsMinIndex)
                AbsNextIndex = AbsMinIndex;

            if (AbsMaxIndex - AbsNextIndex >= MaxStepsOnPage)
                itemsNum = MaxStepsOnPage;
            else
                itemsNum = AbsMaxIndex - AbsNextIndex + 1;

            // ----- Resolve CommonHeader node (UI side) -----
            var CommonHeaderNode = panelInstance.Get("VerticalLayout1/grp_CommonHeader");
            if (CommonHeaderNode == null)
            {
                Log.Warning("MultiStepCfg_BoolInput_NL", "Node 'VerticalLayout1/grp_CommonHeader' not found");
                if (loadingVisibleVar != null) loadingVisibleVar.Value = false;
                return;
            }

            var CurrStepIndexVar = CommonHeaderNode.GetVariable(CurrentStep);
            var HeaderSetEditStepRelVar = CommonHeaderNode.GetVariable(SetEditStepRel);
            var CurrStateVar = CommonHeaderNode.GetVariable(CurrentState);

            if (CurrStepIndexVar == null || HeaderSetEditStepRelVar == null || CurrStateVar == null)
            {
                Log.Warning("MultiStepCfg_BoolInput_NL",
                    $"CommonHeader vars missing: {CurrentStep}={(CurrStepIndexVar != null)}, " +
                    $"{SetEditStepRel}={(HeaderSetEditStepRelVar != null)}, " +
                    $"{CurrentState}={(CurrStateVar != null)}");

                if (loadingVisibleVar != null) loadingVisibleVar.Value = false;
                return;
            }

            // Compute write-back value (must be confirmed in PLC)
            int computedRelStep = AbsNextIndex - AbsMinIndex + 1;
            if (computedRelStep < 1) computedRelStep = 1;
            if (computedRelStep > StepsInStateNum) computedRelStep = StepsInStateNum;

            ForceWriteOwnerSetEditStepRel(
                ownerSetVar: vSetEditStepRel,
                expectedValue: computedRelStep,
                token: token,
                onDone: (confirmed) =>
                {
                    if (_isStopping || token != _initToken)
                        return;

                    if (!confirmed)
                    {
                        Log.Warning("MultiStepCfg_BoolInput_NL",
                            $"Set_EditStepRel did not confirm after retries. expected={computedRelStep}. Releasing loading (fail-safe).");

                        if (loadingVisibleVar != null) loadingVisibleVar.Value = false;
                        return; // IMPORTANT: do not update UI on unconfirmed PLC write
                    }

                    // PLC confirmed -> update UI to match controller state
                    CurrStepIndexVar.Value = AbsNextIndex;
                    CurrStateVar.Value = CurrStateNum;
                    HeaderSetEditStepRelVar.Value = computedRelStep;

                    ClearOldItems(parent);
                    CreateInstance(parent, AbsNextIndex, itemsNum);

                    if (loadingVisibleVar != null) loadingVisibleVar.Value = false;
                }
            );
        }
        catch (Exception ex)
        {
            Log.Error("MultiStepCfg_BoolInput_NL", $"DoInit failed: {ex}");
            if (loadingVisibleVar != null) loadingVisibleVar.Value = false;
        }
    }

    // Strict PLC confirmation: ALWAYS RemoteRead to confirm, local value is not confirmation
    private void ForceWriteOwnerSetEditStepRel(
        IUAVariable ownerSetVar,
        int expectedValue,
        long token,
        Action<bool> onDone,
        int retry = 0)
    {
        if (_isStopping || token != _initToken)
            return;

        const int MaxRetry = 8;
        const int DelayMs = 100;

        var task = new DelayedTask(() =>
        {
            if (_isStopping || token != _initToken)
                return;

            bool confirmed = false;

            try
            {
                int currentValue = (int)ownerSetVar.Value;
                if (currentValue != expectedValue)
                    ownerSetVar.RemoteWrite(expectedValue);

                var remoteRead = ownerSetVar.RemoteRead();
                int remoteValue = (int)remoteRead.Value;

                if (remoteValue == expectedValue)
                    confirmed = true;
            }
            catch (Exception ex)
            {
                Log.Warning("MultiStepCfg_BoolInput_NL", $"Remote sync check failed: {ex.Message}");
            }

            if (confirmed)
            {
                onDone?.Invoke(true);
                return;
            }

            if (retry >= MaxRetry)
            {
                onDone?.Invoke(false);
                return;
            }

            ForceWriteOwnerSetEditStepRel(ownerSetVar, expectedValue, token, onDone, retry + 1);

        }, DelayMs, LogicObject);

        task.Start();
    }

    private void ClearOldItems(IUANode parent)
    {
        var children = parent.Children.ToList();
        foreach (var item in children)
            parent.Remove(item);
    }

    private void CreateInstance(IUANode parent, int startStep, int stepsOnPage)
    {
        for (int j = 0; j < stepsOnPage; j++)
        {
            var instance = InformationModel.MakeObject<raP_5_20_raP_Opr_Seq_MultiStepCfg_BoolInput>($"MultiStepCfg_BoolInput{j}");
            instance.GetVariable(CurrentStep).Value = startStep + j;
            instance.GetVariable(CurrentIndex).Value = j;
            instance.GetVariable(TopMargin).Value = 0;
            if (j == 4 || j == 8)
            {
                instance.GetVariable(TopMargin).Value = 8;
            }
            parent.Add(instance);
        }
    }
}
