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
using FTOptix.Alarm;
using FTOptix.SQLiteStore;
using FTOptix.System;
using FTOptix.DataLogger;
using FTOptix.InfluxDBStore;
using FTOptix.InfluxDBStoreLocal;
using FTOptix.SerialPort;
#endregion

public class raP_5_20_PINTLK_NL_MaintenanceLoadBanks : BaseNetLogic
{
    private LongRunningTask loadTask;
    private volatile bool stopping = false;

    // Track names of widgets we added so Stop() can explicitly remove them
    private System.Collections.Generic.List<string> addedWidgetNames = new System.Collections.Generic.List<string>();

    public override void Start()
    {
        stopping = false;
        addedWidgetNames.Clear();
        loadTask = new LongRunningTask(LoadBanks, LogicObject);
        loadTask.Start();
    }

    public override void Stop()
    {
        stopping = true;
        loadTask?.Dispose();
        loadTask = null;

        // Explicitly remove any widgets we added to Owner
        foreach (var name in addedWidgetNames)
        {
            try
            {
                var child = Owner.Find(name);
                if (child != null)
                    Owner.Remove(child);
            }
            catch { }
        }
        addedWidgetNames.Clear();
    }

    private void LoadBanks()
    {
        Int32 valBankMap = 0;
        const string BANK_MAP_TAG_NAME = "_Val_BankMap";
        const string BANK_ID_TAG_NAME = "Set_BankId";
        const string LOGID = "Interlock Maintenance: Load Banks";
        const string LIST_WIDGET_NAME = "raP_5_20_PINTLK_MaintList";
        const string BANK_WIDGET_NAME = "raP_5_20_PINTLK_MaintListBank";

        try
        {
            valBankMap = Owner.GetVariable(BANK_MAP_TAG_NAME).Value;
        }
        catch
        {
            if (!stopping) Log.Error(LOGID, "Error reading Bank Map tag " + BANK_MAP_TAG_NAME);
            return;
        }

        if (valBankMap == 0)
        {
            try
            {
                var listWidgetType = Project.Current.Find(LIST_WIDGET_NAME);
                if (listWidgetType == null)
                {
                    if (!stopping) Log.Error(LOGID, "List Widget type not found: " + LIST_WIDGET_NAME);
                    return;
                }

                IUAObject newInstance = InformationModel.MakeObject(LIST_WIDGET_NAME, listWidgetType.NodeId);
                if (stopping) return;
                Owner.Add(newInstance);
                addedWidgetNames.Add(LIST_WIDGET_NAME);
            }
            catch { }
        }
        else
        {
            try
            {
                var bankWidgetType = Project.Current.Find(BANK_WIDGET_NAME);
                if (bankWidgetType == null)
                {
                    if (!stopping) Log.Error(LOGID, "Bank Widget type not found: " + BANK_WIDGET_NAME);
                    return;
                }

                var bankWidgetNodeId = bankWidgetType.NodeId;

                for (int i = 0; i < 8; i++)
                {
                    if (stopping) return;

                    if ((valBankMap & (1 << i)) != 0)
                    {
                        try
                        {
                            IUAObject newInstance = InformationModel.MakeObject(BANK_WIDGET_NAME + i, bankWidgetNodeId);
                            newInstance.GetVariable(BANK_ID_TAG_NAME).Value = i;
                            if (stopping) return;
                            Owner.Add(newInstance);
                            addedWidgetNames.Add(BANK_WIDGET_NAME + i);
                        }
                        catch { }
                    }
                }
            }
            catch { }
        }
    }
}
