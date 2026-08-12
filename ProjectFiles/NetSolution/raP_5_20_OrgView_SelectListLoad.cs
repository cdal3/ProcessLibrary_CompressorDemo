#region Using directives
using System;
using UAManagedCore;
using OpcUa = UAManagedCore.OpcUa;
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
using FTOptix.UI;
using FTOptix.Core;
using FTOptix.InfluxDBStore;
using FTOptix.InfluxDBStoreLocal;
using FTOptix.SerialPort;
#endregion

public class raP_5_20_OrgView_SelectListLoad : BaseNetLogic
{
    private DelayedTask myDelayedTask;

    const string NUM_BUS_SIZE = "Cfg_BusSize";
    const string NUM_BUS_INDEX = "_BusIndex";
    const string IS_LOADING_VAR_NAME = "_IsLoading";


    public override void Start()
    {
        // Insert code to be executed when the user-defined logic is started
        this.Initialize();
    }

    public override void Stop()
    {
        // Insert code to be executed when the user-defined logic is stopped
    }
    private void Initialize()
    {
        try
        {
            myDelayedTask = new DelayedTask(BuildTypes, 100, LogicObject);
            myDelayedTask.Start();
        }
        catch (Exception ex)
        {
            Log.Error("Bus Select List", $"Initialization failed: {ex.Message}");
        }
    }

    private void BuildTypes()
    {
        try
        {
            int numBus = Owner.GetVariable(NUM_BUS_SIZE).Value;

            if (numBus < 1) numBus = 1;

            CreateBusSelectInstances(numBus);
        }
        catch (Exception ex)
        {
            Log.Error("Bus Select List", $"Failed to build types: {ex.Message}");
        }
    }

    // Alternately load Bus Select List.
    private void CreateBusSelectInstances(int numBus)
    {
        IUANode setbusTypeNode = Project.Current.Find("raP_5_20_raP_Opr_OrgView_cmd_OCmd_SetBus");

        if (setbusTypeNode == null)
        {
            Log.Error("Bus Select List", "cmd_OCmd_SetBus type node not found.");
            return;
        }

        SetLoading(true);
        try
        {
            for (int i = 1; i <= numBus; i++)
            {
                CreateInstance("BUS_", i, setbusTypeNode);
            }
        }
        catch (Exception ex)
        {
            Log.Error("Bus Select List", $"CreateBusSelectInstances error: {ex.Message}");
        }
        finally
        {
            SetLoading(false);
        }
    }

    // Create a single instance.
    private void CreateInstance(string prefix, int idx, IUANode templateNode)
    {
        try
        {
            IUAObject newInstance = InformationModel.MakeObject(prefix + idx.ToString(), templateNode.NodeId);
            newInstance.GetVariable(NUM_BUS_INDEX).Value = idx.ToString();
            Owner.Add(newInstance);
        }
        catch (Exception ex)
        {
            Log.Error("Bus Select List", $"Failed to create {prefix}{idx}: {ex.Message}");
        }
    }

    // Loading
    private void SetLoading(bool isLoading)
    {
        var v = Owner.GetVariable(IS_LOADING_VAR_NAME);
        if (v == null)
        {
            var loadingVar = InformationModel.MakeVariable(IS_LOADING_VAR_NAME, OpcUa.DataTypes.Boolean);
            Owner.Add(loadingVar);
            v = loadingVar;
        }
        v.Value = isLoading;
    }
}
