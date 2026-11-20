using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom;

public partial class Main
{
    private void AddToolsNavigation()
    {
        var button = new Button
        {
            Text = "Tools & companions",
            AutoSize = true,
            MinimumSize = new Size(260, 42),
            Margin = new Padding(4, 4, 4, 8)
        };
        button.Click += (_, _) => OpenToolsCenter();
        navPanel.Controls.Add(button);
        navPanel.Controls.SetChildIndex(button, 1);
    }

    private void OpenToolsCenter()
    {
        _smartPlayManager?.Stop();
        _inputBridge?.Stop();
        try
        {
            using var window = new ToolCenterForm(() =>
            {
                if (_scriptLibraryService.CurrentSession != null) return "Close Tools and stop the current script before starting a gameplay helper.";
                if (_activeChildForm is HalfangFarmingBot or BazaarReagentBot) return "Close Tools and stop the active trainer bot before starting a gameplay helper.";
                if (_smartPlayManager?.QueueLength > 0 || _inputBridge?.IsIdle == false) return "Close Tools and finish the queued trainer tasks before starting a gameplay helper.";
                return null;
            });
            window.ShowDialog(this);
        }
        finally
        {
            _inputBridge?.Start(TimeSpan.FromSeconds(1));
            _smartPlayManager?.Start();
            ApplyPolicyUiState();
        }
    }
}
