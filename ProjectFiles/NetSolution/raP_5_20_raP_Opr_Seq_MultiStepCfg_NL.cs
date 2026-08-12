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

public class raP_5_20_raP_Opr_Seq_MultiStepCfg_NL : BaseNetLogic
{
    // -------- Init lifecycle & debounce control --------
    private long _initToken = 0;          // identifies the latest Init() request
    private bool _isStopping = false;     // set when NetLogic is stopping
    private DelayedTask _debounceTask;    // debounce Init() calls
    const int InitDebounceMs = 100;

    // -------- Owner (tag-side) variable names -----------
    const string StepStartAbs_Num = "Val_StepStartAbs";
    const string StepsInState_Num = "Val_NumStepsInState";
    const string SetEditStepRel = "Set_EditStepRel";
    const string CurrState = "_CurrState";

    // -------- Loading (UI-side) --------
    const string LoadingNodePath = "VerticalLayout1/ScrollView1/R_Loading";
    const string LoadingVisibleVarName = "_LoadingVisible";

    // -------- CommonHeader and Instance (UI-side) --------
    const string CurrentStep = "_Step";
    const string CurrentState = "_State";
    const string CurrentIndex = "_Index";

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
        Log.Info("MultiStepCfg_NL", $"Init token={myToken}");

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
                    Log.Warning("MultiStepCfg_NL", "PanelLoader not found");
                    return;
                }

                var panelInstance = panelLoader.Children.FirstOrDefault();
                if (panelInstance == null)
                {
                    WaitForPanelAndInit(panelLoader, myToken, 0);
                    return;
                }

                DoInit(panelLoader, myToken);
            }
            catch (Exception ex)
            {
                Log.Error("MultiStepCfg_NL", $"Init failed: {ex}");
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
            Log.Warning("MultiStepCfg_NL", "Panel load timeout");
            return;
        }

        var task = new DelayedTask(() =>
        {
            if (_isStopping || token != _initToken)
                return;

            var panelInstance = panelLoader.Children.FirstOrDefault();
            if (panelInstance != null)
            {
                DoInit(panelLoader, token);
            }
            else
            {
                WaitForPanelAndInit(panelLoader, token, retryCount + 1);
            }

        }, 100, LogicObject);

        task.Start();
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
                return;

            // Show loading (block UI until PLC confirmed)
            var loadingNode = panelInstance.Get(LoadingNodePath);
            loadingVisibleVar = loadingNode?.GetVariable(LoadingVisibleVarName);
            if (loadingVisibleVar != null) loadingVisibleVar.Value = true;

            // Container for step rows (keep current UI until confirmed)
            var parent = panelInstance.Get("VerticalLayout1/ScrollView1/VerticalLayout1");
            if (parent == null)
            {
                if (loadingVisibleVar != null) loadingVisibleVar.Value = false;
                return;
            }

            // Read owner variables
            var vStepStartAbs = Owner.GetVariable(StepStartAbs_Num);
            var vStepsInState = Owner.GetVariable(StepsInState_Num);
            var vSetEditStepRel = Owner.GetVariable(SetEditStepRel);
            var vCurrState = Owner.GetVariable(CurrState);

            if (vStepStartAbs == null || vStepsInState == null ||
                vSetEditStepRel == null || vCurrState == null)
            {
                // Log missing owner variables to diagnose "variable does not exist" issues
                string missing =
                    (vStepStartAbs == null ? $"{StepStartAbs_Num} " : "") +
                    (vStepsInState == null ? $"{StepsInState_Num} " : "") +
                    (vSetEditStepRel == null ? $"{SetEditStepRel} " : "") +
                    (vCurrState == null ? $"{CurrState} " : "");

                Log.Warning("MultiStepCfg_NL",
                    $"Owner variables not found: {missing.Trim()}. token={token}, owner={Owner?.BrowseName}");
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

            itemsNum = (AbsMaxIndex - AbsNextIndex >= MaxStepsOnPage)
                ? MaxStepsOnPage
                : AbsMaxIndex - AbsNextIndex + 1;

            // CommonHeader
            var header = panelInstance.Get("VerticalLayout1/grp_CommonHeader");
            if (header == null)
            {
                if (loadingVisibleVar != null) loadingVisibleVar.Value = false;
                return;
            }

            var currStepVar = header.GetVariable(CurrentStep);
            var headerSetVar = header.GetVariable(SetEditStepRel);
            var currStateVar = header.GetVariable(CurrentState);

            if (currStepVar == null || headerSetVar == null || currStateVar == null)
            {
                // Log missing owner variables to diagnose "variable does not exist" issues
                string missing =
                    (currStepVar == null ? $"{CurrentStep} " : "") +
                    (headerSetVar == null ? $"{SetEditStepRel} " : "") +
                    (currStateVar == null ? $"{CurrentState} " : "");

                Log.Warning("MultiStepCfg_NL",
                    $"Owner variables not found: {missing.Trim()}. token={token}, owner={Owner?.BrowseName}");
                if (loadingVisibleVar != null) loadingVisibleVar.Value = false;
                return;
            }

            // Compute relative step index (target value to be confirmed in PLC)
            int computedRelStep = AbsNextIndex - AbsMinIndex + 1;
            computedRelStep = Math.Max(1, Math.Min(computedRelStep, StepsInStateNum));

            // Strict mode:
            // - keep loading ON until PLC confirmed
            // - update UI (header + list) ONLY when confirmed
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
                        Log.Warning("MultiStepCfg_NL",
                            $"Set_EditStepRel did not confirm after retries. expected={computedRelStep}. Releasing loading (fail-safe).");

                        // Fail-safe: release loading to avoid permanent stuck
                        if (loadingVisibleVar != null) loadingVisibleVar.Value = false;
                        return; // IMPORTANT: do not update UI on unconfirmed PLC write
                    }

                    // PLC confirmed -> now update UI to match controller state
                    currStepVar.Value = AbsNextIndex;
                    currStateVar.Value = CurrStateNum;
                    headerSetVar.Value = computedRelStep;

                    ClearOldItems(parent);
                    CreateInstance(parent, AbsNextIndex, itemsNum);

                    if (loadingVisibleVar != null)
                        loadingVisibleVar.Value = false;
                });
        }
        catch (Exception ex)
        {
            Log.Error("MultiStepCfg_NL", $"DoInit failed. token={token}, loadingPath={LoadingNodePath}, loadingVar={LoadingVisibleVarName}, ex={ex}");
            if (loadingVisibleVar != null)
                loadingVisibleVar.Value = false;
        }
    }

    // Strict PLC confirmation:
    // - optionally skip RemoteWrite if already same, but ALWAYS RemoteRead to confirm
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

                // Write only when needed (reduce traffic),
                // but DO NOT treat local value as confirmation.
                if (currentValue != expectedValue)
                    ownerSetVar.RemoteWrite(expectedValue);

                // Confirmation must come from RemoteRead (PLC side)
                var remoteRead = ownerSetVar.RemoteRead();
                int remoteValue = (int)remoteRead.Value;

                if (remoteValue == expectedValue)
                    confirmed = true;
            }
            catch (Exception ex)
            {
                Log.Warning("MultiStepCfg_NL", $"Remote sync check failed: {ex.Message}");
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

            ForceWriteOwnerSetEditStepRel(
                ownerSetVar,
                expectedValue,
                token,
                onDone,
                retry + 1);

        }, DelayMs, LogicObject);

        task.Start();
    }

    private void ClearOldItems(IUANode parent)
    {
        foreach (var c in parent.Children.ToList())
            parent.Remove(c);
    }

    private void CreateInstance(IUANode parent, int startStep, int stepsOnPage)
    {
        for (int j = 0; j < stepsOnPage; j++)
        {
            var inst = InformationModel.MakeObject<raP_5_20_raP_Opr_Seq_MultiStepCfgHome>(
                $"MultiStepCfgHome{j}");

            inst.GetVariable(CurrentStep).Value = startStep + j;
            inst.GetVariable(CurrentIndex).Value = j;
            parent.Add(inst);
        }
    }
}
