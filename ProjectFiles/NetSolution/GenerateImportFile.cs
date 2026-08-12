#region Using directives
using System;
using UAManagedCore;
using OpcUa = UAManagedCore.OpcUa;
using FTOptix.HMIProject;
using FTOptix.Retentivity;
using FTOptix.UI;
using FTOptix.NativeUI;
using FTOptix.CoreBase;
using FTOptix.NetLogic;
using FTOptix.Core;
using FTOptix.RAEtherNetIP;
using FTOptix.CommunicationDriver;
using FTOptix.AuditSigning;
using System.Collections.Generic;
using Microsoft.VisualBasic.FileIO;
using System.Linq;
using System.Text.RegularExpressions;
using System.IO;
using System.Data;
using NPOI.XSSF.UserModel;
using static ImportConfigFile;
using System.ComponentModel.DataAnnotations;
using FTOptix.SQLiteStore;
using FTOptix.System;
using NPOI.HSSF.UserModel;
using NPOI.SS.UserModel;
using NPOI.HSSF.Util;
using FTOptix.DataLogger;
using FTOptix.InfluxDBStore;
using FTOptix.InfluxDBStoreLocal;
using FTOptix.SerialPort;
#endregion

public class GenerateImportFile : BaseNetLogic
{
    private string exportExcelName = "E-Signature";
    private readonly string exportRelativePath = "/res/ESignature/";
    private readonly string[] summaryColumnNames = { "DriverName", "StationName", "FolderName", "TagName", "DataType", "E-Signature Import", "Note" };
    private const string CommDriversCategoryFolderType = "CommDriversCategoryFolder";
    private const string RAEtherNetIPDriverType = "RAEtherNetIPDriver";
    private const string RAEtherNetIPStationType = "RAEtherNetIPStation";
    private readonly string[] TemplateColumnNames = { "DataType", "TagMember", "EsigWorkflowType", "Caption", "Statement", "SignApproverGroup", "ValueMappingContent", "ValueMappingType" };
    private LongRunningTask myLongRunningTask;


