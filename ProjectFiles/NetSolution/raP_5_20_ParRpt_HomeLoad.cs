#region Using directives
using System;
using UAManagedCore;
using FTOptix.UI;
using FTOptix.HMIProject;
using FTOptix.Core;
using FTOptix.NetLogic;
using FTOptix.DataLogger;
using FTOptix.InfluxDBStore;
using FTOptix.InfluxDBStoreLocal;
using FTOptix.SerialPort;
using OpcUa = UAManagedCore.OpcUa;
#endregion

public class raP_5_20_ParRpt_HomeLoad : BaseNetLogic
{
    private DelayedTask myDelayedTask;
    const string USER_SECURITYCODE = "User_SecurityCode";
    const string NUMPARS_PARTAGNAME = "Cfg_NumPars";
    const string NUMRPTS_RPTTAGNAME = "Cfg_NumRpts";
    const string BANK_ID_TAG_NAME = "Set_BankId";
    const string VISIBLE_PAGE_VAR_NAME = "_VisiblePageNumber";
    const string SET_PAGE_VAR_NAME = "Set_Page";
    const string IS_LOADING_VAR_NAME = "_IsLoading";

    const int itemsPerPage = 16;

    public override void Start()
    {
        // Start method is now optional, main logic moved to exported method
    }

    public override void Stop()
    {
        myDelayedTask?.Dispose();
    }

    [ExportMethod]
    public void Initialize()
    {
        try
        {
            var user = GetCurrentUser();
            string userType = GetUserType(user);
            if (!string.IsNullOrEmpty(userType))
            {
                Owner.GetVariable(USER_SECURITYCODE).Value = "%" + userType + "%";
            }

            myDelayedTask = new DelayedTask(BuildTypes, 2000, LogicObject);
            myDelayedTask.Start();
        }
        catch (Exception ex)
        {
            Log.Error("Home Initialization", $"Initialization failed: {ex.Message}");
        }
    }

    [ExportMethod]
    public void RefreshUserSecurity()
    {
        try
        {
            var user = GetCurrentUser();
            string userType = GetUserType(user);
            if (!string.IsNullOrEmpty(userType))
            {
                Owner.GetVariable(USER_SECURITYCODE).Value = "%" + userType + "%";
                Log.Info("Home Initialization", $"User security updated to: {userType}");
            }
            else
            {
                Log.Warning("Home Initialization", "No valid user type found");
            }
        }
        catch (Exception ex)
        {
            Log.Error("Home Initialization", $"Failed to refresh user security: {ex.Message}");
        }
    }


    private void BuildTypes()
    {
        try
        {
            int numPars = Owner.GetVariable(NUMPARS_PARTAGNAME).Value;
            int numRpts = Owner.GetVariable(NUMRPTS_RPTTAGNAME).Value;

            // Set_Page (0=PAR, 1=RPT)
            var setPageVar = Owner.GetVariable(SET_PAGE_VAR_NAME);
            if (setPageVar == null)
            {
                Log.Error("Home Initialization", "Set_Page variable not found on Owner (VerticalLayout1).");
                return;
            }
            int setPage = setPageVar.Value;

            CreateAllParRptInstances(numPars, numRpts, setPage);
        }
        catch (Exception ex)
        {
            Log.Error("Home Initialization", $"Failed to build types: {ex.Message}");
        }
    }

    // Alternately load Par and Rpt.
    private void CreateAllParRptInstances(int numPars, int numRpts, int setPage)
    {
        IUANode unifiedTypeNode = Project.Current.Find("raP_5_20_ParRpt_Grp_ParRptUnified");
        var setLoading = Owner.GetVariable(IS_LOADING_VAR_NAME);

        if (unifiedTypeNode == null)
        {
            Log.Error("Home Initialization", "Unified type node not found.");
            return;
        }


        if ((setPage == 0 && numPars == 0) || (setPage == 1 && numRpts == 0))
        {
            setLoading.Value = false;
            return;
        }


        int max = Math.Max(numPars, numRpts);
        for (int i = 0; i < max; i++)
        {
            if (i < numPars)
            {
                CreateInstances("PAR_", i, /*pageValue*/ 0, unifiedTypeNode);
            }

            if (i < numRpts)
            {
                CreateInstances("RPT_", i, /*pageValue*/ 1, unifiedTypeNode);
            }
        }
        setLoading.Value = false;
    }

    // Create a single instance.
    private void CreateInstances(string prefix, int idx, int pageValue, IUANode templateNode)
    {
        try
        {
            IUAObject newInstance = InformationModel.MakeObject(prefix + idx.ToString("00"), templateNode.NodeId);
            newInstance.GetVariable(BANK_ID_TAG_NAME).Value = idx.ToString("00");
            newInstance.GetVariable(VISIBLE_PAGE_VAR_NAME).Value = (idx / itemsPerPage) + 1;
            newInstance.GetVariable(SET_PAGE_VAR_NAME).Value = pageValue;

            Owner.Add(newInstance);
        }
        catch (Exception ex)
        {
            Log.Error("Home Initialization", $"Failed to create {prefix}{idx:00}: {ex.Message}");
        }
    }


    private FTOptix.Core.User GetCurrentUser()
    {
        return Session.User;
    }

    private string GetUserType(FTOptix.Core.User user)
    {
        if (user == null) return "";

        return user.ToString() switch
        {
            "OperatorType" => "A",
            "OperatingSupervisorType" => "B",
            "MaintenanceType" => "C",
            "MaintenanceSupervisorType" => "D",
            "EngineerType" => "E",
            "ManagerType" => "F",
            "AdministratorType" => "G",
            _ => ""
        };
    }
}
