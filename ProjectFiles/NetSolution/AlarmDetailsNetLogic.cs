#region Using directives
using System;
using UAManagedCore;
using OpcUa = UAManagedCore.OpcUa;
using FTOptix.UI;
using FTOptix.HMIProject;
using FTOptix.CoreBase;
using FTOptix.System;
using FTOptix.NativeUI;
using FTOptix.OPCUAServer;
using FTOptix.RAEtherNetIP;
using FTOptix.NetLogic;
using FTOptix.Retentivity;
using FTOptix.CommunicationDriver;
using FTOptix.AuditSigning;
using FTOptix.EventLogger;
using FTOptix.SQLiteStore;
using FTOptix.Store;
using FTOptix.Core;
using System.IO;
using FTOptix.InfluxDBStore;
using FTOptix.InfluxDBStoreLocal;
using FTOptix.SerialPort;
#endregion

public class AlarmDetailsNetLogic : BaseNetLogic
{
    public override void Start()
    {
        // Insert code to be executed when the user-defined logic is started
    }

    public override void Stop()
    {
        // Insert code to be executed when the user-defined logic is stopped
    }

    [ExportMethod]
    public void GetAlarmDetailsFileName()
    {
        IUAObject launchObj = null;
        //Get Dialog nodeid
        var diagName = Owner.GetVariable("AlarmDetailsDiag").Value;
        var diagObj = InformationModel.Get(diagName);

        //Get the Alias Node to determine the Launch Object
        var aliasNode = Owner.Owner.GetAlias("raSDK1_DialogBox");
        launchObj = InformationModel.GetObject(aliasNode.NodeId);

        //Get the Ref_Tag from the Alias
        var refTag = InformationModel.Get(launchObj.GetVariable("Ref_Tag").Value);

        //Get Alarm name
        string alarmName = Owner.GetVariable("AlarmName").Value;

        //Get File name
        string fileName = refTag.BrowseName + "_" + alarmName + ".html";
        var fileN = diagObj.FindVariable("_FileName");
        fileN.Value = refTag.BrowseName + "_" + alarmName;

        // Locate the Alarm Details files path
        string projectPath = Project.Current.ProjectDirectory;
        string filePath = projectPath + "/res/AlarmDetails";
        string _urlPath = filePath + "/" + fileName;
        var urlP = diagObj.FindVariable("_URL_Path");
        urlP.Value = _urlPath;

        // Judge the Alarm Details files exist or not.
        bool fileFound = false;
        if (Path.Exists(filePath))
        {
            string[] files = Directory.GetFiles(filePath);
            foreach (string file in files)
            {
                if (Path.GetFileName(file).Equals(fileName, StringComparison.OrdinalIgnoreCase))
                {
                    fileFound = true;

                    break;
                }
            }
        }
        var fileExist = diagObj.FindVariable("_FileExist");
        fileExist.Value = fileFound;
    }
}
