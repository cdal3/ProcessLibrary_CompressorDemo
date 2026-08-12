#region Using directives
using FTOptix.AuditSigning;
using FTOptix.CommunicationDriver;
using FTOptix.Core;
using FTOptix.CoreBase;
using FTOptix.HMIProject;
using FTOptix.NetLogic;
using FTOptix.UI;
using Microsoft.VisualBasic.FileIO;
using NPOI.POIFS.Properties;
using NPOI.SS.Formula.Functions;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Data.Common;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using UAManagedCore;
using FTOptix.SQLiteStore;
using FTOptix.System;
using Match = System.Text.RegularExpressions.Match;
using OpcUa = UAManagedCore.OpcUa;
using System.ComponentModel;
using static System.Net.Mime.MediaTypeNames;
using static ImportConfigFile;
using NPOI.HSSF.UserModel;
using System.Runtime.Intrinsics.Arm;
using FTOptix.DataLogger;
using FTOptix.InfluxDBStore;
using FTOptix.InfluxDBStoreLocal;
using FTOptix.SerialPort;
#endregion
public class ImportConfigFile : BaseNetLogic
{
    private const string CommDriversCategoryFolderType = "CommDriversCategoryFolder";
    private const string RAEtherNetIPDriverType = "RAEtherNetIPDriver";
    private const string RAEtherNetIPStationType = "RAEtherNetIPStation";
    private static readonly Dictionary<string, string> extendedTagProperties = new Dictionary<string, string>
    {
        { "Description", "Description" },
        { "Label", "Label" },
        { "Area", "Area" },
        { "Navigation", "Navigation" },
        { "State0", "FalseState" },
        { "State1", "TrueState" },
        { "EngineeringUnit", "EngineeringUnits/DisplayName" },
        { "Max", "EURange/High" },
        { "Min", "EURange/Low" },
        { "Library", "Library" },
        { "Instruction", "Instruction" },
        { "URL","URL"}
    };
    private List<string> validworkflowTypeValues = new List<string> { "Confirm", "Sign", "DoubleSign" };

    private LongRunningTask myLongRunningTask;


    [ExportMethod]
    public void CreateAuditSigning()
    {
        myLongRunningTask = new LongRunningTask(CreateAuditSigningTask, LogicObject);
        myLongRunningTask.Start();
        UAManagedCore.Log.Info("AuditSigning Configuration", "Importing audit configuration... Please wait...");

    }

