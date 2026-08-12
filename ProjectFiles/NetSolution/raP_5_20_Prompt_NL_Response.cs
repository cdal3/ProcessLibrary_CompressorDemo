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
using System.Collections.Generic;
using System.Linq;
using System.Data;
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

public class raP_5_20_Prompt_NL_Response : BaseNetLogic
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

    public void NavSuffix()
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
        IUAObject lButton = null;
        DialogType dBFromString = null;
        IUANode Ref_Tag = null;
        IUANode Ref_Tag_Response = null;
        UAVariable launchAlias = null;
        string faceplateTypeName = null;
        string launchAliasPath = null;
        IUANode launchAliasObj = null;
        IUANode DialogBox_Call_Tag = null;
        string nav_Context = "";

        // Get the tag specified by Ref_BaseTag
        try
        {
            // Get button object
            lButton = Owner.Owner.GetObject(this.Owner.BrowseName);
            // Get Alias from button
            launchAlias = (UAManagedCore.UAVariable)lButton.Children.Get("Ref_BaseTag");
            // Get logix Tag from passed alias NodeId
            IUANode tagNodeId = InformationModel.Get(launchAlias.Value);
            // Get Browse Path for the tag
            launchAliasPath = GetOptixPathByNode(tagNodeId, "CommDrivers");
        }
        catch
        {
            Log.Warning(this.GetType().Name, "Error retrieving base tag specified by variable Ref_BaseTag (" + launchAlias + ").");
            return;
        }

        // Add the suffix to the base tag and get the resultant tag
        try
        {
            var cfg_Path = lButton.Children.GetVariable("Cfg_Suffix").Value;          
       
            nav_Context = lButton.GetVariable("Nav_Context").Value;
        
            if (nav_Context == "0")
            {
                Ref_Tag = Project.Current.Get((string)launchAliasPath);              
                Ref_Tag_Response = Project.Current.Get((string)launchAliasPath + cfg_Path);
                DialogBox_Call_Tag = Ref_Tag_Response;
            }
            else if (nav_Context == "1") 
            {
                Ref_Tag = Project.Current.Get((string)launchAliasPath);
                DialogBox_Call_Tag = Project.Current.Get((string)launchAliasPath + cfg_Path);
                Ref_Tag_Response = DialogBox_Call_Tag.Children.Get("Prompt");
            }
            else if(nav_Context == "2")
            {
                Ref_Tag = Project.Current.Get((string)launchAliasPath);
                DialogBox_Call_Tag = Ref_Tag;
                Ref_Tag_Response = DialogBox_Call_Tag.Children.Get("Prompt");

            }
            launchAliasObj = InformationModel.MakeObject("LaunchAliasObj");
            var newVar = InformationModel.MakeVariable("Ref_Tag", OpcUa.DataTypes.NodeId);
            var newVars = InformationModel.MakeVariable("Ref_Tag_Response", OpcUa.DataTypes.NodeId);
            newVars.Value = Ref_Tag_Response.NodeId;
            newVar.Value = Ref_Tag.NodeId;
            // Add new variable into the launch object
            launchAliasObj.Add(newVar);
            launchAliasObj.Add(newVars);
        }
        catch
        {
            Log.Warning(this.GetType().Name, "Error retrieving tag specified by Ref_BaseTag + Cfg_Suffix (" + launchAliasPath + ").");
            return;
        }

        // Make sure the Logix Tag is valid before continuing
        if (DialogBox_Call_Tag == null)
        {
            Log.Warning(this.GetType().Name, "Failed to get tag for path '" + launchAliasPath + "'");
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
            lib = (string)DialogBox_Call_Tag.Children.GetVariable(library).Value;
            lType = (string)DialogBox_Call_Tag.Children.GetVariable(libraryType).Value;
            sourceMsg = "Check Extended Tag Properties '" + library + "' (" + lib + ") and '" + libraryType + "' (" + lType + ")";
      
        }
        catch
        {
            // The extended tag property was empty, try the Inf_Lib and Inf_Type tags - this will eventually be removed
            try
            {
                lib = (string)DialogBox_Call_Tag.Children.GetVariable(libraryTag).RemoteRead().Value;
                lType = (string)DialogBox_Call_Tag.Children.GetVariable(libraryTypeTag).RemoteRead().Value;
                sourceMsg = "Check tag members '" + libraryTag + "' (" + lib + ") and '" + libraryTypeTag + "' (" + lType + ")";
            }
            catch
            {
                Log.Warning(this.GetType().Name, "Failed to read identity tags for object '" + DialogBox_Call_Tag.BrowseName + "'. Object must contain Extended Tag Properties '" + library + "' and '" + libraryType + "' or tags '" + libraryTag + "' and '" + libraryTypeTag + "'");
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
                Log.Warning(this.GetType().Name, "Dialog Box '" + faceplateTypeName + "' not found for tag '" + DialogBox_Call_Tag.BrowseName + "'. " + sourceMsg);
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
            Log.Warning(this.GetType().Name, "Error retrieving Dialog Box for tag '" + DialogBox_Call_Tag.BrowseName + "'. " + sourceMsg);
            return;
        }

        // Create the object that contains the alias and launch the faceplate
        try
        {

            UICommands.OpenDialog(lButton, dBFromString, launchAliasObj.NodeId);
        }
        catch
        {
            Log.Warning(this.GetType().Name, "Failed to launch dialog box '" + faceplateTypeName + "' for tag '" + DialogBox_Call_Tag.BrowseName + "'.");
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
    public string GetOptixPathByNode(IUANode inputNode, string topContainer)
    {
        List<string> pathToVar = new List<string>();

        FindBrowsePath(inputNode);
        if (pathToVar.Count > 0)
        {
            var launchAliasPath = ConstructBrowsePath();
            return launchAliasPath;
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

    public void CloseCurrentDB(IUANode inputNode)
    {
        // if input node is of type Dialog, close it
        if (inputNode.GetType().BaseType.BaseType == typeof(Dialog))
        {
            // close dialog box
            ((Dialog)inputNode).Close();
            return;
        }
        // if input node is Main Window, no dialog box was found, return
        if (inputNode.GetType() == typeof(MainWindow))
        {
            return;
        }
        // continue search for Dialog or Main Window
        CloseCurrentDB(inputNode.Owner);
    }

}
