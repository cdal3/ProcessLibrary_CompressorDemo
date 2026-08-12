#region Using directives
using System;
using UAManagedCore;
using OpcUa = UAManagedCore.OpcUa;
using FTOptix.UI;
using FTOptix.DataLogger;
using FTOptix.HMIProject;
using FTOptix.CoreBase;
using FTOptix.System;
using FTOptix.NativeUI;
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
using FTOptix.InfluxDBStore;
using FTOptix.InfluxDBStoreLocal;
using FTOptix.SerialPort;
#endregion

public class raSDK1_NL_NavExplicitName : BaseNetLogic
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
    public void NavByExplicitName()
    {
        //Define nodes used
        IUAObject lButton = null;
        IUAObject launchAliasObj = null;
        DialogType dBFromString = null;

        var diagName = Owner.Owner.GetVariable("Cfg_DialogBoxName");

        // Get the dialogbox name then find the Faceplate
        try
        {
            var foundFp = Project.Current.Find(diagName.Value);
            if (foundFp == null)
            {
                Log.Warning(this.GetType().Name, "Dialog Box '" + diagName + "' not found '");
                return;
            }

            // if found is DialogType, than it is a faceplate type
            if (foundFp.GetType() == typeof(DialogType))
            {
                dBFromString = (DialogType)foundFp;
            }
            else // found current instance of faceplate
            {
                // Get faceplate type from instance
                System.Reflection.PropertyInfo objType = foundFp.GetType().GetProperty("ObjectType");
                dBFromString = (DialogType)(objType.GetValue(foundFp, null));
            }

        }
        catch (Exception)
        {
            Log.Warning(this.GetType().Name, "Dialog box '" + diagName + "' not found");
            return;
        }

        try
        {
            // Get button object
            lButton = Owner.Owner.GetObject(this.Owner.BrowseName);
            // Make Launch Object that will contain aliases
            launchAliasObj = InformationModel.MakeObject("LaunchAliasObj");
        }
        catch
        {
            Log.Warning(this.GetType().Name, "Error getting owner object");
            return;
        }

        // Get each alias from Launch Button and add them into Launch Object, and assign NodeId values 
        foreach (var inpTag in lButton.Children)
        {
            if (inpTag.BrowseName.Contains("Ref_"))  // & !inpTag.BrowseName.Contains("Ref_DialogBox") & (inpTag.GetType() == typeof(UAVariable)))
            {
                // Make a variable with same name as alias of type NodeId
                var newVar = InformationModel.MakeVariable(inpTag.BrowseName, OpcUa.DataTypes.NodeId);
                try
                {
                    // Assign alias value to new variable
                    newVar.Value = ((UAManagedCore.UAVariable)inpTag).Value;
                }
                catch
                {
                    //If no value is assigned to a Ref_ input, annunciate that it is missing a node assignment
                    Log.Warning(this.GetType().Name, "Missing node assignment to variable: " + inpTag.BrowseName);
                }

                // Add variable int launch object
                launchAliasObj.Add(newVar);
            }
        }

        // Launch the faceplate
        try
        {
            // Launch DialogBox passing Launch Object that contains the aliases as an alias 
            UICommands.OpenDialog(lButton, dBFromString, launchAliasObj.NodeId);
        }
        catch
        {
            Log.Warning(this.GetType().Name, "Failed to launch dialog box specified by Cfg_DialogBox '" + dBFromString.BrowseName + "'");
            return;
        }



        // If configured, close the dialog box containing launch button
        try
        {
            bool cfgCloseCurrent = lButton.GetVariable("Cfg_CloseCurrentDisplay").Value;
            if (cfgCloseCurrent)
            {
                CloseCurrentDB(Owner);
            }
        }
        catch
        {
            Log.Warning(this.GetType().Name, "Failed to close current dialog box");
        }
    }

    public void CloseCurrentDB(IUANode inputNode)
    {
        // if input node is of type Dialog, close it
        if (inputNode is Dialog)
        {
            // close dialog box
            ((Dialog)inputNode).Close();
            return;
        }
        // if input node is Main Window, no dialog box was found, return
        if (inputNode is Window)
        {
            return;
        }
        // continue search for Dialog or Main Window
        CloseCurrentDB(inputNode.Owner);
    }


}
