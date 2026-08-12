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

public class DoubleSignWorkflowHandler : BaseNetLogic
{
    private Button _confirmButton;
    private Button _cancelButton;

    public override void Start()
    {
        _confirmButton = Owner.Get<Button>("Confirm");

        if (_confirmButton == null)
            Log.Error("DoubleSignWorkflowHandler", "Confirm button not found");

        _cancelButton = Owner.Get<Button>("Cancel");

        if (_cancelButton == null)
            Log.Error("DoubleSignWorkflowHandler", "Cancel button not found");

    }

    public override void Stop()
    {
        // Insert code to be executed when the user-defined logic is stopped
    }

    /// <summary>
    /// This method initiates the signing process by calling the DoubleSign method on the SigningController alias.
    /// The method retrieves the SigningController and passes the provided username(s), password(s), and note(s).
    /// The result of the signing operation is then handled by the CheckResult method.
    /// The confirm button is disabled during the operation to prevent multiple submissions.
    /// The cancel button is disabled during the operation to prevent cancellation.
    /// </summary>
    /// <param name="FirstUsername"></param>
    /// <param name="FirstPassword"></param>
    /// <param name="FirstNote"></param>
    /// <param name="SecondUsername"></param>
    /// <param name="SecondPassword"></param>
    /// <param name="SecondNote"></param>
    [ExportMethod]
    public void DoubleSign(string FirstUsername, string FirstPassword, string FirstNote, string SecondUsername, string SecondPassword, string SecondNote)
    {
        if (_confirmButton != null)
            _confirmButton.Enabled = false;
        if (_cancelButton != null)
            _cancelButton.Enabled = false;

        var signingControllerAlias = LogicObject.GetAlias("SigningController") as SigningController;

        if (signingControllerAlias == null)
        {
            Log.Error("DoubleSignWorkflowHandler", "SigningController Alias not found.");
            if (_confirmButton != null)
                _confirmButton.Enabled = true;
            if (_cancelButton != null)
                _cancelButton.Enabled = true;
            return;
        }

        try
        {
            var result = signingControllerAlias.DoubleSign(FirstUsername, FirstPassword, FirstNote, SecondUsername, SecondPassword, SecondNote);
            CheckResult(result);
        }
        catch (Exception ex)
        {
            Log.Error("DoubleSignWorkflowHandler", $"An error occurred during the double sign operation: {ex.Message}");
        }

        if (_confirmButton != null)
            _confirmButton.Enabled = true;
        if (_cancelButton != null)
            _cancelButton.Enabled = true;
    }

    /// <summary>
    /// This method handles different sign result scenarios by displaying appropriate dialogs or closing existing ones based on the provided <see cref="SignResult"/> enum value.
    /// <example>
    /// For example:
    /// <code>
    /// CheckResult(SignResult.Succeeded);
    /// </code>
    /// will close the audit dialog box if the sign result is succeeded.
    /// </example>
    /// </summary>
    /// <param name="signResult">The result of the sign operation, which determines the action to be taken.</param>
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
                var failureDialog = (DialogType)((IUAObject)LogicObject.Owner).ObjectType.Owner.Get("WorkflowFailDialog");
                failureDialog.Get<Label>("User").LocalizedText = new LocalizedText(LogicObject.NodeId.NamespaceIndex, "SigningWorkflowFirstLoginFailed");
                _confirmButton.OpenDialog(failureDialog);
                break;
            }
            case SignResult.SecondUserLoginFailed:
            {
                var failureDialog = (DialogType)((IUAObject)LogicObject.Owner).ObjectType.Owner.Get("WorkflowFailDialog");
                failureDialog.Get<Label>("User").LocalizedText = new LocalizedText(LogicObject.NodeId.NamespaceIndex, "SigningWorkflowSecondLoginFailed");
                _confirmButton.OpenDialog(failureDialog);
                break;
            }
            case SignResult.FirstUserNotAuthorized:
            {
                var failureDialog = (DialogType)((IUAObject)LogicObject.Owner).ObjectType.Owner.Get("WorkflowFailDialog");
                failureDialog.Get<Label>("User").LocalizedText = new LocalizedText(LogicObject.NodeId.NamespaceIndex, "SigningWorkflowFirstLoginUnauthorized");
                _confirmButton.OpenDialog(failureDialog);
                break;
            }
            case SignResult.SecondUserNotAuthorized:
            {
                var failureDialog = (DialogType)((IUAObject)LogicObject.Owner).ObjectType.Owner.Get("WorkflowFailDialog");
                failureDialog.Get<Label>("User").LocalizedText = new LocalizedText(LogicObject.NodeId.NamespaceIndex, "SigningWorkflowSecondLoginUnauthorized");
                _confirmButton.OpenDialog(failureDialog);
                break;
            }
            case SignResult.SecondUserMustBeDifferentFromFirstUser:
            {
                var failureDialog = (DialogType)((IUAObject)LogicObject.Owner).ObjectType.Owner.Get("WorkflowFailDialog");
                failureDialog.Get<Label>("User").LocalizedText = new LocalizedText(LogicObject.NodeId.NamespaceIndex, "SigningWorkflowSecondUserMustBeDifferentFromFirstUser");
                _confirmButton.OpenDialog(failureDialog);
                break;
            }
        }
    }
}
