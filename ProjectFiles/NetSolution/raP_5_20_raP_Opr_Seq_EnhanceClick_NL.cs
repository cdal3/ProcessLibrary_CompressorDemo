#region Using directives
using System;
using UAManagedCore;
using FTOptix.NetLogic;
using FTOptix.InfluxDBStore;
using FTOptix.InfluxDBStoreLocal;
using FTOptix.SerialPort;
#endregion

public class raP_5_20_raP_Opr_Seq_EnhanceClick_NL : BaseNetLogic
{
    private DelayedTask reEnableTask;

    private const string VAR_ENABLE = "_Enable";
    private const string VAR_SET_VARIABLE = "Set_Variable";
    private const string VAR_VARIABLE = "Value";

    private const int DISABLE_MS = 500;

    public override void Start()
    {
    }

    public override void Stop()
    {
        DisposeTask();
    }

    [ExportMethod]
    public void ClickButton()
    {
        var button = Owner;
        if (button == null)
            return;

        var enableVar = button.GetVariable(VAR_ENABLE);
        if (enableVar == null)
            return;

        enableVar.Value = false;

        DisposeTask();

        reEnableTask = new DelayedTask(() =>
        {
            try
            {
                var btn = Owner;
                if (btn == null)
                    return;

                var v = btn.GetVariable(VAR_ENABLE);
                if (v != null)
                    v.Value = true;

                var setVar = btn.GetVariable(VAR_SET_VARIABLE);
                var objVar = btn.GetVariable(VAR_VARIABLE);

                if (setVar == null || objVar == null)
                    return;

                var valueToSet = objVar.Value;
                setVar.RemoteWrite(valueToSet);
            }
            finally
            {
                // Ensure the task is released after execution
                DisposeTask();
            }

        }, DISABLE_MS, LogicObject);

        reEnableTask.Start();
    }

    private void DisposeTask()
    {
        try
        {
            reEnableTask?.Dispose();
        }
        catch (Exception ex)
        {
            Log.Warning(LogicObject.BrowseName, $"DisposeTask: reEnableTask.Dispose() failed: {ex.Message}");
        }
        finally
        {
            reEnableTask = null;
        }
    }
}
