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
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.IO;
using FTOptix.SQLiteStore;
using FTOptix.System;
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

public class raSDK1_NL_NavUsingTag : BaseNetLogic
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

    public void NavTag()
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
        DialogType dBFromString = null;
        IUANode logixTag = null;
        IUAObject lButton = null;
        IUAObject launchAliasObj = null;
        string faceplateTypeName = "";

        //  Find the button owner object and the Ref_* tags associated with it.
        try
        {
            // Get button object
            lButton = Owner.Owner.GetObject(this.Owner.BrowseName);

            // Make Launch Object that will contain aliases
            launchAliasObj = InformationModel.MakeObject("LaunchAliasObj");

            // Get each alias from Launch Button and add them into Launch Object, and assign NodeId values 
            foreach (var inpTag in lButton.Children)
            {
                if (inpTag.BrowseName.Contains("Ref_"))
                {
                    try
                    {
                        // Make a variable with same name as alias of type NodeId
                        var newVar = InformationModel.MakeVariable(inpTag.BrowseName, OpcUa.DataTypes.NodeId);
                        // Assign alias value to new variable
                        newVar.Value = ((UAManagedCore.UAVariable)inpTag).Value;
                        // Add variable int launch object
                        launchAliasObj.Add(newVar);

                        if (inpTag.BrowseName == "Ref_Tag")
                        {
                            logixTag = InformationModel.Get(((UAVariable)inpTag).Value);
                        }
                    }
                    catch
                    {
                        if (inpTag.BrowseName == "Ref_Tag")
                        {
                            Log.Warning(this.GetType().Name, "Unable to create alias for object '" + inpTag.BrowseName + "'");
                            return;
                        }
                        else
                        {
                            Log.Info(this.GetType().Name, "Skipping alias creation for object '" + inpTag.BrowseName + "'");
                        }
                    }

                }
            }
        }
        catch
        {
            Log.Warning(this.GetType().Name, "Error creating alias Ref_* objects");
            return;
        }

        // Make sure the Logix Tag is valid before continuing
        if (logixTag == null)
        {
            Log.Warning(this.GetType().Name, "Failed to get logix tag from Ref_* objects");
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
            if ( foundFp == null )
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


        // Launch the faceplate
        try
        {
            // Launch DialogBox passing Launch Object that contains the aliases as an alias 
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
