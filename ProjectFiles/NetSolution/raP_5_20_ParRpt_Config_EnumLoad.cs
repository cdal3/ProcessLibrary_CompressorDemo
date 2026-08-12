#region Using directives
using System;
using UAManagedCore;
using FTOptix.UI;
using FTOptix.HMIProject;
using FTOptix.Core;
using FTOptix.NetLogic;
using FTOptix.DataLogger;
using OpcUa = UAManagedCore.OpcUa;
using System.Runtime.Serialization;
using FTOptix.InfluxDBStore;
using FTOptix.InfluxDBStoreLocal;
using FTOptix.SerialPort;
#endregion

public class raP_5_20_ParRpt_Config_EnumLoad : BaseNetLogic
{
    const string SET_PAGE_VAR_NAME = "Set_Page";

    public override void Start()
    {
        // Start method is now optional, main logic moved to exported method
    }

    public override void Stop()
    {

    }

    [ExportMethod]
    public void AddEnum()
    {
        IUANode unifiedTypeNode = Project.Current.Find("raP_5_20_ParRpt_Cfg_ParRptConfig_Enum");

        if (unifiedTypeNode == null)
        {
            Log.Error("Home Initialization", "Config type node not found.");
            return;
        }

        CreateInstances("PAR_", 50, 0, unifiedTypeNode);
        CreateInstances("RPT_", 50, 1, unifiedTypeNode);

    }

    private void CreateInstances(string prefix, int count, int pageValue, IUANode templateNode)
    {
        for (int i = 0; i < count; i++)
        {
            try
            {
                IUAObject newInstance = InformationModel.MakeObject("raP_5_20_ParRpt_Cfg_ParRptConfig_Enum" + i, templateNode.NodeId);
                
                newInstance.GetVariable("Cfg_Label").Value = i;
                newInstance.GetVariable(SET_PAGE_VAR_NAME).Value = pageValue;

                Owner.Add(newInstance);
            }
            catch (Exception ex)
            {
                Log.Error("Home Initialization", $"Failed to create {prefix}{i:00}: {ex.Message}");
            }
        }
    }

}
