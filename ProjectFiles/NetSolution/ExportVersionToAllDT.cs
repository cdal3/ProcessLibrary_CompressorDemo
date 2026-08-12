#region Using directives
using System;
using UAManagedCore;
using OpcUa = UAManagedCore.OpcUa;
using FTOptix.RAEtherNetIP;
using FTOptix.HMIProject;
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
using System.Collections.Generic;
using System.Linq;
using System.ComponentModel;
using System.IO;
using System.Threading.Tasks;
using System.Text;
using System.Globalization;
using FTOptix.SQLiteStore;
using FTOptix.System;
using System.Security.AccessControl;
using FTOptix.DataLogger;
using FTOptix.InfluxDBStore;
using FTOptix.InfluxDBStoreLocal;
using FTOptix.SerialPort;
#endregion

public class ExportVersionToAllDT : BaseNetLogic
{
    public const string ObjectNamePrefix = "raP_5_20_";

    [ExportMethod]
    public void ExportVersionToAll()
    {
        // Insert code to be executed by the method
        var findMappingPath = LogicObject.FindVariable("FindMappingPath").Value;
        var MappingPath = Project.Current.Find(findMappingPath);
        var dataMappings = LoadVersionMapping(MappingPath);
        var headers = new List<string>() { "BrowserName", "ObjectVersion", "Path", "NodeId(Don't update data.)" };
        var filePath = LogicObject.GetVariable("CSVPath").Value;
        var fileName = findMappingPath + ".csv";
        WriteCSV(filePath, fileName, headers, dataMappings);
    }

    public static List<dataMapping> LoadVersionMapping(IUANode MappingPath)
    {
        List<dataMapping> dataMappings = new List<dataMapping>();
        RecursiveSearch(MappingPath, dataMappings);
        return dataMappings;
    }

    private static void RecursiveSearch(IUANode findMapping, List<dataMapping> dataMappings)
    {
        foreach (var child in findMapping.Children)
        {
            if (child.GetType() != typeof(Folder))
            {
                var objectValue = child.GetVariable("Cfg_ObjectVersion");
                if (objectValue != null && child.BrowseName.Contains(ObjectNamePrefix))
                {
                    dataMapping dataMapping = new dataMapping();
                    dataMapping.BrowserName = ((UAManagedCore.UANode)child).BrowseName;
                    dataMapping.ObjectVersion = objectValue.Value;
                    var Path = GetOptixPathByNode(child, "UI");
                    dataMapping.Path = Path;
                    dataMapping.NodeId = ((UAManagedCore.UANode)objectValue).NodeId.ToString();
                    dataMappings.Add(dataMapping);
                }
            }
            else if (child.Children.Count > 0)
            {
                RecursiveSearch((Folder)child, dataMappings);
            }
        }
    }

    public static string GetOptixPathByNode(IUANode inputNode, string topContainer)
    {
        List<string> pathToVar = new List<string>();

        FindBrowsePath(inputNode);
        if (pathToVar.Count > 0)
        {
            var Path = ConstructBrowsePath();
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

    public class dataMapping
    {
        public string BrowserName { get; set; }
        public string ObjectVersion { get; set; }
        public string Path { get; set; }
        public string NodeId { get; set; }

    }

    public static void WriteCSV(string directory, string fileName, List<string> headers, List<dataMapping> content)
    {
        if (!Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }
        var filePath = directory + "\\" + fileName;
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
        }

        FileStream fs = new FileStream(filePath, FileMode.Create, FileAccess.Write);

        StreamWriter sw = new StreamWriter(fs);

        StringBuilder sb = new StringBuilder();

        for (int i = 0; i < headers.Count; i++)
        {
            string header = headers[i];
            sb.Append(header);
            if (i < headers.Count - 1)
            {
                sb.Append(",");
            }
        }
        sw.WriteLine(sb);

        foreach (dataMapping tm in content)
        {
            StringBuilder sbd = new StringBuilder();
            sbd.Append(tm.BrowserName).Append(",").Append(tm.ObjectVersion).Append(",").Append(tm.Path).Append(",").Append(tm.NodeId);
            sw.WriteLine(sbd);
        }
        sw.Flush();
        sw.Close();
        fs.Close();
    }
}

