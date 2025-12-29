using System.Drawing;
using System.Windows.Forms;

namespace ProjectMaelstrom
{
    public class OverlayHudForm : Form
    {
        private readonly Label _titleLabel;
        private readonly Label _statusLabel;
        private readonly Label _playlistLabel;

        public OverlayHudForm()
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            BackColor = Color.FromArgb(18, 24, 38);
            ForeColor = Color.Gold;
            Opacity = 0.9;
            StartPosition = FormStartPosition.Manual;
            Size = new Size(260, 140);

            _titleLabel = new Label
            {
                AutoSize = true,
                Location = new Point(12, 12),
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                Text = "SmartPlay HUD"
            };

            _statusLabel = new Label
            {
                AutoSize = true,
                Location = new Point(12, 44),
                Text = "Status: Idle"
            };

            _playlistLabel = new Label
            {
                AutoSize = true,
                Location = new Point(12, 68),
                Text = "Playlist: 0 items"
            };

            Controls.Add(_titleLabel);
            Controls.Add(_statusLabel);
            Controls.Add(_playlistLabel);
        }

        public void UpdateStatus(string statusText, int playlistCount)
        {
            _statusLabel.Text = $"Status: {statusText}";
            _playlistLabel.Text = $"Playlist: {playlistCount} item(s)";
        }
    }
}
