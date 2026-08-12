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
using System.Text.RegularExpressions;
using System.ComponentModel.DataAnnotations;
using FTOptix.DataLogger;
using FTOptix.InfluxDBStore;
using FTOptix.InfluxDBStoreLocal;
using FTOptix.SerialPort;
#endregion

public class AuditSigningConfiguration : BaseNetLogic
{
    private LongRunningTask myLongRunningTask;
    private const string CommDriversCategoryFolderType = "CommDriversCategoryFolder";
    private const string RAEtherNetIPDriverType = "RAEtherNetIPDriver";
    private const string RAEtherNetIPStationType = "RAEtherNetIPStation";
    [ExportMethod]
    public void CreateAuditSigning()
    {
        myLongRunningTask = new LongRunningTask(CreateAuditSigningTask, LogicObject);
        myLongRunningTask.Start();
        UAManagedCore.Log.Info("AuditSigning Configuration", "Importing audit configuration... Please wait...");

    }
    public void CreateAuditSigningTask(LongRunningTask task)
    {
        var eSignatureTemplate = new List<EsigTemplate>
        {
            new EsigTemplate { DataType = "P_ANALOG_OUTPUT", TagMember = "MCmd_Bypass", EsigWorkflowType = "Confirm", Caption = "Bypass interlocks for the device" },
            new EsigTemplate { DataType = "P_DISCRETE_4STATE", TagMember = "MCmd_Bypass", EsigWorkflowType = "Confirm", Caption = "Bypass interlocks and permissives for the device" },
            new EsigTemplate { DataType = "P_DISCRETE_OUTPUT", TagMember = "MCmd_Bypass", EsigWorkflowType = "Confirm", Caption = "Bypass interlocks and permissives for the device" },
            new EsigTemplate { DataType = "P_MOTOR_DISCRETE", TagMember = "MCmd_Bypass", EsigWorkflowType = "Confirm", Caption = "Bypass interlocks and permissives for the device" },
            new EsigTemplate { DataType = "P_PID", TagMember = "MCmd_Bypass", EsigWorkflowType = "Confirm", Caption = "Bypass interlocks for the device" },
            new EsigTemplate { DataType = "P_VALVE_DISCRETE", TagMember = "MCmd_Bypass", EsigWorkflowType = "Confirm", Caption = "Bypass interlocks and permissives for the device" },
            new EsigTemplate { DataType = "P_VARIABLE_SPEED_DRIVE", TagMember = "MCmd_Bypass", EsigWorkflowType = "Confirm", Caption = "Bypass interlocks and permissives for the device" },
            new EsigTemplate { DataType = "P_ANALOG_OUTPUT", TagMember = "MCmd_OoS", EsigWorkflowType = "Confirm", Caption = "Take the device Out of Service" },
            new EsigTemplate { DataType = "P_DEADBAND", TagMember = "MCmd_OoS", EsigWorkflowType = "Confirm", Caption = "Take the device Out of Service" },
            new EsigTemplate { DataType = "P_DISCRETE_4STATE", TagMember = "MCmd_OoS", EsigWorkflowType = "Confirm", Caption = "Take the device Out of Service" },
            new EsigTemplate { DataType = "P_DISCRETE_OUTPUT", TagMember = "MCmd_OoS", EsigWorkflowType = "Confirm", Caption = "Take the device Out of Service" },
            new EsigTemplate { DataType = "P_DOSING", TagMember = "MCmd_OoS", EsigWorkflowType = "Confirm", Caption = "Take the device Out of Service" },
            new EsigTemplate { DataType = "P_MOTOR_DISCRETE", TagMember = "MCmd_OoS", EsigWorkflowType = "Confirm", Caption = "Take the device Out of Service" },
            new EsigTemplate { DataType = "P_PID", TagMember = "MCmd_OoS", EsigWorkflowType = "Confirm", Caption = "Take the device Out of Service" },
            new EsigTemplate { DataType = "P_VALVE_DISCRETE", TagMember = "MCmd_OoS", EsigWorkflowType = "Confirm", Caption = "Take the device Out of Service" },
            new EsigTemplate { DataType = "P_VARIABLE_SPEED_DRIVE", TagMember = "MCmd_OoS", EsigWorkflowType = "Confirm", Caption = "Take the device Out of Service" },
            new EsigTemplate { DataType = "P_ANALOG_INPUT", TagMember = "MCmd_SubstPV", EsigWorkflowType = "Confirm", Caption = "Replace the PV with a substitute value for the device" },
            new EsigTemplate { DataType = "P_DISCRETE_INPUT", TagMember = "MCmd_SubstPV", EsigWorkflowType = "Confirm", Caption = "Replace the PV with a substitute value for the device" },
            new EsigTemplate { DataType = "P_ANALOG_INPUT", TagMember = "MCmd_Virtual", EsigWorkflowType = "Confirm", Caption = "Place the device in virtual operation" },
            new EsigTemplate { DataType = "P_ANALOG_OUTPUT", TagMember = "MCmd_Virtual", EsigWorkflowType = "Confirm", Caption = "Place the device in virtual operation" },
            new EsigTemplate { DataType = "P_DISCRETE_4STATE", TagMember = "MCmd_Virtual", EsigWorkflowType = "Confirm", Caption = "Place the device in virtual operation" },
            new EsigTemplate { DataType = "P_DISCRETE_INPUT", TagMember = "MCmd_Virtual", EsigWorkflowType = "Confirm", Caption = "Place the device in virtual operation" },
            new EsigTemplate { DataType = "P_DISCRETE_OUTPUT", TagMember = "MCmd_Virtual", EsigWorkflowType = "Confirm", Caption = "Place the device in virtual operation" },
            new EsigTemplate { DataType = "P_DOSING", TagMember = "MCmd_Virtual", EsigWorkflowType = "Confirm", Caption = "Place the device in virtual operation" },
            new EsigTemplate { DataType = "P_MOTOR_DISCRETE", TagMember = "MCmd_Virtual", EsigWorkflowType = "Confirm", Caption = "Place the device in virtual operation" },
            new EsigTemplate { DataType = "P_VALVE_DISCRETE", TagMember = "MCmd_Virtual", EsigWorkflowType = "Confirm", Caption = "Place the device in virtual operation" },
            new EsigTemplate { DataType = "P_VARIABLE_SPEED_DRIVE", TagMember = "MCmd_Virtual", EsigWorkflowType = "Confirm", Caption = "Place the device in virtual operation" },
            new EsigTemplate { DataType = "P_ANALOG_HART", TagMember = "MCmd_Virtual", EsigWorkflowType = "Confirm", Caption = "Place the device in virtual operation" },
            new EsigTemplate { DataType = "P_LEAD_LAG_STANDBY", TagMember = "MCmd_OoS", EsigWorkflowType = "Confirm", Caption = "Take the device Out of Service" },
            new EsigTemplate { DataType = "P_LEAD_LAG_STANDBY", TagMember = "MCmd_Bypass", EsigWorkflowType = "Confirm", Caption = "Bypass interlocks and permissives for the device" },
            new EsigTemplate { DataType = "P_DISCRETE_N_POSITION", TagMember = "MCmd_Virtual", EsigWorkflowType = "Confirm", Caption = "Place the device in virtual operation" },
            new EsigTemplate { DataType = "P_DISCRETE_N_POSITION", TagMember = "MCmd_Bypass", EsigWorkflowType = "Confirm", Caption = "Bypass interlocks and permissives for the device" },
            new EsigTemplate { DataType = "P_DISCRETE_N_POSITION", TagMember = "MCmd_OoS", EsigWorkflowType = "Confirm", Caption = "Take the device Out of Service" },
            new EsigTemplate { DataType = "P_DISCRETE_MIX_PROOF", TagMember = "MCmd_Virtual", EsigWorkflowType = "Confirm", Caption = "Place the device in virtual operation" },
            new EsigTemplate { DataType = "P_DISCRETE_MIX_PROOF", TagMember = "MCmd_OoS", EsigWorkflowType = "Confirm", Caption = "Take the device Out of Service" },
            new EsigTemplate { DataType = "P_DISCRETE_MIX_PROOF", TagMember = "MCmd_Bypass", EsigWorkflowType = "Confirm", Caption = "Bypass interlocks for the device" },
            new EsigTemplate { DataType = "raP_Dvc_LgxCPU_5x80", TagMember = "MCmd_Enable", EsigWorkflowType = "Confirm", Caption = "Enabling data collection will affect communication and processor performance. Be sure to disable data collection when you are finished monitoring the controller." },
            new EsigTemplate { DataType = "raP_Dvc_LgxCPU_5x80", TagMember = "MCmd_Virtual", EsigWorkflowType = "Confirm", Caption = "Place the device in virtual operation" },
            new EsigTemplate { DataType = "raP_Dvc_LgxCPU_5x80", TagMember = "MCmd_Bypass", EsigWorkflowType = "Confirm", Caption = "Bypass connection checking for the device" },
            new EsigTemplate { DataType = "raP_Dvc_LgxRedun", TagMember = "MCmd_Switchover", EsigWorkflowType = "Confirm", Caption = "Initiate switchover for controller" },
            new EsigTemplate { DataType = "raP_Opr_Area", TagMember = "MCmd_Virtual", EsigWorkflowType = "Confirm", Caption = "Place the device in virtual operation" },
            new EsigTemplate { DataType = "raP_Opr_Area", TagMember = "MCmd_OoS", EsigWorkflowType = "Confirm", Caption = "Take the device Out of Service" },
            new EsigTemplate { DataType = "raP_Opr_EMGen", TagMember = "MCmd_StateForce", EsigWorkflowType = "Confirm", Caption = "Force State" },
            new EsigTemplate { DataType = "raP_Opr_EMGen", TagMember = "MCmd_OoS", EsigWorkflowType = "Confirm", Caption = "Take the device Out of Service" },
            new EsigTemplate { DataType = "raP_Opr_EMGen", TagMember = "MCmd_Bypass", EsigWorkflowType = "Confirm", Caption = "Bypass interlocks and permissives for the device" },
            new EsigTemplate { DataType = "raP_Opr_EPGen", TagMember = "OCmd_Abort", EsigWorkflowType = "Confirm", Caption = "Abort Phase" },
            new EsigTemplate { DataType = "raP_Opr_EPGen", TagMember = "MCmd_StateForce", EsigWorkflowType = "Confirm", Caption = "Force State" },
            new EsigTemplate { DataType = "raP_Opr_EPGen", TagMember = "MCmd_OoS", EsigWorkflowType = "Confirm", Caption = "Take the device Out of Service" },
            new EsigTemplate { DataType = "raP_Opr_EPGen", TagMember = "MCmd_Bypass", EsigWorkflowType = "Confirm", Caption = "Bypass interlocks and permissives for the device" },
            new EsigTemplate { DataType = "raP_Opr_Unit", TagMember = "MCmd_Virtual", EsigWorkflowType = "Confirm", Caption = "Place the device in virtual operation" },
            new EsigTemplate { DataType = "raP_Opr_Unit", TagMember = "MCmd_OoS", EsigWorkflowType = "Confirm", Caption = "Take the device Out of Service" },
            new EsigTemplate { DataType = "raP_Opr_Unit", TagMember = "MCmd_Bypass", EsigWorkflowType = "Confirm", Caption = "Bypass interlocks and permissives for the device" },
            new EsigTemplate { DataType = "P_ANALOG_INPUT", TagMember = "@Alarms/Alm_**/OperShelve", EsigWorkflowType = "Confirm", Caption = "Shelve alarm" },
            new EsigTemplate { DataType = "P_ANALOG_INPUT_DUAL", TagMember = "@Alarms/Alm_**/OperShelve", EsigWorkflowType = "Confirm", Caption = "Shelve alarm" },
            new EsigTemplate { DataType = "P_ANALOG_INPUT_MULTI", TagMember = "@Alarms/Alm_**/OperShelve", EsigWorkflowType = "Confirm", Caption = "Shelve alarm" },
            new EsigTemplate { DataType = "P_ANALOG_OUTPUT", TagMember = "@Alarms/Alm_**/OperShelve", EsigWorkflowType = "Confirm", Caption = "Shelve alarm" },
            new EsigTemplate { DataType = "P_DISCRETE_4STATE", TagMember = "@Alarms/Alm_**/OperShelve", EsigWorkflowType = "Confirm", Caption = "Shelve alarm" },
            new EsigTemplate { DataType = "P_DEADBAND", TagMember = "@Alarms/Alm_**/OperShelve", EsigWorkflowType = "Confirm", Caption = "Shelve alarm" },
            new EsigTemplate { DataType = "P_DISCRETE_INPUT", TagMember = "@Alarms/Alm_**/OperShelve", EsigWorkflowType = "Confirm", Caption = "Shelve alarm" },
            new EsigTemplate { DataType = "P_DISCRETE_OUTPUT", TagMember = "@Alarms/Alm_**/OperShelve", EsigWorkflowType = "Confirm", Caption = "Shelve alarm" },
            new EsigTemplate { DataType = "P_DOSING", TagMember = "@Alarms/Alm_**/OperShelve", EsigWorkflowType = "Confirm", Caption = "Shelve alarm" },
            new EsigTemplate { DataType = "P_LEAD_LAG_STANDBY", TagMember = "@Alarms/Alm_**/OperShelve", EsigWorkflowType = "Confirm", Caption = "Shelve alarm" },
            new EsigTemplate { DataType = "P_MOTOR_DISCRETE", TagMember = "@Alarms/Alm_**/OperShelve", EsigWorkflowType = "Confirm", Caption = "Shelve alarm" },
            new EsigTemplate { DataType = "P_DISCRETE_N_POSITION", TagMember = "@Alarms/Alm_**/OperShelve", EsigWorkflowType = "Confirm", Caption = "Shelve alarm" },
            new EsigTemplate { DataType = "P_PID", TagMember = "@Alarms/Alm_**/OperShelve", EsigWorkflowType = "Confirm", Caption = "Shelve alarm" },
            new EsigTemplate { DataType = "P_VALVE_DISCRETE", TagMember = "@Alarms/Alm_**/OperShelve", EsigWorkflowType = "Confirm", Caption = "Shelve alarm" },
            new EsigTemplate { DataType = "P_DISCRETE_MIX_PROOF", TagMember = "@Alarms/Alm_**/OperShelve", EsigWorkflowType = "Confirm", Caption = "Shelve alarm" },
            new EsigTemplate { DataType = "P_VARIABLE_SPEED_DRIVE", TagMember = "@Alarms/Alm_**/OperShelve", EsigWorkflowType = "Confirm", Caption = "Shelve alarm" },
            new EsigTemplate { DataType = "raP_Dvc_LgxChangeDet", TagMember = "@Alarms/Alm_**/OperShelve", EsigWorkflowType = "Confirm", Caption = "Shelve alarm" },
            new EsigTemplate { DataType = "raP_Dvc_LgxModuleSts", TagMember = "@Alarms/Alm_**/OperShelve", EsigWorkflowType = "Confirm", Caption = "Shelve alarm" },
            new EsigTemplate { DataType = "raP_Dvc_LgxModuleSts", TagMember = "MCmd_Virtual", EsigWorkflowType = "Confirm", Caption = "Place the device in virtual operation" },
            new EsigTemplate { DataType = "raP_Dvc_LgxModuleSts", TagMember = "MCmd_Bypass", EsigWorkflowType = "Confirm", Caption =  "Bypass connection checking for the device" },
            new EsigTemplate { DataType = "raP_Dvc_LgxRedun", TagMember = "@Alarms/Alm_**/OperShelve", EsigWorkflowType = "Confirm", Caption = "Shelve alarm" },
            new EsigTemplate { DataType = "raP_Dvc_LgxTaskMon", TagMember = "@Alarms/Alm_**/OperShelve", EsigWorkflowType = "Confirm", Caption = "Shelve alarm" },
            new EsigTemplate { DataType = "raP_Opr_Area", TagMember = "@Alarms/Alm_**/OperShelve", EsigWorkflowType = "Confirm", Caption = "Shelve alarm" },
            new EsigTemplate { DataType = "raP_Opr_EMGen", TagMember = "@Alarms/Alm_**/OperShelve", EsigWorkflowType = "Confirm", Caption = "Shelve alarm" },
            new EsigTemplate { DataType = "raP_Opr_EPGen", TagMember = "@Alarms/Alm_**/OperShelve", EsigWorkflowType = "Confirm", Caption = "Shelve alarm" },
            new EsigTemplate { DataType = "raP_Opr_ExtddAlm", TagMember = "@Alarms/Alm_**/OperShelve", EsigWorkflowType = "Confirm", Caption = "Shelve alarm" },
            new EsigTemplate { DataType = "raP_Opr_Prompt", TagMember = "@Alarms/Alm_**/OperShelve", EsigWorkflowType = "Confirm", Caption = "Shelve alarm" },
            new EsigTemplate { DataType = "raP_Opr_Unit", TagMember = "@Alarms/Alm_**/OperShelve", EsigWorkflowType = "Confirm", Caption = "Shelve alarm" },
            new EsigTemplate { DataType = "raP_Opr_Seq", TagMember = "Cmd_StepDel", EsigWorkflowType = "Confirm", Caption = "Delete Step" },
            new EsigTemplate { DataType = "raP_Opr_Seq", TagMember = "MCmd_Bypass", EsigWorkflowType = "Confirm", Caption = "Bypass interlocks and permissives for the device" },
            new EsigTemplate { DataType = "raP_Opr_Seq", TagMember = "MCmd_Check", EsigWorkflowType = "Confirm", Caption = "Disable interlock bypass" },
            new EsigTemplate { DataType = "raP_Opr_Seq", TagMember = "MCmd_SeqStepForce", EsigWorkflowType = "Confirm", Caption = "Force Step" },
            new EsigTemplate { DataType = "raP_Opr_OrgScan", TagMember = "OCmd_DelNode", EsigWorkflowType = "Confirm", Caption = "Delete this node" },
            new EsigTemplate { DataType = "raP_Opr_OrgScan", TagMember = "OCmd_AddChild", EsigWorkflowType = "Confirm", Caption = "Add a child node to this node" },
        };
        var commDrivers = Project.Current.Get("CommDrivers");
        var dataTypeList = eSignatureTemplate.Select(Item => Item.DataType).Distinct().ToList();
        var tags = SearchTag(commDrivers, dataTypeList);
        bool isCreated = false;
        foreach (var row in eSignatureTemplate)
        {
            foreach (var Controlinfo in tags)
            {
                IUANode EtherNetIPDriverNode = Project.Current.Find(Controlinfo.DriverName);
                var inputPath = GetTagPath(EtherNetIPDriverNode, "CommDrivers", Controlinfo.FolderName, Controlinfo.TagName, Controlinfo.StationName);
                var ControllerTag = Project.Current.Get(inputPath);
                string TagName = row.TagMember;
                if (ControllerTag != null)
                {
                    string dataTypeBrowserName = ((UAManagedCore.UANode)((UAManagedCore.UAVariable)ControllerTag).VariableType).BrowseName;
                    if (Regex.IsMatch(dataTypeBrowserName, $"^{row.DataType}\\d*$") && Regex.IsMatch(Controlinfo.DataType, $"^{row.DataType}\\d*$"))
                    {
                        // Check if this is an array data type
                        int arrayCount = GetArrayCount(ControllerTag);
                        if (arrayCount > 0)
                        {
                            // Handle array elements
                            for (int i = 0; i < arrayCount; i++)
                            {
                                string arrayPath = $"{i}";
                                var arrayTag = GetVariableByPath(ControllerTag, arrayPath);
                                if (arrayTag != null)
                                {
                                    ProcessTagMembers(row, arrayTag, arrayTag, dataTypeBrowserName);
                                    isCreated = true;
                                }
                            }
                        }
                        else
                        {
                            // Handle non-array elements (existing logic)
                            ProcessTagMembers(row, ControllerTag, ControllerTag, dataTypeBrowserName);
                            isCreated = true;
                        }
                    }
                }
            }
        }
        if (isCreated) // Check the flag and print the log message 
        {
            UAManagedCore.Log.Info("AuditSigning Configuration", "AuditSigning configuration imported.");
        }
        else
        {
            UAManagedCore.Log.Error("AuditSigning Configuration", "No AuditSigning created.");
        }
    }
    private int GetArrayCount(IUANode controllerTag)
    {
        return controllerTag.Children.Count(child => Regex.IsMatch(child.BrowseName, @"^\d+$"));
    }

