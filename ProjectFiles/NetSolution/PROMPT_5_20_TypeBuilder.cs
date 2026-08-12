#region Using directives
using System;
using UAManagedCore;
using OpcUa = UAManagedCore.OpcUa;
using FTOptix.UI;
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
using System.Threading;
using FTOptix.DataLogger;
using System.Linq;
using FTOptix.InfluxDBStore;
using FTOptix.InfluxDBStoreLocal;
using FTOptix.SerialPort;
#endregion

public class PROMPT_5_20_TypeBuilder : BaseNetLogic
{
    const string COBITEM_NAME = "my_Item";
    const int MaxRetry = 20;
    const int RetryInterval = 50;

    public override void Start()
    {
        WaitAndBuildTypes(0);
    }

    public override void Stop()
    {
        // Insert code to be executed when the user-defined logic is stopped
    }

    private void WaitAndBuildTypes(int retry)
    {
        var aliasNode = Owner.GetAlias("raSDK1_DialogBox");
        if (aliasNode == null) return;

        var launchObj = InformationModel.GetObject(aliasNode.NodeId);
        var refTagValue = launchObj.GetVariable("Ref_Tag_Prompts")?.Value;

        if (refTagValue == null || refTagValue.Value == null)
        {
            if (retry >= MaxRetry)
            {
                Log.Warning("PROMPT_5_20_TypeBuilder", "Ref_Tag_Prompts not found after max retries");
                return;
            }
            new DelayedTask(() => WaitAndBuildTypes(retry + 1), RetryInterval, LogicObject).Start();
            return;
        }

        var refTag = InformationModel.Get(refTagValue);
        if (refTag == null) return;

        BuildTypes(launchObj, refTag);
    }

    private void BuildTypes(IUAObject launchObj, IUANode refTag)
    {
        string refTagBrowserName = string.Empty;
        try
        {
            refTagBrowserName = InformationModel.Get(launchObj.GetVariable("Ref_Tag").Value).BrowseName;
            //var promptCounts = refTag.Children.Count(c => int.TryParse(c.BrowseName, out _));

            for (int i = 0; i < 32; i++)
            {
                string childName = COBITEM_NAME + i.ToString();
                IUANode child = Owner.Find(childName);

                // Remove the child who is not exist
                var refTagElement = refTag.Get<IUAVariable>(i.ToString() + "/Cfg_Label");

                if (refTagElement == null)
                {
                    Owner.Remove(child);
                }
            }
        }
        catch
        {
            Log.Error("Build Types", "Error getting BankTag " + refTagBrowserName + "s");
        }
    }
}
