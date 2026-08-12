#region Using directives
using System;
using UAManagedCore;
using OpcUa = UAManagedCore.OpcUa;
using FTOptix.HMIProject;
using FTOptix.NetLogic;
using FTOptix.UI;
using FTOptix.NativeUI;
using FTOptix.OPCUAServer;
using FTOptix.WebUI;
using FTOptix.RAEtherNetIP;
using FTOptix.Retentivity;
using FTOptix.CommunicationDriver;
using FTOptix.CoreBase;
using FTOptix.Core;
using FTOptix.SQLiteStore;
using FTOptix.DataLogger;
using FTOptix.InfluxDBStore;
using FTOptix.InfluxDBStoreLocal;
using FTOptix.SerialPort;
#endregion

/*
Dialog box navigation script.
***** Warning *****
DO NOT EDIT!  Edits to this script may cause dialog box navigation to fail.  

=============================================================

Disclaimer of Warranty
THE MATERIALS PROVIDED OR REFERENCED BY WAY OF THIS DOCUMENT ARE PROVIDED "AS IS" WITHOUT WARRANTIES OF ANY KIND, EITHER EXPRESS OR IMPLIED, INCLUDING WITHOUT LIMITATION, ALL IMPLIED WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE, NON-INFRINGEMENT OR OTHER VIOLATION OF RIGHTS. ROCKWELL AUTOMATION DOES NOT WARRANT OR MAKE ANY REPRESENTATIONS REGARDING THE USE, VALIDITY, ACCURACY, OR RELIABILITY OF, OR THE RESULTS OF ANY USE OF, OR OTHERWISE RESPECTING, THE MATERIALS PROVIDED OR REFERENCED BY WAY OF THIS DOCUMENT OR ANY WEB SITE LINKED TO THIS DOCUMENT 

Limitation of Liability
UNDER NO CIRCUMSTANCE (INCLUDING NEGLIGENCE AND TO THE FULLEST EXTEND PERMITTED BY APPLICABLE LAW) WILL ROCKWELL AUTOMATION BE LIABLE FOR ANY DIRECT, INDIRECT, SPECIAL, INCIDENTAL, PUNITIVE OR CONSEQUENTIAL DAMAGES (INCLUDING WITHOUT LIMITATION, BUSINESS INTERRUPTION, DELAYS, LOSS OF DATA OR PROFIT) ARISING OUT OF THE USE OR THE INABILITY TO USE THE MATERIALS PROVIDED OR REFERENCED BY WAY OF THIS DOCUMENT EVEN IF ROCKWELL AUTOMATION HAS BEEN ADVISED OF THE POSSIBILITY OF SUCH DAMAGES. IF USE OF SUCH MATERIALS RESULTS IN THE NEED FOR SERVICING, REPAIR OR CORRECTION OF USER EQUIPMENT OR DATA, USER ASSUMES ANY COSTS ASSOCIATED THEREWITH.

Copyright © Rockwell Automation, Inc.  All Rights Reserved. 

=============================================================
*/

