using FTOptix.HMIProject;
using FTOptix.NetLogic;
using FTOptix.UI;
using System.Collections.Generic;
using System;
using UAManagedCore;
using System.Linq;
using FTOptix.DataLogger;
using FTOptix.InfluxDBStore;
using FTOptix.InfluxDBStoreLocal;
using FTOptix.SerialPort;
using OpcUa = UAManagedCore.OpcUa;

public class raP_5_20_ParRpt_NL : BaseNetLogic
{
    public override void Start()
    {
        // Start method is now optional, main logic moved to exported method
    }

    public override void Stop()
    {
    }
    [ExportMethod]
    public void CreateBanks()
    {
        string BasePath = "";
        IUAObject launchObj = null;
        const string NUMPARS_TAGNAME = "Cfg_NumPars";
        const string NUMRPTS_TAGNAME = "Cfg_NumRpts";

        try
        {
            var aliasNode = Owner.Owner?.GetAlias("raSDK1_DialogBox");
            launchObj = InformationModel.GetObject(aliasNode.NodeId);
            var refTag = InformationModel.Get(launchObj.GetVariable("Ref_Tag").Value);
            string topContainer = "";
            string refTagBrowsePath = GetOptixPathByNode(refTag, topContainer);
            string tag_0_BrowsePath = "CommDrivers" + refTagBrowsePath.Split("CommDrivers")[1];
            BasePath = tag_0_BrowsePath;
            var Base_Tag = Project.Current.Get(BasePath);
            launchObj.GetVariable("Ref_Tag").Value = Base_Tag.NodeId;

            var numParsCounts = Base_Tag.Children.GetVariable(NUMPARS_TAGNAME).RemoteRead().Value;
            var numRptCounts = Base_Tag.Children.GetVariable(NUMRPTS_TAGNAME).RemoteRead().Value;

            for (int i = 0; i < Convert.ToInt32(numParsCounts); i++)
            {
                AddToLaunchObject(launchObj, "PAR", BasePath, i.ToString());
            }

            for (int i = 0; i < Convert.ToInt32(numRptCounts); i++)
            {
                AddToLaunchObject(launchObj, "RPT", BasePath, i.ToString());
            }

            var navPanel = Owner.Owner?.Find("NavigationPanel") as NavigationPanel;
            navPanel?.ChangePanelByTabName("Home");
        }
        catch (Exception ex)
        {
            if (launchObj == null)
                Log.Error("ParRptCreateBanks", $"Launch object error: {ex.Message}");
            else if (string.IsNullOrEmpty(BasePath))
                Log.Error("ParRptCreateBanks", $"Base path error: {ex.Message}");
            else
                Log.Error("ParRptCreateBanks", $"Unhandled error: {ex.Message}");
        }
    }

    private void AddToLaunchObject(IUAObject launchObj, string tagType, string basePath, string idx)
    {
        try
        {
            if (int.TryParse(idx, out int idxInt) && idxInt < 10)
                idx = idxInt.ToString("00");

            var tagPath = $"{basePath}_{tagType.ToUpper()}_{idx}";
            IUANode logixBankTag = Project.Current.Get(tagPath);
            if (logixBankTag == null)
            {
                Log.Warning("ParRptCreateBanks", $"Tag not found: {tagPath}");
                return;
            }

            IUAVariable newBankVar = InformationModel.MakeVariable($"Ref_Tag_{tagType.ToUpper()}_{idx}", OpcUa.DataTypes.NodeId);
            newBankVar.Value = logixBankTag.NodeId;
            launchObj.Add(newBankVar);

            var objTypeVar = logixBankTag.Children.GetVariable("Sts_eObjType");
            if (objTypeVar == null) return;

            var objType = objTypeVar.RemoteRead().Value;
            if (Convert.ToInt32(objType) == 4)
            {
                var enumPath = $"{tagPath}_Cfg_Enum";
                IUANode logixBankTags = Project.Current.Get(enumPath);
                if (logixBankTags != null)
                {
                    IUAVariable newBankVars = InformationModel.MakeVariable($"Ref_Tag_{tagType.ToUpper()}_{idx}_Cfg_Enum", OpcUa.DataTypes.NodeId);
                    newBankVars.Value = logixBankTags.NodeId;
                    launchObj.Add(newBankVars);
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error("ParRptCreateBanks", $"Failed to add {tagType}_{idx}: {ex.Message}");
        }
    }

    private string GetOptixPathByNode(IUANode inputNode, string topContainer)
    {
        List<string> pathToVar = new List<string>();

        void FindBrowsePath(IUANode node)
        {
            if (node?.Owner != null)
            {
                if (node.BrowseName == topContainer)
                    return;

                pathToVar.Add(node.BrowseName);
                FindBrowsePath(node.Owner);
            }
        }

        string ConstructBrowsePath()
        {
            string outStr = topContainer;
            for (int i = pathToVar.Count - 1; i >= 0; i--)
            {
                outStr += "/" + pathToVar[i];
            }
            return outStr;
        }

        FindBrowsePath(inputNode);
        return pathToVar.Count > 0 ? ConstructBrowsePath() : null;
    }
}
