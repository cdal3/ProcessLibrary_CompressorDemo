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

public class raP_5_20_Prompt_NL_Config : BaseNetLogic
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
        IUANode Ref_Primary_Tag = null;
        UAVariable launchAlias = null;
        string faceplateTypeName = null;
        string launchAliasPath = null;
        IUANode launchAliasObj = null;
        IUANode DialogBox_Call_Tag = null;
        string nav_Context = "";
        string Ref_Primary_Path = null;
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
            nav_Context = lButton.GetVariable("Nav_Context").Value;
            launchAliasObj = InformationModel.MakeObject("LaunchAliasObj");

            if (string.IsNullOrEmpty(launchAliasPath))
            {
                Log.Warning("Prompt_NL", "launchAliasPath is empty");
                return;
            }

            // Cache common lookups to avoid repeated Project.Current.Get calls.
            var baseTag = Project.Current.Get(launchAliasPath);
            var promptTag = Project.Current.Get(launchAliasPath + "_Prompt");
            var promptsTag = Project.Current.Get(launchAliasPath + "_Prompts");
            var stepsTag = Project.Current.Get(launchAliasPath + "_Steps");

            // nav_Context:
            // "0" = dialog uses core object as call target
            // "1" = dialog uses current alias directly
            if (nav_Context == "0")
            {
                const string promptSuffix = "_Prompt";
                bool isPromptAlias = launchAliasPath.EndsWith(promptSuffix, StringComparison.Ordinal);

                if (isPromptAlias)
                {
                    // launchAliasPath already points to *_Prompt. Core is the base object.
                    Ref_Primary_Path = launchAliasPath.Substring(0, launchAliasPath.Length - promptSuffix.Length);
                    Ref_Primary_Tag = Project.Current.Get(Ref_Primary_Path);
                    Ref_Tag = baseTag;
                }
                else
                {
                    // launchAliasPath points to base object. Try to resolve *_Prompt as Ref_Tag.
                    Ref_Primary_Path = launchAliasPath;
                    Ref_Primary_Tag = baseTag;
                    Ref_Tag = promptTag ?? baseTag;

                    // Optional companion node used by some prompts.
                    if (promptsTag != null)
                    {
                        var refTagPromptsVar = InformationModel.MakeVariable("Ref_Tag_Prompts", OpcUa.DataTypes.NodeId);
                        refTagPromptsVar.Value = promptsTag.NodeId;
                        launchAliasObj.Add(refTagPromptsVar);
                    }

                    if (stepsTag != null)
                    {
                        var refTagStepsVar = InformationModel.MakeVariable("Ref_Tag_Steps", OpcUa.DataTypes.NodeId);
                        refTagStepsVar.Value = stepsTag.NodeId;
                        launchAliasObj.Add(refTagStepsVar);
                    }
                }

                DialogBox_Call_Tag = Ref_Primary_Tag;
            }
            else if (nav_Context == "1")
            {
                Ref_Tag = baseTag;
                Ref_Primary_Tag = Ref_Tag;
                DialogBox_Call_Tag = Ref_Tag;
            }
            else
            {
                Log.Warning("Prompt_NL", $"Unsupported Nav_Context: {nav_Context}");
                return;
            }

            // Guard before creating LaunchAliasObj payload variables.
            if (Ref_Tag == null || Ref_Primary_Tag == null)
            {
                Log.Warning("Prompt_NL", $"Ref resolution failed. launchAliasPath={launchAliasPath}, nav_Context={nav_Context}");
                return;
            }

            var refTagVar = InformationModel.MakeVariable("Ref_Tag", OpcUa.DataTypes.NodeId);
            refTagVar.Value = Ref_Tag.NodeId;
            launchAliasObj.Add(refTagVar);

            var refPrimaryVar = InformationModel.MakeVariable("Ref_Primary", OpcUa.DataTypes.NodeId);
            refPrimaryVar.Value = Ref_Primary_Tag.NodeId;
            launchAliasObj.Add(refPrimaryVar);
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
            if(lType!= "raP_Opr_Prompt")
            {
                lType = "raP_Opr_Prompt";
            }
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