public class raSDK1_NL_NavUsingString : BaseNetLogic
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

    public void NavString()
    {
        //constant for library and type location in Logix Controller
        const string libraryTag = "Inf_Lib";      // This will eventually be deleted when all libraries are using Extended Tag Properties
        const string libraryTypeTag = "Inf_Type"; // This will eventually be deleted when all libraries are using Extended Tag Properties
        const string library = "Library";
        const string libraryType = "Instruction";

        //Define strings for library and type to be read from Logix Controller
        string lib;
        string lType;
        string sourceMsg;

        //Define nodes used
        IUANode logixTag = null;
        IUAObject lButton = null;
        string lpath = "";
        string controllerShortcut = null;
        string tag = null;
        string container = null;
        DialogType dBFromString = null;
        string faceplateTypeName = null;


        try
        {
            // Get button object
            lButton = Owner.Owner.GetObject(this.Owner.BrowseName);
            // Get the value of tag that holds navigation path string
            lpath = lButton.GetVariable("Ref_Nav").Value;
        }
        catch
        {
            Log.Warning(this.GetType().Name, "Error retrieving Ref_Nav value");
            return;
        }

        // Get the logix tag specified by Ref_Nav
        if (lpath.Contains("["))  // Test to see if this address format is FTView or OPtix
        {
            try
            {
                // Convert the FTVIew style address to an Optix style address
                controllerShortcut = lpath.Split(']')[0].Split('[')[1];
                if (lpath.Contains('.'))
                {
                    tag = lpath.Split(']')[1].Split('.')[1];
                    container = lpath.Split(']')[1].Split('.')[0];
                }
                else
                {
                    tag = lpath.Split(']')[1];
                    container = lpath.Split(']')[1];
                    if (!lpath.Contains("Program:"))
                    {
                        container = "Controller Tags";
                    }
                }
                // Get CommDrivers folder in Optix
                var commDrivers = Project.Current.Find("CommDrivers");
                logixTag = commDrivers.Get("RAEtherNet_IPDriver1").Get(controllerShortcut).Get("Tags").Get(container).Get(tag);
            }
            catch
            {
                Log.Warning(this.GetType().Name, "Failed to get tag for FactoryTalk View style path " + lpath);
                return;
            }
        }
        else
        {
            try
            {
                logixTag = Project.Current.Get(lpath);
            }
            catch
            {
                Log.Warning(this.GetType().Name, "Failed to get tag for path '" + lpath + "'");
                return;
            }
        }


        // Make sure the Logix Tag is valid before continuing
        if ( logixTag == null )
        {
            Log.Warning(this.GetType().Name, "Failed to get tag for path '" + lpath + "'");
            return;
        }


        // Retrieve the display type
        string fpType;
        try
        {
            fpType = lButton.GetVariable("Cfg_DisplayType").Value;
        }
        catch
        {
            Log.Warning(this.GetType().Name, "Failed to read Optix variable 'Cfg_DisplayType'");
            return;
        }


        // From the Logix Tag, get the identity of the object
        try
        {
            // First try the extended tag properties
            lib = (string)logixTag.Children.GetVariable(library).Value;
            lType = (string)logixTag.Children.GetVariable(libraryType).Value;
            sourceMsg = "Check Extended Tag Properties '" + library + "' (" + lib + ") and '" + libraryType + "' (" + lType + ")";
        }
        catch
        {
            // The extended tag property was empty, try the Inf_Lib and Inf_Type tags - this will eventually be removed
            try
            {
                lib = (string)logixTag.Children.GetVariable(libraryTag).RemoteRead().Value;
                lType = (string)logixTag.Children.GetVariable(libraryTypeTag).RemoteRead().Value;
                sourceMsg = "Check tag members '" + libraryTag + "' (" + lib + ") and '" + libraryTypeTag + "' (" + lType + ")";
            }
            catch
            {
                Log.Warning(this.GetType().Name, "Failed to read identity tags for object '" + logixTag.BrowseName + "'. Object must contain Extended Tag Properties '" + library + "' and '" + libraryType + "' or tags '" + libraryTag + "' and '" + libraryTypeTag + "'");
                return;
            }
        }

        // Build the dialog box name and return the object
        try
        {
            faceplateTypeName = lib.Replace('-', '_') + '_' + lType + '_' + fpType;

            // Find DialogBox from assembled Faceplate string
            var foundFp = Project.Current.Find(faceplateTypeName);
            if (foundFp == null)
            {
                Log.Warning(this.GetType().Name, "Dialog Box '" + faceplateTypeName + "' not found for tag '" + logixTag.BrowseName + "'. " + sourceMsg);
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
        catch
        {
            Log.Warning(this.GetType().Name, "Error retrieving Dialog Box for tag '" + logixTag.BrowseName + "'. " + sourceMsg);
            return;
        }

        // Create the object that contains the alias and launch the faceplate
        try
        {
            // Make Launch Object that will contain aliases
            var launchAliasObj = InformationModel.MakeObject("LaunchAliasObj");
            // Make new variable "Ref_Tag"
            var newVar = InformationModel.MakeVariable("Ref_Tag", OpcUa.DataTypes.NodeId);
            // Assign value of Logix Tag NodeId to new variable 
            newVar.Value = logixTag.NodeId;
            // Add new variable into the launch object
            launchAliasObj.Add(newVar);
            // Launch DialogBox passing Launch Object that contains the alias as an alias 
            UICommands.OpenDialog(lButton, dBFromString, launchAliasObj.NodeId);
        }
        catch
        {
            Log.Warning(this.GetType().Name, "Failed to launch dialog box '" + faceplateTypeName + "' for tag '" + logixTag.BrowseName + "'.");
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
