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

public class raSDK1_NL_AddAdditional_Ref_tags : BaseNetLogic
{
    public override void Start()
    {
        // Insert code to be executed when the user-defined logic is started
        CreateRefTags();
    }

    public override void Stop()
    {
        // Insert code to be executed when the user-defined logic is stopped
    }


    [ExportMethod]
    public void CreateRefTags()
    {
        string BasePath = "";
        IUAObject launchObj = null;
        try
        {

            // Step 1: Get the Alias Node to determine the Launch Object
            var aliasNode = Owner.Owner.GetAlias("raSDK1_DialogBox");
            launchObj = InformationModel.GetObject(aliasNode.NodeId);

            // Step 2: Get the Ref_Tag from the Alias
            var refTag = InformationModel.Get(launchObj.GetVariable("Ref_Tag").Value);
            string topContainer = "";
            string refTagBrowsePath = GetOptixPathByNode(refTag, topContainer);
            Log.Info("Ref_Tag browse path:  " + refTagBrowsePath);
            string tag_0_BrowsePath = "CommDrivers" + refTagBrowsePath.Split("CommDrivers")[1];
            var SuffixLength = Owner.GetVariable("Suffix_ReplaceLength").Value;
            if (SuffixLength > 0)
            {
                BasePath = tag_0_BrowsePath.Substring(0, tag_0_BrowsePath.Length - SuffixLength);
            }
            else
            {
                BasePath = tag_0_BrowsePath;
            }

                //Step 3: Get suffix tags
            string[] suffixTags = Owner.GetVariable("Suffix_Tags").Value;
            var filtered = suffixTags.Where(s => !string.IsNullOrWhiteSpace(s)).ToArray();
            foreach(var item in filtered)
            {
                this.AddToLaunchObect(launchObj, BasePath,item, false);
            }
            //Step 4 : Get station
            var Ref_StsVariable = Owner.GetVariable("Ref_Station").Value;
            var Display_ControllerName = Owner.GetVariable("Display_ControllerName").Value;
            if (Ref_StsVariable == true )
            {
                int index = BasePath.IndexOf("/Tags");
                string trimmedPath = BasePath.Substring(0, index);
                this.AddToLaunchObect(launchObj, trimmedPath, "_Station", true);
                if(Display_ControllerName == true)
                {
                    var logixBankTags = Project.Current.Get(trimmedPath);
                    var ControllerNameobject = logixBankTags.Children.Get("StationStatusVariables").Children.Get("ControllerName");
                    var ControllerName = ((UAManagedCore.UAVariable)ControllerNameobject).Value;
                    Owner.GetVariable("ControllerName").Value = ControllerName;
                }
            }


            //  We need to force a reload of the panel, since the panel may have rendered before we added the bank reference tags
            var navPanel = Owner.Owner.Find("NavigationPanel");
            var navPanelUI = (NavigationPanel)navPanel;
            //Log.Info(navPanel.BrowseName);
            navPanelUI.ChangePanelByTabName("Home");

            //navPanel = Owner.Find("np_Advanced");
            //navPanelUI = (NavigationPanel)navPanel;
            ////Log.Info(navPanel.BrowseName);
            //navPanelUI.ChangePanelByTabName("Engineering");


        }
        catch
        {
            if (launchObj == null)
            {
                Log.Error("Dialog Box startup", "Error getting Alias Node Id");
            }
            else if (BasePath == "")
            {
                Log.Error("Dialog Box startup", "CommDrivers folder not found");
            }
            else
            {
                Log.Error("Dialog Box startup", "Unknown error occured");
            }
        }

    }


    public void AddToLaunchObect(IUAObject launchObj, string basePath, string suffix, bool station)
    {
        IUANode logixBankTag = null;

        if (station == false)
        {
            logixBankTag = Project.Current.Get(basePath + suffix);
            if(logixBankTag == null)
            {
                Log.Warning("Dialog Box startup", $"\"{basePath + suffix}\" is unresolved.");
                return;
            }
        }
        else
        {
            logixBankTag = Project.Current.Get(basePath);
        }
        // Make new variable for each bank
        IUAVariable newBankVar = InformationModel.MakeVariable("Ref_Tag" + suffix, OpcUa.DataTypes.NodeId);
        // Assign value of Logix Tag NodeId to new variable 
        newBankVar.Value = logixBankTag.NodeId;
        // Add new variable into the launch object
        launchObj.Add(newBankVar);
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
