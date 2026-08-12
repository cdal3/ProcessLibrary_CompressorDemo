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
using System.Security.Cryptography;
using System.Collections.Generic;
using System.Linq;
using FTOptix.SQLiteStore;
using FTOptix.System;
using FTOptix.DataLogger;
using FTOptix.InfluxDBStore;
using FTOptix.InfluxDBStoreLocal;
using FTOptix.SerialPort;
#endregion

public class PINTLK_5_20_NL_TypeBuilder : BaseNetLogic
{
    public override void Start()
    {
        // Insert code to be executed when the user-defined logic is started
        BuildTypes();
    }

    public override void Stop()
    {
        // Insert code to be executed when the user-defined logic is stopped
    }


    private void BuildTypes()
    {
        const string BANKID_VARIABLE = "Set_BankId";  // Bank ID tag name
        const string ETYPES_OBJECT = "PINTLK_eTypes";  // eTypes Object Name
        const string HASTYPE_TAGNAME = "Cfg_HasType";  // Bank Id tag name used when creating banks

        const string COBITEM_NAME = "my_Item"; // Item name without the numeric reference

        const string LOGID = "Interlock List: Build TYpes"; // ID used for the error logs


        IUAObject launchObj;
        IUAObject eTypesObj;
        sbyte hasType;

        try
        {
            // Get the Alias passed to the Dialog Box
            var aliasNode = Owner.GetAlias("raSDK1_DialogBox");
            launchObj = InformationModel.GetObject(aliasNode.NodeId);

            // Read the Bank Id variable
            int bankId = Owner.GetVariable(BANKID_VARIABLE).Value;
            var refTag = InformationModel.Get(launchObj.GetVariable("Ref_Tag_" + bankId).Value);
            
            string topContainer = "";
            string refTagBrowsePath = GetOptixPathByNode(refTag, topContainer);
            string refTag_BrowsePath = "CommDrivers" + refTagBrowsePath.Split("CommDrivers")[1];
            IUANode logixBankTag = Project.Current.Get(refTag_BrowsePath);

            hasType = (sbyte)logixBankTag.Children.GetVariable(HASTYPE_TAGNAME).RemoteRead().Value;
            Log.Info("HasType = " + hasType);

            eTypesObj = Owner.GetObject(ETYPES_OBJECT);
            
            for (int i = 0; i < 8; i++)
            {
                if ((hasType & (1 << i)) == 0)
                {
                    string childName = COBITEM_NAME + i.ToString();
                    IUANode child = eTypesObj.Find(childName);
                    eTypesObj.Remove(child);
                }
            }


        }
        catch
        {
            Log.Error(LOGID, "Error getting BankTag " + HASTYPE_TAGNAME);
        }

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
