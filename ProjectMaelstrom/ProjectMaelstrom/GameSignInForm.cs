using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom;

/// <summary>Explicit local credential setup only. Opening this dialog never
/// retrieves a saved username/password, launches a game or sends game input.</summary>
internal sealed class GameSignInForm : Form
{
    private readonly GameCredentialStore _store;
    private readonly GameCredentialProfile _profile;
    private readonly Label _status = new() { AutoSize = true, MaximumSize = new(550, 0), AccessibleName = "Saved sign-in status" };
    private readonly TextBox _userName = new() { Width = 520, MaxLength = 256, AccessibleName = "Game username", AutoCompleteMode = AutoCompleteMode.None };
    private readonly TextBox _password = new() { Width = 520, MaxLength = 1280, UseSystemPasswordChar = true, AccessibleName = "Game password", AutoCompleteMode = AutoCompleteMode.None };
    private readonly Button _save = new() { Text = "Save sign-in on this PC", AutoSize = true };
    private readonly Button _remove = new() { Text = "Remove saved sign-in", AutoSize = true };

    public GameSignInForm() : this(GameCredentialStore.Windows(), GameCredentialProfile.Wizard101) { }

    internal GameSignInForm(GameCredentialStore store, GameCredentialProfile profile)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _profile = profile ?? throw new ArgumentNullException(nameof(profile));
        Text = "Maelstrom · " + profile.DisplayName + " sign-in";
        ClientSize = new(610, 550); MinimumSize = new(590, 500);
        StartPosition = FormStartPosition.CenterParent; AutoScaleMode = AutoScaleMode.Dpi; Font = new("Segoe UI", 10);
        var body = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill, Padding = new(24), AutoScroll = true,
            FlowDirection = FlowDirection.TopDown, WrapContents = false
        };
        var heading = new Label { Text = profile.DisplayName + " sign-in", AutoSize = true, Font = new(Font.FontFamily, 20, FontStyle.Bold) };
        body.Controls.Add(heading);
        body.Controls.Add(Paragraph("Optional: save your game sign-in in Windows Credential Manager for this Windows account on this PC. Enter it here, never in chat, a game pack or a report."));
        body.Controls.Add(_status);
        body.Controls.Add(Paragraph("Automatic sign-in is not connected yet. Saving credentials does not log in, launch the game or enable an unattended routine."));
        body.Controls.Add(new Label { Text = "Username", AutoSize = true, Margin = new(0, 10, 0, 4) });
        body.Controls.Add(_userName);
        body.Controls.Add(new Label { Text = "Password", AutoSize = true, Margin = new(0, 10, 0, 4) });
        body.Controls.Add(_password);
        body.Controls.Add(Paragraph("Saved details are never filled back into this screen. Re-enter both fields to replace a saved sign-in. The fields clear after saving or closing."));
        var actions = new FlowLayoutPanel { AutoSize = true, WrapContents = true, MaximumSize = new(550, 0) };
        actions.Controls.AddRange([_save, _remove]); body.Controls.Add(actions);
        var close = new Button { Text = "Close", AutoSize = true, DialogResult = DialogResult.Cancel, Margin = new(0, 12, 0, 0) };
        body.Controls.Add(close); Controls.Add(body);
        _save.Click += (_, _) => SaveCredential();
        _remove.Click += (_, _) => RemoveCredential();
        _userName.TextChanged += (_, _) => UpdateSave();
        _password.TextChanged += (_, _) => UpdateSave();
        close.Click += (_, _) => Close(); CancelButton = close;
        WorkspaceTheme.Apply(this); heading.ForeColor = WorkspaceTheme.Palette.Accent;
        RefreshStatus(); UpdateSave();
    }

    private static Label Paragraph(string text) => new()
    {
        Text = text, AutoSize = true, MaximumSize = new(550, 0), Margin = new(0, 12, 0, 12)
    };

    private void UpdateSave() => _save.Enabled = !string.IsNullOrWhiteSpace(_userName.Text) && _password.TextLength > 0;

    private void RefreshStatus()
    {
        var status = _store.GetStatus(_profile);
        _status.Text = status switch
        {
            GameCredentialStatus.Stored => "A sign-in is saved for this game on this PC.",
            GameCredentialStatus.Missing => "No sign-in is saved for this game.",
            _ => "Windows Credential Manager is unavailable. No saved details have been loaded."
        };
        _remove.Enabled = status == GameCredentialStatus.Stored;
    }

    private void SaveCredential()
    {
        try
        {
            _store.Save(_profile, _userName.Text, _password.Text.AsSpan());
            RefreshStatus();
        }
        catch (Exception)
        {
            // Deliberately exclude exception text and entered values from UI/logs.
            MessageBox.Show(this, "The sign-in could not be saved. Check both fields and Windows Credential Manager, then try again.", "Sign-in not saved");
        }
        finally { ClearFields(); }
    }

    private void RemoveCredential()
    {
        try { _store.Remove(_profile); RefreshStatus(); }
        catch (Exception)
        {
            MessageBox.Show(this, "The saved sign-in could not be removed. Try again in Windows Credential Manager.", "Sign-in not removed");
        }
        finally { ClearFields(); }
    }

    private void ClearFields()
    {
        if (!_password.IsDisposed) _password.Clear();
        if (!_userName.IsDisposed) _userName.Clear();
    }
    protected override void OnFormClosed(FormClosedEventArgs e) { ClearFields(); base.OnFormClosed(e); }
    protected override void Dispose(bool disposing) { if (disposing) ClearFields(); base.Dispose(disposing); }
}