    public void CreateAuditSigningTask(LongRunningTask task)
    {
        // Get file path for EsigInitDataPath variable
        string filePath = GetFilePath();

        // init the e-signatures of all DataTypes
        bool CleanALLESigConfig = LogicObject.GetVariable("CleanALLESigConfig").Value;
        if (CleanALLESigConfig)
        {
            var nodePath = Project.Current.Get("CommDrivers");
            InitAuditSigning(nodePath);
        }

        // Get all match rows and StationNames and FolderName
        if (filePath != null)
        {
            var (MatchRows, controlInfoList) = GetMatchingRowsAndStationNames(filePath);
            if (MatchRows != null)
            {
                // Convert DataTable to dictionary
                var dictData = ConvertDataTableToDictionary(MatchRows);
                bool isCreated = false; // Add a flag to check if any Audit Signing is created

                foreach (var row in dictData)
                {
                    string TagName = row["TagMember"];
                    string dataType = row["DataType"];

                    try
                    {
                        if (controlInfoList != null)
                        {
                            foreach (var Controlinfo in controlInfoList)
                            {
                                // Find the EtherNetIPDriver based on the DriverName
                                IUANode EtherNetIPDriverNode = Project.Current.Find(Controlinfo.DriverName);

                                // Get the inputPath by finding the tag folder path
                                if (EtherNetIPDriverNode != null)
                                {
                                    var inputPath = GetTagPath(EtherNetIPDriverNode, "CommDrivers", Controlinfo.FolderName, Controlinfo.TagName, Controlinfo.StationName);
                                    // Get ControllerTag from the project
                                    var ControllerTag = Project.Current.Get(inputPath);

                                    if (ControllerTag != null)
                                    {
                                        string dataTypeBrowserName = ((UAVariable)ControllerTag).VariableType.BrowseName;
                                        int arrayCount = GetArrayCount(ControllerTag);
                                        if (arrayCount>0)
                                        {                                        
                                            for (int i = 0; i < arrayCount; i++)
                                            {
                                                string arrayPath = $"{i}";
                                                var arrayTag = GetVariableByPath(ControllerTag, arrayPath);
                                                if (arrayTag == null) continue;
                                                bool matched = ProcessTagChildren(
                                                    arrayTag,
                                                    ref TagName,
                                                    row,
                                                    Controlinfo,
                                                    dataType,
                                                    dataTypeBrowserName,
                                                    ref isCreated,
                                                    null
                                                );                                              
                                            }                              
                                        }
                                        else
                                        {
                                            if (Regex.IsMatch(dataTypeBrowserName, $"^{dataType}\\d*$") &&
                                                Regex.IsMatch(Controlinfo.DataType, $"^{dataType}\\d*$"))
                                            {
                                                var tagBrowsers = new List<string>();

                                                bool matched = ProcessTagChildren(
                                                    ControllerTag,
                                                    ref TagName,
                                                    row,
                                                    Controlinfo,
                                                    dataType,
                                                    dataTypeBrowserName,
                                                    ref isCreated,
                                                    tagBrowsers
                                                );                                             
                                                if (tagBrowsers.Contains(TagName))
                                                {
                                                    tagBrowsers.Clear();
                                                }
                                                else
                                                {
                                                    UAManagedCore.Log.Error(
                                                        "AuditSigning Configuration",
                                                        $"Import failed. No matching \"{Controlinfo.TagName}.{TagName}\"TagMember found."
                                                    );
                                                    continue;
                                                }
                                            }
                                            else if (Regex.IsMatch(dataTypeBrowserName, $"^{dataType}\\d*$"))
                                            {
                                                if (!Regex.IsMatch(Controlinfo.DataType, $"^{dataType}\\d*$"))
                                                {
                                                    UAManagedCore.Log.Error("AuditSigning Configuration", $"Incorrect import DataType '{Controlinfo.DataType}' format.");
                                                }
                                            }
                                        }
                                    }
                                    else
                                    {
                                        UAManagedCore.Log.Error("AuditSigning Configuration", $"Import failed. No matching \"{inputPath}\" path found.");
                                        continue;
                                    }
                                }
                                else
                                {
                                    UAManagedCore.Log.Error("AuditSigning Configuration", $"Import failed. No matching \"{Controlinfo.DriverName}\" DriverName found.");
                                    continue;
                                }
                            }
                        }
                    }
                    catch
                    {
                    
                        isCreated = false;
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
        }
    }

    private bool ProcessTagChildren(
        IUANode parentTag,
        ref string TagName,
        Dictionary<string, string> row,
        ControlInfo Controlinfo,
        string dataType,
        string dataTypeBrowserName,
        ref bool isCreated,
        List<string> tagBrowsers
    )
    {
        bool matched = false;

        foreach (var tagChildren in parentTag.Children)
        {
            string tagBrowser = tagChildren.BrowseName;


            tagBrowsers?.Add(tagBrowser);

            try
            {
                bool shouldBreak = ProcessTagCondition(
                    tagBrowser,
                    dataTypeBrowserName,
                    dataType,
                    ref TagName,
                    parentTag,
                    tagChildren,
                    row,
                    Controlinfo,
                    tagBrowsers,
                    ref isCreated,
                    out bool hit 
                );

                if (hit) matched = true;

                if (shouldBreak)
                {
                    break;
                }
            }
            catch
            {
                continue;
            }
        }

        return matched;
    }

    private bool ProcessTagCondition(
        string tagBrowser,
        string dataTypeBrowserName,
        string dataType,
        ref string TagName,
        IUANode parentTag,
        IUANode tagChildren,
        Dictionary<string, string> row,
        ControlInfo Controlinfo,
        List<string> tagBrowsers,
        ref bool isCreated,
        out bool hit
    )
    {
        hit = false;
        string originalTagName = TagName;


        if (tagBrowser == "@Alarms" && TagName.Contains("/") && TagName.Contains("@Alarms") &&
            Regex.IsMatch(dataTypeBrowserName, $"^{dataType}\\d*$"))
        {
            if (TagName.Contains("**"))
            {
                foreach (var alarmTag in tagChildren.Children)
                {
                    tagBrowsers?.Add(TagName);
                    TagName = originalTagName.Replace("Alm_**", alarmTag.BrowseName);
                    var getTagNamePath = GetVariableByPath(parentTag, TagName);
                    RemovTagMemberAuditSigning(getTagNamePath);
                    AddAuditSignature(row, getTagNamePath, parentTag, Controlinfo, alarmTag.BrowseName.Replace("Alm_", ""));
                    isCreated = true;
                    hit = true;
                    TagName = row["TagMember"]; 
                }
            }
            else
            {
                var getTagNamePath = GetVariableByPath(parentTag, TagName);
                RemovTagMemberAuditSigning(getTagNamePath);
                tagBrowsers?.Add(TagName);
                AddAuditSignature(row, getTagNamePath, parentTag, Controlinfo);
                isCreated = true;
                hit = true;
            }
            return false; 
        }

        if (tagBrowser == "CmdSrc" && TagName.Contains("/") && TagName.Contains("CmdSrc") &&
            Regex.IsMatch(dataTypeBrowserName, $"^{dataType}\\d*$"))
        {
            var getTagNamePath = GetVariableByPath(parentTag, TagName);
            RemovTagMemberAuditSigning(getTagNamePath);
            tagBrowsers?.Add(TagName);
            AddAuditSignature(row, getTagNamePath, parentTag, Controlinfo);
            isCreated = true;
            hit = true;
            return false;
        }

    
        if (tagBrowser == TagName && Regex.IsMatch(dataTypeBrowserName, $"^{dataType}\\d*$"))
        {
            RemovTagMemberAuditSigning(tagChildren);
            AddAuditSignature(row, tagChildren, parentTag, Controlinfo);
            isCreated = true;
            hit = true;
            return true; 
        }
        if (!tagBrowser.Equals("@Alarms") &&
            !tagBrowser.Equals("CmdSrc") &&
            TagName.Contains("/") &&
            !TagName.Contains("@Alarms")&&
            Regex.IsMatch(dataTypeBrowserName, $"^{dataType}\\d*$"))
        {
            var getTagNamePath = GetVariableByPath(parentTag, TagName);
            RemovTagMemberAuditSigning(getTagNamePath);
            tagBrowsers?.Add(TagName);
            AddAuditSignature(row, getTagNamePath, parentTag, Controlinfo);
            isCreated = true;
            hit = true;
            return false; 
        }
        return false;
    }

    private int GetArrayCount(IUANode controllerTag)
    {
        return controllerTag.Children.Count(child => Regex.IsMatch(child.BrowseName, @"^\d+$"));
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

    public void AddAuditSignature(Dictionary<string, string> row, IUANode tagChildren, IUANode controllerTag, ControlInfo Controlinfo, string tagname = "")
    {
        var audSig = InformationModel.MakeObject<FTOptix.AuditSigning.AuditInfo>("AuditSigning Signature");
        //create EsigWorkflow
        string workflowType = string.Empty;
        try
        {
            workflowType = row["EsigWorkflowType"];
            if (workflowType != "")
            {
                switch (workflowType)
                {
                    case "Confirm":
                        audSig.WorkflowType = WorkflowType.Confirm;
                        break;
                    case "Sign":
                        audSig.WorkflowType = WorkflowType.Sign;
                        break;
                    case "DoubleSign":
                        audSig.WorkflowType = WorkflowType.DoubleSign;
                        break;
                    case "Dafault":
                        break;
                    default:
                        break;
                }
            }
        }
        catch
        {
            UAManagedCore.Log.Error("AuditSigning Configuration", "Import failed. No matching EsigWorkflowType found " + "'" + workflowType + "'.");
        }
        //create SignApproverGroup
        string groupsData = string.Empty;
        try
        {
            groupsData = row["SignApproverGroup"];
            if (groupsData != "")
            {
                var groups = groupsData.Split('/');
                for (int m = 0; m < groups.Length; m++)
                {
                    var group = Project.Current.Get("Security/Groups").Find(groups[m]);
                    var audSigGroup = InformationModel.Make<NodePointer>("Group" + (m + 1));
                    audSigGroup.SetValue(group.NodeId);
                    audSigGroup.Kind = group.NodeId;
                    audSig.Find("Groups").Children.Add(audSigGroup);
                }
            }
        }
        catch
        {
            UAManagedCore.Log.Error("AuditSigning Configuration", "Import failed. No matching SignApproverGroup found " + "'" + groupsData + "'.");
        }
        tagChildren.Add(audSig);
        CreateIdentifiersForItems(audSig, row["Caption"], "Caption", tagChildren, controllerTag, tagname);
        CreateIdentifiersForItems(audSig, row["Statement"], "Statement", tagChildren, controllerTag, tagname);
        CreateIdentifiersForItems(audSig, row["ValueMappingContent"], "ValueMappingContent", tagChildren, controllerTag, tagname);
        IUAVariable ValueMappingType = InformationModel.MakeVariable("ValueMappingType", OpcUa.DataTypes.String);
        tagChildren.Find("AuditSigning Signature").Children.Add(ValueMappingType);
        ValueMappingType.Value = row["ValueMappingType"];
        IUAVariable parentStation = InformationModel.MakeVariable("ParentStation", OpcUa.DataTypes.String);
        tagChildren.Find("AuditSigning Signature").Children.Add(parentStation);
        parentStation.Value = Controlinfo.StationName;
        IUAVariable parentTag = InformationModel.MakeVariable("ParentTag", OpcUa.DataTypes.String);
        tagChildren.Find("AuditSigning Signature").Children.Add(parentTag);
        parentTag.Value = controllerTag.BrowseName;
    }
    public void CreateIdentifiersForItems(AuditInfo audSig, string items, string variableName, IUANode tagChildren, IUANode tag, string tagName = "")
    {
        const string pattern = @"{(.*?)}";

        // Replace "**" in items with the actual tag name
        if (items.Contains("**"))
        {
            items = items.Replace("**", tagName);
        }

        IUAVariable confirmItem;

        // Determine whether items contains placeholders like {value}
        bool hasPlaceholders = Regex.IsMatch(items, @".*\{.*\}.*");

        if (hasPlaceholders)
        {
            //If items contains placeholders, create a String-type variable
            confirmItem = InformationModel.MakeVariable(variableName, OpcUa.DataTypes.String);
            tagChildren.Find("AuditSigning Signature").Children.Add(confirmItem);

            // Create a StringFormatter to dynamically format the value
            var stringFormatter = InformationModel.Make<StringFormatter>("StringFormatter1");
            var matches = Regex.Matches(items, pattern);

            for (int i = 0; i < matches.Count; i++)
            {
                string matchValue = matches[i].Groups[1].Value;
                UAManagedCore.UAVariable extendedTag = null;

                // Match strings starting with "@" but not containing "@Alarm"
                if (matchValue.StartsWith("@") && !matchValue.Contains("@Alarm"))
                {
                    variableName = matchValue.Substring(1);
                    extendedTag = (UAManagedCore.UAVariable)tag.Find(variableName);
                }
                // Match strings containing "@" but excluding "@Alarm", "CmdSrc", and "**"
                else if (matchValue.Contains("@") && !matchValue.Contains("@Alarm") && !matchValue.Contains("CmdSrc") && !matchValue.Contains("**"))
                {
                    string[] parts = matchValue.Split('@');
                    if (parts.Length >= 2)
                    {
                        string key = parts[1];
                        if (extendedTagProperties.ContainsKey(key))
                        {
                            string path = extendedTagProperties[key];
                            extendedTag = GetVariableByPath(tag, parts[0] + "/" + path);
                        }
                    }
                }
                // Match strings containing "@Alarm"
                else if (matchValue.Contains("@Alarm"))
                {
                    if (matchValue.Length >= 2)
                    {
                        extendedTag = GetVariableByPath(tag, matchValue);
                    }
                }
                // Match strings containing "CmdSrc"
                else if (matchValue.Contains("CmdSrc"))
                {
                    if (matchValue.Length >= 2)
                    {
                        matchValue = matchValue.Replace("@", "/");
                        extendedTag = GetVariableByPath(tag, matchValue);
                    }
                }
                // Default case: find variable directly under tag
                else
                {
                    extendedTag = (UAManagedCore.UAVariable)tag.Find(matchValue);
                }

                if (extendedTag != null)
                {
                    // Create a source variable and link it to the extended tag
                    var source = InformationModel.MakeVariable("Source" + i, OpcUa.DataTypes.BaseDataType);
                    stringFormatter.Refs.AddReference(FTOptix.CoreBase.ReferenceTypes.HasSource, source);
                    source.SetDynamicLink(extendedTag);

                    // Replace placeholder with index reference
                    string patterns = "{" + matchValue + "}";
                    string replacement = "{" + i.ToString() + "}";
                    items = items.Replace(patterns, replacement);
                }
                else
                {
                    // Log error and remove audSig if no matching tag is found
                    var NodeBrowserName = tag.BrowseName;
                    UAManagedCore.Log.Error("AuditSigning Configuration", confirmItem.BrowseName + " import failed. No matching tags found '" + NodeBrowserName + "." + matchValue + "'.");
                    tagChildren.Remove(audSig);
                    return;
                }
            }

            stringFormatter.Format = items;
            confirmItem.SetConverter(stringFormatter);
        }
        else
        {
            // If items does not contain placeholders, create a LocalizedText-type variable
            confirmItem = InformationModel.MakeVariable(variableName, OpcUa.DataTypes.LocalizedText);
            tagChildren.Find("AuditSigning Signature").Children.Add(confirmItem);

            // Set the value of confirmItem directly as LocalizedText
            int correctNameSpace = LogicObject.GetVariable("LocalizationDictionary").NodeId.NamespaceIndex;
            LocalizedText localizedText = new LocalizedText(correctNameSpace, items, "", "");
            confirmItem.Value = localizedText;
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
    private string GetFilePath()
    {
        var PathVariable = LogicObject.GetVariable("ImportFilePath");
        if (PathVariable == null)
        {
            UAManagedCore.Log.Error("AuditSigning Configuration", "ImportFilePath variable not found.");
            return null;
        }

        string[] SplittedPath = PathVariable.Value.ToString().Split('/');
        string filePath;
        if (SplittedPath.Length <= 1)
        {
            filePath = ResourceUri.FromProjectRelativePath(LogicObject.GetVariable("ImportFilePath").Value).Uri;
        }
        else
        {

            filePath = new ResourceUri(PathVariable.Value).Uri;
        }
        if (!File.Exists(filePath))
        {
            UAManagedCore.Log.Error("AuditSigning Configuration", "File not found: " + filePath);
            return null;
        }
        if (!filePath.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase) && !filePath.EndsWith(".xls", StringComparison.OrdinalIgnoreCase))
        {
            UAManagedCore.Log.Error("AuditSigning Configuration", "Incorrect import file format, only support .xlsx and .xls file type.");
            return null;

        }
        //check if the file is occupied
        FileStream stream = null;
        try
        {
            stream = new FileStream(filePath, FileMode.Open, FileAccess.ReadWrite);
        }

        catch (IOException)
        {
            //file is occupied
            UAManagedCore.Log.Error("AuditSigning Configuration", "File in use: " + filePath);
            return null;
        }
        finally
        {
            stream?.Close();
        }

        return filePath;
    }

    public List<Dictionary<string, string>> ConvertDataTableToDictionary(DataTable dt)
    {
        List<Dictionary<string, string>> list = new List<Dictionary<string, string>>();

        foreach (DataRow row in dt.Rows)
        {
            Dictionary<string, string> dict = new Dictionary<string, string>();
            foreach (DataColumn column in dt.Columns)
            {
                dict[column.ColumnName] = row[column].ToString()?.Trim() ?? "";
            }
            list.Add(dict);
        }
        return list;
    }

    public class ControlInfo
    {
        public string StationName { get; set; }
        public string FolderName { get; set; }
        public string DataType { get; set; }
        public string TagName { get; set; }
        public string DriverName { get; set; }

    }

    public (DataTable, List<ControlInfo>) GetMatchingRowsAndStationNames(string excelFilePath)
    {
        var controlInfoList = new List<ControlInfo>();
        var dt = new DataTable();

        try
        {
            using var file = new FileStream(excelFilePath, FileMode.Open, FileAccess.Read);
            IWorkbook workbook = null;
            if (excelFilePath.EndsWith(".xls"))
            {
                workbook = new HSSFWorkbook(file);
            }
            else if (excelFilePath.EndsWith(".xlsx"))
            {
                workbook = new XSSFWorkbook(file);
            }
            ISheet sheet1 = workbook.GetSheetAt(0);
            ISheet sheet2 = workbook.GetSheetAt(1);

            // Create DataTable column.
            var headerRow2 = sheet2.GetRow(0);
            foreach (var headerCell in headerRow2.Cells)
            {
                dt.Columns.Add(headerCell.ToString()?.Trim() ?? "");
            }

            // Get the index of the "E-Signature import", "DataType", "StationName", and "FolderName" columns
            var headerRow1 = sheet1.GetRow(0);
            int eSigIndex = headerRow1.Cells.FindIndex(cell => cell.StringCellValue?.Trim() == "E-Signature Import");
            int dataTypeIndex1 = headerRow1.Cells.FindIndex(cell => cell.StringCellValue?.Trim() == "DataType");
            int dataTypeIndex2 = headerRow2.Cells.FindIndex(cell => cell.StringCellValue?.Trim() == "DataType");
            int DriverNameIndex = headerRow1.Cells.FindIndex(cell => cell.StringCellValue?.Trim() == "DriverName");
            int StationNameIndex = headerRow1.Cells.FindIndex(cell => cell.StringCellValue?.Trim() == "StationName");
            int FolderIndex = headerRow1.Cells.FindIndex(cell => cell.StringCellValue?.Trim() == "FolderName");
            int tagNameIndex = headerRow1.Cells.FindIndex(cell => cell.StringCellValue?.Trim() == "TagName");

            //Check if the ValueMappingContent, ValueMappingType cell exists.
            int ValueMappingContent = headerRow2.Cells.FindIndex(cell => cell.StringCellValue?.Trim() == "ValueMappingContent");
            int ValueMappingType = headerRow2.Cells.FindIndex(cell => cell.StringCellValue?.Trim() == "ValueMappingType");
            {
                if (ValueMappingContent == -1 || ValueMappingType == -1)
                {
                    throw new Exception();

                }
            }
            // Create dict to store the rows form the first sheet where "E-Signature import" column is "Y".
            var dict = new Dictionary<string, IRow>();
            foreach (IRow row in Enumerable.Range(1, sheet1.LastRowNum).Select(i => sheet1.GetRow(i)))
            {
                var eSigCell = row.GetCell(eSigIndex);
                var dataTypeCell = row.GetCell(dataTypeIndex1);
                if (eSigCell != null && "Y".Equals(eSigCell.StringCellValue?.Trim()))
                {
                    var DriverNamecell = row.GetCell(DriverNameIndex);
                    var DriverName = DriverNamecell.StringCellValue?.Trim() ?? "";
                    var StationNamecell = row.GetCell(StationNameIndex);
                    var StationName = StationNamecell.StringCellValue?.Trim() ?? "";
                    var FolderIndexcell = row.GetCell(FolderIndex);
                    var FolderName = FolderIndexcell.StringCellValue?.Trim() ?? "";
                    var dataTypeValue = dataTypeCell.StringCellValue?.Trim() ?? "";
                    var tagNameValueCell = row.GetCell(tagNameIndex);
                    var tagNameValue = tagNameValueCell.StringCellValue?.Trim() ?? "";
                    dict[dataTypeValue] = row;
                    controlInfoList.Add(new ControlInfo { DriverName = DriverName, StationName = StationName, FolderName = FolderName, DataType = dataTypeValue, TagName = tagNameValue });
                }
            }

            // look for the row in the second sheet that matches the value of the "DataType" column.
            List<string> dataTypeValues = new List<string>();
            foreach (IRow row2 in Enumerable.Range(1, sheet2.LastRowNum).Select(j => sheet2.GetRow(j)))
            {
                if (row2 != null && row2.Cells.Any(cell => cell.CellType != CellType.Blank))
                {
                    var dataTypeCell2 = row2.GetCell(dataTypeIndex2);
                    if (dataTypeCell2 != null)
                    {
                        string dataTypeValue = dataTypeCell2.StringCellValue?.Trim() ?? "";
                        dataTypeValues.Add(dataTypeValue); // add datatype to list
                        var dr = dt.NewRow();
                        for (int k = 0; k < row2.LastCellNum; k++)
                        {
                            var cell = row2.GetCell(k);
                            if (cell != null)
                            {
                                dr[k] = cell.ToString()?.Trim() ?? "";
                            }
                        }
                        dt.Rows.Add(dr);
                    }
                }
            }


            foreach (var key in dict.Keys)
            {
                bool matchFound = dataTypeValues.Any(value => Regex.IsMatch(key, @"^" + Regex.Escape(value) + @"\d*$"));

                if (!matchFound)
                {
                    var rowsToDelete = controlInfoList.AsEnumerable().Where(r => r.DataType == key).ToList();
                    foreach (var row in rowsToDelete)
                    {
                        controlInfoList.Remove(row);
                    }

                    UAManagedCore.Log.Error("AuditSigning Configuration", "Incorrect import DataType '" + key + "' format.");
                }
            }
            //check the EsigWorkflowType column of dt
            int columnIndexToCheck = 2;

            // Create a list of rows to remove
            List<DataRow> rowsToRemove = new List<DataRow>();

            foreach (DataRow row in dt.Rows)
            {
                if (row.IsNull(columnIndexToCheck) || string.IsNullOrWhiteSpace(row[columnIndexToCheck].ToString()) || !validworkflowTypeValues.Contains(row[columnIndexToCheck].ToString()))
                {
                    rowsToRemove.Add(row);
                }
            }

            // Remove all marked rows
            foreach (DataRow row in rowsToRemove)
            {
                dt.Rows.Remove(row);
            }

            // Return dt and controlInfoList
            return (dt, controlInfoList);
        }
        catch (FileNotFoundException)
        {
            var ProjectPath = FindProjectFolderPath("ProjectFolder", (UAManagedCore.UAObject)Owner);
            UAManagedCore.Log.Error("AuditSigning Configuration", $"Could not find file \"ICSharpCode.SharpZipLib.dll\" under \"{ProjectPath}/NetSolution/bin\" folder.");
            throw;
        }
        catch
        {
            UAManagedCore.Log.Error("AuditSigning Configuration", "Incorrect import file format.");
            return (null, null);
        }
    }

    private string FindProjectFolderPath(string projectBrowserName, UAManagedCore.UAObject currentObject)
    {
        var pathObject = currentObject.ObjectType.BrowseName;
        if (pathObject == projectBrowserName)
        {
            return ((FTOptix.HMIProject.ProjectFolder)currentObject).ProjectDirectory;
        }
        else if (currentObject.Parent != null)
        {
            return FindProjectFolderPath(projectBrowserName, (UAManagedCore.UAObject)currentObject.Parent);
        }
        return null;
    }
    private void InitAuditSigning(IUANode node)
    {
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
                InitAuditSigning(item);
            }
        }
        // If the node type is RAEtherNetIPStationType, search the "Tags" folder
        else if (objectTypeBrowseName == RAEtherNetIPStationType)
        {
            var folder = node.Find<Folder>("Tags");
            foreach (var item in folder.Children)
            {
                InitAuditSigning(item);
            }
        }
        // If the node is a Folder, search its non-Folder children
        else if (node is Folder)
        {
            foreach (var item in node.Children)
            {
                if (item is not Folder)
                {
                    InitAuditSigning(item);
                }
            }
        }
        // If the node is TagStructure, recursively remove all "AuditSigning Signature" nodes
        else if (node is FTOptix.CommunicationDriver.TagStructure)
        {
            RemoveAuditSigning(node);
        }
    }

    // Recursive method to remove "AuditSigning Signature" nodes
    private void RemoveAuditSigning(IUANode node)
    {
        foreach (var item in node.Children.ToList()) // 
        {
            if (item.BrowseName == "AuditSigning Signature")
            {
                node.Remove(item);
            }
            else
            {
                RemoveAuditSigning(item); // Recursively check child nodes
            }
        }
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

}