    public void AddAuditSignature(EsigTemplate row, IUANode tagChildren, IUANode controllerTag, string tagName = "")
    {
        var audSig = InformationModel.MakeObject<FTOptix.AuditSigning.AuditInfo>("AuditSigning Signature");
        audSig.WorkflowType = WorkflowType.Confirm;
        tagChildren.Add(audSig);
        CreateIdentifiersForItems(row.Caption, "Caption", tagChildren);
    }
    public void CreateIdentifiersForItems(string items, string variableName, IUANode tagChildren, string tagName = "")
    {
        // Create a new variable and add it to the children of the tag
        IUAVariable confirmItem = InformationModel.MakeVariable(variableName, OpcUa.DataTypes.LocalizedText);
        tagChildren.Find("AuditSigning Signature").Children.Add(confirmItem);
        if (items.Contains("**"))
        {
            items = items.Replace("**", tagName);
        }
        // Find matches in the items string
        if (!Regex.IsMatch(items, @"\{.*\}"))
        {
            // Set the value of the confirmItem to the items string
            try
            {
                int correctNameSpace = LogicObject.GetVariable("LocalizationDictionary").NodeId.NamespaceIndex;
                LocalizedText localizedText = new LocalizedText(correctNameSpace, items, "", "");
                confirmItem.Value = localizedText;
            }
            catch
            {
                Log.Info("AuditSigning Configuration", "The first localization dictionary found will be used since the LocalizationDictionary variable cannot be not found");
            }

        }
    }
    public class EsigTemplate
    {
        public string DataType { get; set; }
        public string TagMember { get; set; }
        public string EsigWorkflowType { get; set; }
        public string Caption { get; set; }
    }
    private List<ControlInfo> SearchTag(IUANode node, List<string> dataTypeList)
    {
        // Initialize a list to store the tags
        List<ControlInfo> tags = new List<ControlInfo>();
        string objectTypeBrowseName = string.Empty;
        // Determine the type of the node and get its BrowseName
        if (node is IUAVariable)
        {
            objectTypeBrowseName = ((IUAVariable)node).VariableType.BrowseName;
        }
        else
        {
            objectTypeBrowseName = ((UAObject)node).ObjectType.BrowseName;
        }
        // If the node type is CommDriversCategoryFolderType or RAEtherNetIPDriverType, recursively search its children
        if (objectTypeBrowseName == CommDriversCategoryFolderType || objectTypeBrowseName == RAEtherNetIPDriverType)
        {
            foreach (var item in node.Children)
            {
                tags.AddRange(SearchTag(item, dataTypeList));
            }
        }
        // If the node type is RAEtherNetIPStationType, search the "Tags" folder
        else if (objectTypeBrowseName == RAEtherNetIPStationType)
        {
            var folder = node.Find<Folder>("Tags");
            foreach (var item in folder.Children)
            {
                tags.AddRange(SearchTag(item, dataTypeList));
            }
        }
        // If the node is a Folder, search its non-Folder children
        else if (node is Folder)
        {
            foreach (var item in node.Children)
            {
                if (item is not Folder)
                {
                    tags.AddRange(SearchTag(item, dataTypeList));
                }
            }
        }
        // For other types of nodes, extract the control name and control folder name from the path
        else
        {
            // Check if the prefix is in the dataTypeList
            if (dataTypeList.Any(item => Regex.IsMatch(objectTypeBrowseName, $"^{item}\\d*$")))
            {
                var inputPath = FindPath(node).TrimEnd('/');
                string pattern = @".*/(.*?)/(.*?)/Tags/(.*?)/.*";
                Match match = Regex.Match(inputPath, pattern);
                if (match.Success)
                {
                    ControlInfo controlInfo = new ControlInfo
                    {
                        DataType = objectTypeBrowseName,
                        DriverName = match.Groups[1].Value,
                        FolderName = match.Groups[3].Value,
                        StationName = match.Groups[2].Value,
                        TagName = node.BrowseName
                    };
                    tags.Add(controlInfo);
                }
            }
        }
        return tags;
    }
    private string FindPath(IUANode node, string path = "")
    {
        if (node.BrowseName != "CommDrivers")
            path = FindPath(node.Owner, path);
        path += node.BrowseName + "/";
        return path;
    }
    public class ControlInfo
    {
        public string StationName { get; set; }
        public string FolderName { get; set; }
        public string DataType { get; set; }
        public string TagName { get; set; }
        public string DriverName { get; set; }
    }
    public static string GetTagPath(IUANode inputNode, string topContainer, string folderName, string tagName, string stationName)
    {
        List<string> pathToVar = new List<string>();
        FindBrowsePath(inputNode);
        if (pathToVar.Count > 0)
        {
            var Path = ConstructBrowsePath();
            Path += "/" + stationName + "/" + "Tags" + "/" + folderName + "/" + tagName;
            return Path;
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
    public void RemovTagMemberAuditSigning(IUANode tag)
    {
        foreach (var child in tag.Children)
        {
            if (child.BrowseName == "AuditSigning Signature")
            {
                tag.Remove(child);
            }
        }
    }
    private UAManagedCore.UAVariable GetVariableByPath(IUANode node, string path)
    {
        string[] pathParts = path.Split('/');


        foreach (string part in pathParts)
        {
            try
            {
                node = (UANode)node.Children.Get(part);
                if (node == null)
                {
                    return null;

                }
            }
            catch
            {
                throw;
            }
        }
        return node as UAManagedCore.UAVariable;
    }

    private void ProcessTagMembers(EsigTemplate row, IUANode targetTag, IUANode baseTag, string dataTypeBrowserName)
    {
        foreach (var tagChildren in targetTag.Children)
        {
            List<string> tagBrowsers = new List<string>();
            string tagBrowser = tagChildren.BrowseName;
            string TagName = row.TagMember;

            if (tagBrowser == row.TagMember && Regex.IsMatch(dataTypeBrowserName, $"^{row.DataType}\\d*$"))
            {
                RemovTagMemberAuditSigning(tagChildren);
                AddAuditSignature(row, tagChildren, baseTag);
            }

            string originalTagName = TagName;
            if (tagBrowser == "@Alarms" && TagName.Contains("/") && TagName.Contains("@Alarms") && Regex.IsMatch(dataTypeBrowserName, $"^{row.DataType}\\d*$"))
            {
                if (TagName.Contains("**"))
                {
                    foreach (var alarmTag in tagChildren.Children)
                    {
                        tagBrowsers.Add(TagName);
                        TagName = originalTagName.Replace("Alm_**", alarmTag.BrowseName);
                        var getTagNamePath = GetVariableByPath(targetTag, TagName);
                        RemovTagMemberAuditSigning(getTagNamePath);
                        // Add the audit for current tag
                        AddAuditSignature(row, getTagNamePath, baseTag, alarmTag.BrowseName.Replace("Alm_", ""));
                        // Reset the tagname to the original value from the row.                                                             
                        TagName = row.TagMember;
                    }
                }
                else
                {
                    var getTagNamePath = GetVariableByPath(targetTag, TagName);
                    RemovTagMemberAuditSigning(getTagNamePath);
                    tagBrowsers.Add(TagName);
                    AddAuditSignature(row, getTagNamePath, baseTag);
                }
            }
        }
    }
}
