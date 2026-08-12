#region Using directives

using System;
using UAManagedCore;
using OpcUa = UAManagedCore.OpcUa;
using FTOptix.NativeUI;
using FTOptix.HMIProject;
using FTOptix.Store;
using FTOptix.UI;
using FTOptix.EventLogger;
using FTOptix.SQLiteStore;
using FTOptix.CoreBase;
using FTOptix.Core;
using FTOptix.NetLogic;
using FTOptix.AuditSigning;
using FTOptix.InfluxDBStore;
using FTOptix.InfluxDBStoreLocal;
using FTOptix.SerialPort;

#endregion

public class SignWorkflowHandler : BaseNetLogic
{
    private Button _confirmButton;
    private Button _cancelButton;

    public override void Start()
    {
        _confirmButton = Owner.Get<Button>("Confirm");

        if (_confirmButton == null)
            Log.Error("SignWorkflowHandler", "Confirm button not found");

        _cancelButton = Owner.Get<Button>("Cancel");

        if (_cancelButton == null)
            Log.Error("SignWorkflowHandler", "Cancel button not found");
    }

    public override void Stop()
    {
        // Insert code to be executed when the user-defined logic is stopped
    }


    /// <summary>
    /// This method initiates the signing process by calling the Sign method on the SigningController alias.
    /// The method retrieves the SigningController and passes the provided username, password, and note.
    /// The result of the signing operation is then handled by the CheckResult method.
    /// The confirm button is disabled during the operation to prevent multiple submissions.
    /// The cancel button is disabled during the operation to prevent cancellation.
    /// </summary>
    /// <param name="Username"></param>
    /// <param name="Password"></param>
    /// <param name="Note"></param>
    [ExportMethod]
    public void Sign(string Username, string Password, string Note)
    {
        if (_confirmButton != null)
            _confirmButton.Enabled = false;
        if (_cancelButton != null)
            _cancelButton.Enabled = false;

        var signingControllerAlias = LogicObject.GetAlias("SigningController") as SigningController;

        if (signingControllerAlias == null)
        {
            Log.Error("SignWorkflowHandler", "SigningController Alias not found.");
            if (_confirmButton != null)
                _confirmButton.Enabled = true;
            if (_cancelButton != null)
                _cancelButton.Enabled = true;
            return;
        }

        try
        {
            var result = signingControllerAlias.Sign(Username, Password, Note);
            CheckResult(result);
        }
        catch (Exception ex)
        {
            Log.Error("SignWorkflowHandler", $"An error occurred during signing: {ex.Message}");
        }

        if (_confirmButton != null)
            _confirmButton.Enabled = true;
        if (_cancelButton != null)
            _cancelButton.Enabled = true;
    }

    /// <summary>
    /// This method handles different outcomes based on the SignResult parameter.
    /// Each case corresponds to a specific outcome with appropriate dialog opening or message updates.
    /// The logic involves accessing UI objects and displaying relevant messages for failed login attempts.
    /// </summary>
    /// <param name="signResult">The result indicating which action to take.</param>
    /// <remarks>
    /// - If Succeeded, closes the AuditDialog.
    /// - If FirstUserLoginFailed, opens the WorkflowFailDialog with localized text and prompts for confirmation.
    /// - If FirstUserNotAuthorized, also opens the WorkflowFailDialog with localized text and prompts for confirmation.
    /// </remarks>
    public void CheckResult(SignResult signResult)
    {
        switch(signResult)
        {
            case SignResult.Succeeded:
            {
                var auditDialog = (AuditDialogBox)LogicObject.GetAlias("AuditDialog");
                auditDialog.Close();
                break;
            }
            case SignResult.FirstUserLoginFailed:
            {
                var wrongPasswordDialog = (DialogType)((IUAObject)LogicObject.Owner).ObjectType.Owner.Get("WorkflowFailDialog");
                wrongPasswordDialog.Get<Label>("User").LocalizedText = new LocalizedText(LogicObject.NodeId.NamespaceIndex, "SigningWorkflowFirstLoginFailed");
                _confirmButton.OpenDialog(wrongPasswordDialog);
                break;
            }
            case SignResult.FirstUserNotAuthorized:
            {
                var wrongPasswordDialog = (DialogType)((IUAObject)LogicObject.Owner).ObjectType.Owner.Get("WorkflowFailDialog");
                wrongPasswordDialog.Get<Label>("User").LocalizedText = new LocalizedText(LogicObject.NodeId.NamespaceIndex, "SigningWorkflowFirstLoginUnauthorized");
                _confirmButton.OpenDialog(wrongPasswordDialog);
                break;
            }
        }
    }
}
