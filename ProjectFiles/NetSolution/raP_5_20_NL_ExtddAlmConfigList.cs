#region Using directives
using System;
using UAManagedCore;
using OpcUa = UAManagedCore.OpcUa;
using FTOptix.UI;
using FTOptix.NativeUI;
using FTOptix.HMIProject;
using FTOptix.CoreBase;
using FTOptix.System;
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
using System.Runtime.Serialization;
using System.Linq.Expressions;
using FTOptix.DataLogger;
using FTOptix.InfluxDBStore;
using FTOptix.InfluxDBStoreLocal;
using FTOptix.SerialPort;
#endregion

public class raP_5_20_NL_ExtddAlmConfigList : BaseNetLogic
{
    public override void Start()
    {
        // Insert code to be executed when the user-defined logic is started
        this.AddExtddAlarm();
    }

    public override void Stop()
    {
        // Insert code to be executed when the user-defined logic is stopped
    }

    public void AddExtddAlarm()
    {
        string BasePath = "";
        IUANode alarmPanel = null;
        IUANode launchObj = null;
        int ExtddAlmMaxCount = 0;
        int ExtddAlmsUsed = 0;
        const string LIST_WIDGET_NAME = "raP_5_20_ExtddAlarmConfig";
        const string LOGID = "ExtddAlmConfig";

        IUANode MyWidget = Project.Current.Find(LIST_WIDGET_NAME);

        const string SET_ALARM_TAG_NAME = "Set_Alarm";

        try
        {
            // Get Alarm Panel
            alarmPanel = FindNodeByTypeUpwards(Owner, typeof(raP_5_20_AdvAlarmPanelBase));

            // Step 1: Get the Alias Node to determine the Launch Object
            launchObj = alarmPanel.GetAlias("Tag");

            //  Save the security variable
            var secAlarmConfig = alarmPanel.GetVariable("Sec_AlarmConfig");


            // Step 2: GetOptixPath from Alias
            string topContainer = "";
            string refTagBrowsePath = GetOptixPathByNode(launchObj, topContainer);

            // Step 3:  Do some string manipulation to get the content after the "CommDrivers"
            BasePath = "CommDrivers" + refTagBrowsePath.Split("CommDrivers")[1];

            //Load ExtddAlmMaxCounts
            IUANode logixAlmCountTag = Project.Current.Get(BasePath);
            var ExtddAlmMaxCounts = logixAlmCountTag.Children.GetVariable("Sts_ExtddAlmMaxCount").RemoteRead().Value;
            ExtddAlmMaxCount = Convert.ToInt32(ExtddAlmMaxCounts);
            var ExtddAlmsUsedvar = logixAlmCountTag.Children.GetVariable("Inp_ExtddAlmsUsed").RemoteRead().Value;
            ExtddAlmsUsed = Convert.ToInt32(ExtddAlmsUsedvar);

            // Step 4:  Create instance of "raP_5_20_ExtddAlarmConfig".
            for (int i = 0; i < ExtddAlmMaxCount; i++)
            {
                IUANode newInstance = InformationModel.MakeObject("raP_5_20_ExtddAlarmConfig" + i, MyWidget.NodeId);
                string formattedI = i.ToString("D2");
                newInstance.GetVariable(SET_ALARM_TAG_NAME).Value = formattedI;
                bool addInstance = true;

                if ((ExtddAlmsUsed & (1 << i)) != 0) // if the bit is set, the extended alarm is in use
                {
                    try
                    {
                        //Get actual tag path for "ExtddAlmTag".
                        string alarmTagName = BasePath + '_' + "ExtddAlm" + '_' + formattedI;
                        IUANode logixAlmTag = Project.Current.Get(alarmTagName);
                        newInstance.FindVariable("ExtddAlmTag").Value = logixAlmTag.NodeId;

                        IUANode AlmConditionTag = Project.Current.Get(alarmTagName + "/@Alarms/Alm_Alarm");
                        newInstance.FindVariable("_AlarmCondition").Value = AlmConditionTag.NodeId;
                    }
                    catch
                    {
                        Log.Error(LOGID, "Get " + "_" + "ExtddAlm" + '_' + formattedI + " actual tag path is failed.");
                        addInstance = false;
                    }

                    if (addInstance)
                    {
                        //Set security to "raP_5_20_ExtddAlarmConfig".
                        newInstance.GetVariable("Sec_AlarmConfig").SetDynamicLink(secAlarmConfig);

                        Owner.Add(newInstance);
                        Log.Info(LOGID, "Generate ExtddAlarm_" + formattedI + " to 'raP_5_20_ExtddAlarmConfigList' and read tag successfully");
                    }
                }
            }
        }
        catch
        {
            if (alarmPanel == null)
            {
                Log.Error(LOGID, "Error getting Alarm Panel");
            }
            else if (launchObj == null)
            {
                Log.Error(LOGID, "Error getting Alias Node Id");
            }
            else if (BasePath == "")
            {
                Log.Error(LOGID, "CommDrivers folder not found");
            }
            else if (ExtddAlmMaxCount == 0)
            {
                Log.Error(LOGID, "Error reading Sts_ExtddAlmCount");
            }
            else
            {
                Log.Error(LOGID, "Unknown error occured");
            }
        }

    }

    private IUANode FindNodeByTypeUpwards(IUANode node, Type type)
    {
        while (node != null)
        {
            if (type.IsInstanceOfType(node))
            {
                return node;
            }
            node = node.Owner;
        }
        return null;
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