    [ExportMethod]
    public void InitDB()
    {
        myLongRunningTask = new LongRunningTask(InitDBTask, LogicObject);
        myLongRunningTask.Start();
    }
    public void InitDBTask(LongRunningTask task)
    {

        // Get file path for EsigInitDataPath variable
        string esigExInitPath = GetFilePath();
        // Get Esig DataTable
        if (esigExInitPath != null)
        {
            DataTable esigExInitDT = ImportExcelFile(esigExInitPath);
            if (esigExInitDT != null)
            {
                // Get communication drivers of the current project
                var commDrivers = Project.Current.Get("CommDrivers");
                // Get list of dataTypes of rows in EsigExInitDT
                var dataTypeList = GetEsigExInitDTRows(esigExInitDT);
                if (dataTypeList == null)
                {
                    UAManagedCore.Log.Error("AuditSigning Configuration", "Incorrect template format.");
                }
                else if (dataTypeList.Count > 0)
                {
                    // Search for tags
                    var tags = SearchTag(commDrivers, esigExInitDT, dataTypeList);
                    // Get system time
                    if (tags.Count > 0)
                    {
                        DateTime now = DateTime.Now;
                        string timestamp = now.ToString("yyyyMMddHHmmss");
                        exportExcelName = exportExcelName + "_" + timestamp + ".xlsx";
                        // Get path to export Excel
                        var ProjectPath = FindProjectFolderPath("ProjectFolder", (UAManagedCore.UAObject)Owner);
                        string exportExcelPath = ProjectPath + exportRelativePath + exportExcelName;
                        // Export tags
                        ExportTags(tags, esigExInitDT, exportExcelPath);
                        UAManagedCore.Log.Info("AuditSigning Configuration", "Tags exported successfully, export file path:" + exportExcelPath + ".");
                    }
                    else
                    {
                        UAManagedCore.Log.Error("AuditSigning Configuration", "Export faild. No tags found.");
                    }
                }
                else
                {
                    UAManagedCore.Log.Error("AuditSigning Configuration", "Incorrect template format.");
                }
            }
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

    private string GetFilePath()
    {
        var PathVariable = LogicObject.GetVariable("TemplateFilePath");
        if (PathVariable == null)
        {
            UAManagedCore.Log.Error("AuditSigning Configuration", "TemplateFilePath variable not found.");
            return null;
        }

        string[] SplittedPath = PathVariable.Value.ToString().Split('/');
        string filePath;
        if (SplittedPath.Length <= 1)
        {
            filePath = ResourceUri.FromProjectRelativePath(LogicObject.GetVariable("TemplateFilePath").Value).Uri;
        }
        else
        {

            filePath = new ResourceUri(PathVariable.Value).Uri;
        }
        if (!File.Exists(filePath))
        {
            UAManagedCore.Log.Error("AuditSigning Configuration", "Template file not found: " + filePath);
            return null;
        }
        if (!filePath.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase) && !filePath.EndsWith(".xls", StringComparison.OrdinalIgnoreCase))
        {
            UAManagedCore.Log.Error("AuditSigning Configuration", "Incorrect template format, only support .xlsx and .xls file type.");
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
    private List<string> GetEsigExInitDTRows(DataTable esigExInitDT)
    {
        var dataTypeList = new List<string>();
        for (int i = 0; i < esigExInitDT.Rows.Count; i++)
        {
            bool containsAllColumns = true;
            foreach (var columnName in TemplateColumnNames)
            {
                bool columnExists = false;
                foreach (DataColumn column in esigExInitDT.Columns)
                {
                    if (column.ColumnName.Equals(columnName, StringComparison.Ordinal))
                    {
                        columnExists = true;
                        break;
                    }
                }
                if (!columnExists)
                {
                    containsAllColumns = false;
                    break;
                }
            }
            if (containsAllColumns)
            {
                dataTypeList.Add(esigExInitDT.Rows[i]["DataType"].ToString());
            }
            else
            {
                return null;
            }
        }
        return dataTypeList;
    }
    private List<string[]> SearchTag(IUANode node, DataTable esigExInitDT, List<string> dataTypeList)
    {
        // Initialize a list to store the tags
        List<string[]> tags = new List<string[]>();
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
                tags.AddRange(SearchTag(item, esigExInitDT, dataTypeList));
            }
        }
        // If the node type is RAEtherNetIPStationType, search the "Tags" folder
        else if (objectTypeBrowseName == RAEtherNetIPStationType)
        {
            var folder = node.Find<Folder>("Tags");
            foreach (var item in folder.Children)
            {
                tags.AddRange(SearchTag(item, esigExInitDT, dataTypeList));
            }
        }
        // If the node is a Folder, search its non-Folder children
        else if (node is Folder)
        {
            foreach (var item in node.Children)
            {
                if (item is not Folder)
                {
                    tags.AddRange(SearchTag(item, esigExInitDT, dataTypeList));
                }
            }
        }
        // For other types of nodes, extract the control name and control folder name from the path
        else
        {
            string[] tagTypes = new string[7];
            var inputPath = FindPath(node).TrimEnd('/');
            string pattern = @".*/(.*?)/(.*?)/Tags/(.*?)/.*";
            Match match = Regex.Match(inputPath, pattern);
            if (match.Success)
            {
                string dirverName = match.Groups[1].Value;
                string StationName = match.Groups[2].Value;
                string controlFolderName = match.Groups[3].Value;
                tagTypes[0] = dirverName;
                tagTypes[1] = StationName;
                tagTypes[2] = controlFolderName;

            }

            tagTypes[3] = node.BrowseName;
            // extract the prefix part ofobjectTypeBrowseName 
            tagTypes[4] = objectTypeBrowseName;

            // Check if the prefix is in the dataTypeList
            if (dataTypeList.Any(item => Regex.IsMatch(objectTypeBrowseName, $"^{item}\\d*$")))
            {
                tagTypes[5] = "Y";
                tagTypes[6] = "";
            }
            else
            {
                tagTypes[5] = "N";
                tagTypes[6] = "The DateType is not in E-Signature Template.";
            }

            tags.Add(tagTypes);
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

    #region  

    private DataTable ImportExcelFile(string filePath)
    {
        try
        {
            using (FileStream file = new FileStream(filePath, FileMode.Open, FileAccess.Read))
            {
                IWorkbook workbook = null;
                if (filePath.EndsWith(".xls"))
                {
                    workbook = new HSSFWorkbook(file);
                }
                else if (filePath.EndsWith(".xlsx"))
                {
                    workbook = new XSSFWorkbook(file);
                }
                ISheet sheet = workbook.GetSheetAt(0);
                System.Collections.IEnumerator rows = sheet.GetRowEnumerator();
                DataTable dt = new DataTable();
                var cells = sheet.GetRow(0).Cells;
                for (int j = 0; j < (sheet.GetRow(0).LastCellNum); j++)
                {
                    string columnName = cells[j].StringCellValue?.Trim() ?? "";
                    dt.Columns.Add(columnName);
                }
                rows.MoveNext();
                while (rows.MoveNext())
                {
                    IRow row = (IRow)rows.Current;
                    DataRow dr = dt.NewRow();
                    for (int i = 0; i < row.LastCellNum; i++)
                    {
                        ICell cell = row.GetCell(i);
                        if (cell == null)
                        {
                            dr[i] = null;
                        }
                        else
                        {
                            dr[i] = cell.ToString()?.Trim();
                        }
                    }
                    dt.Rows.Add(dr);
                }
                return dt;
            }
        }
        catch (FileNotFoundException)
        {
            var ProjectPath = FindProjectFolderPath("ProjectFolder", (UAManagedCore.UAObject)Owner);
            UAManagedCore.Log.Error("AuditSigning Configuration", $"Could not find file \"ICSharpCode.SharpZipLib.dll\" under \"{ProjectPath}/NetSolution/bin\" folder.");
            throw;
        }
        catch
        {
            UAManagedCore.Log.Error("AuditSigning Configuration", "Incorrect template format.");
            return null;
        }
    }
    #endregion

    private void ExportTags(List<string[]> tags, DataTable esigExInitDT, string filePath)
    {
        try
        {
            // Check if the input parameters are valid
            if (!string.IsNullOrEmpty(filePath) && null != esigExInitDT && esigExInitDT.Rows.Count > 0 && null != tags && tags.Count > 0)
            {
                // Create a new workbook
                XSSFWorkbook book = new XSSFWorkbook();

                // Create a sheet for summary information
                NPOI.SS.UserModel.ISheet summarySheet = book.CreateSheet("Summary");
                NPOI.SS.UserModel.IRow row = summarySheet.CreateRow(0);
                // Fill in the summary information
                for (int i = 0; i < summaryColumnNames.Length; i++)
                {
                    row.CreateCell(i).SetCellValue(summaryColumnNames[i]);
                }
                // Fill in the tag data
                for (int i = 0; i < tags.Count; i++)
                {
                    NPOI.SS.UserModel.IRow row2 = summarySheet.CreateRow(i + 1);
                    for (int j = 0; j < tags[i].Length; j++)
                    {
                        row2.CreateCell(j).SetCellValue(Convert.ToString(tags[i][j]));
                    }
                }

                // Create a sheet for detailed information
                NPOI.SS.UserModel.ISheet detailSheet = book.CreateSheet("Detail");
                row = detailSheet.CreateRow(0);
                int rowIndex = 1;
                // Fill in the column names
                for (int i = 0; i < esigExInitDT.Columns.Count; i++)
                {
                    row.CreateCell(i).SetCellValue(esigExInitDT.Columns[i].ColumnName);
                }

                // Create a cell style with yellow background
                ICellStyle yellowStyle = book.CreateCellStyle();
                yellowStyle.FillForegroundColor = HSSFColor.Yellow.Index;
                yellowStyle.FillPattern = FillPattern.SolidForeground;

                // Fill in the row data
                for (int i = 0; i < esigExInitDT.Rows.Count; i++)
                {
                    NPOI.SS.UserModel.IRow row2 = detailSheet.CreateRow(rowIndex++);
                    for (int j = 0; j < esigExInitDT.Columns.Count; j++)
                    {
                        ICell cell = row2.CreateCell(j);
                        cell.SetCellValue(Convert.ToString(esigExInitDT.Rows[i][j]));

                        // Check if the cell is in the second column and is empty
                        if (j == 2 && string.IsNullOrEmpty(Convert.ToString(esigExInitDT.Rows[i][j])))
                        {
                            cell.CellStyle = yellowStyle;
                        }
                    }
                }

                // Write the workbook to a file
                using (System.IO.MemoryStream ms = new System.IO.MemoryStream())
                {
                    book.Write(ms);
                    using (FileStream fs = new FileStream(filePath, FileMode.Create, FileAccess.Write))
                    {
                        byte[] data = ms.ToArray();
                        fs.Write(data, 0, data.Length);
                        fs.Flush();
                    }
                    book = null;
                }
            }
        }
        catch
        {
            UAManagedCore.Log.Error("AuditSigning Configuration", "Incorrect template format.");
        }
    

}


}
