using System.Diagnostics;
using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom;

/// <summary>Local archive → calibration → recording review → proposal pack. No game input capability.</summary>
internal sealed class PetDanceTeacherForm : Form
{
    private readonly string _dataRoot;
    private readonly FlowLayoutPanel _commands = new() { Dock = DockStyle.Fill, AutoSize = true };
    private readonly FlowLayoutPanel _calibration = new() { Dock = DockStyle.Fill, AutoSize = true };
    private readonly PictureBox _image = new() { Dock = DockStyle.Fill, SizeMode = PictureBoxSizeMode.Zoom, BackColor = Color.FromArgb(28, 30, 34) };
    private readonly NumericUpDown _frame = new() { Minimum = 0, Maximum = 0, Width = 85 };
    private readonly NumericUpDown _length = new() { Minimum = 3, Maximum = 7, Value = 3, Width = 55 };
    private readonly ComboBox _direction = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 80 };
    private readonly Label _context = new() { AutoSize = true, MaximumSize = new Size(980, 0), Text = "No pack or recording selected." };
    private readonly Label _status = new() { AutoSize = true, MaximumSize = new Size(980, 0), Text = "Stopped · no model, capture or game controls are running." };
    private readonly Button _stop = new() { Text = "Stop review", AutoSize = true, Enabled = false };
    private readonly Button _report = new() { Text = "Open report", AutoSize = true, Enabled = false };
    private TeachingRecording? _recording;
    private string? _pack, _lastReport, _resultPack;
    private VisualStateRecognizer? _phases;
    private Rectangle? _region;
    private Point? _drag;
    private int _neutral = -1;
    private CancellationTokenSource? _cancel;
    private bool _closeRequested;

    public PetDanceTeacherForm(string dataRoot, Action<string>? openPack = null)
    {
        _dataRoot = dataRoot; Text = "Maelstrom · Pet Dance teacher";
        var review = new Button { Text = "Run teacher routine → save pack", AutoSize = true };
        Size = new(1080, 860); MinimumSize = new(950, 790); Font = new("Segoe UI", 10); AutoScaleMode = AutoScaleMode.Dpi;
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new(18), RowCount = 7, ColumnCount = 1 };
        for (var row = 0; row < 7; row++) layout.RowStyles.Add(new(row == 3 ? SizeType.Percent : SizeType.AutoSize, row == 3 ? 100 : 0));
        layout.Controls.Add(new Label { AutoSize = true, MaximumSize = new(980, 0), Text = "Learn from the legacy Pet Dance teacher\nImport its local archive, open a recording, and drag around the arrow area. Choose an empty-gap frame and a known arrow to calibrate. Review creates sequence proposals at Go; it does not play the game or feed snacks." }, 0, 0);
        layout.Controls.Add(_commands, 0, 1); layout.Controls.Add(_context, 0, 2); layout.Controls.Add(_image, 0, 3); layout.Controls.Add(_calibration, 0, 4);
        var actions = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill }; layout.Controls.Add(actions, 0, 5); layout.Controls.Add(_status, 0, 6); Controls.Add(layout);
        Button Button(string title, Action action)
        {
            var button = new Button { Text = title, AutoSize = true }; button.Click += (_, _) => Guard(action); return button;
        }
        var import = new Button { Text = "Import Pet Dance archive…", AutoSize = true }; _commands.Controls.Add(import);
        import.Click += async (_, _) =>
        {
            using var dialog = new OpenFileDialog { Title = "Choose the supplied petdance.zip or TAR archive", Filter = "Pet Dance archive|*.zip;*.tar|All files|*.*" };
            if (dialog.ShowDialog(this) != DialogResult.OK) return;
            await Work(async token => { _pack = await Task.Run(() => PetDanceAssets.Import(dialog.FileName, _dataRoot, token), token); _status.Text = "Imported eight verified local arrow images. Next: open a teaching recording and calibrate."; UpdateContext(); });
        };
        _commands.Controls.Add(Button("Open teacher pack…", () =>
        {
            var path = Pick("Choose a Pet Dance teacher pack", "Game pack|pack.json", "game-packs"); if (path == null) return;
            _ = PetDanceAssets.Read(path); _pack = path; UpdateContext(); _status.Text = "Teacher pack loaded. Its saved calibration is used during review.";
        }));
        _commands.Controls.Add(Button("Open recording…", () =>
        {
            var path = Pick("Choose a Pet Dance recording", "Recording|recording.json", "recordings"); if (path == null) return;
            var recording = TeachingRecording.Open(path);
            if (recording.Frames.Count == 0) throw new InvalidDataException("This recording has no frames.");
            if (GameplaySamplePlan.Load(recording.DirectoryPath)?.Purpose == "check") throw new InvalidOperationException("Use a teaching recording here; keep check sessions independent.");
            _recording = recording; _region = null; _neutral = -1; _frame.Value = 0; _frame.Maximum = recording.Frames.Count - 1; ShowFrame(); UpdateContext();
        }));
        _commands.Controls.Add(Button("Open phase teaching pack…", () =>
        {
            var path = Pick("Choose a pack taught on the Watch/Go/result text area", "Game pack|pack.json", "game-packs"); if (path == null) return;
            _phases = VisualStateRecognizer.Learn(path); UpdateContext();
        }));
        _commands.Controls.Add(Button("Use frame phase labels", () => { _phases = null; UpdateContext(); }));
        _commands.Controls.Add(Button("Review phase labels…", () =>
        {
            if (_recording == null) throw new InvalidOperationException("Open a recording first.");
            using var form = new TeachingReviewForm(_recording, _dataRoot, openPack); form.ShowDialog(this); ShowFrame();
        }));
        _direction.Items.AddRange(new object[] { DanceCue.Up, DanceCue.Down, DanceCue.Left, DanceCue.Right }); _direction.SelectedIndex = 0;
        _calibration.Controls.AddRange([new Label { AutoSize = true, Text = "Frame (0-based)", Padding = new(0, 6, 0, 0) }, _frame,
            Button("Use as empty gap", () => { if (_recording == null) throw new InvalidOperationException("Open a recording first."); _neutral = (int)_frame.Value; _status.Text = $"Empty gap: frame {_neutral}. Now select a visible arrow frame and its direction."; }), _direction]);
        var calibrate = new Button { Text = "Calibrate on this arrow", AutoSize = true }; _calibration.Controls.Add(calibrate);
        calibrate.Click += async (_, _) =>
        {
            if (_pack == null || _recording == null || _region == null || _neutral < 0) { _status.Text = "Load a teacher pack and recording, drag an arrow-area region, then choose an empty-gap frame."; return; }
            var index = (int)_frame.Value; var neutral = _neutral; var region = _region.Value; var direction = (DanceCue)_direction.SelectedItem!;
            var pack = _pack; var recording = _recording;
            await Work(async token =>
            {
                var result = await Task.Run(() => PetDanceVision.Calibrate(pack, recording, index, neutral, region, direction, token), token);
                _status.Text = $"Calibration saved · scale {result.Scale:0.00} · {region.Width} × {region.Height} arrow area. Recalibrate after changing game UI scale or window size.";
            });
        };
        _calibration.Controls.Add(new Label { AutoSize = true, MaximumSize = new(970, 0), Text = "Phase labels: dance-ready, dance-watch, dance-go, dance-success, dance-failure. Teach the phase-text region in Review and label; unknown phases block proposals. Use at least 10 samples/s (20 recommended). One-per-second stills miss cue boundaries." });
        actions.Controls.AddRange([new Label { AutoSize = true, Text = "First round cues", Padding = new(0, 6, 0, 0) }, _length, review, _stop, _report]);
        var workshop = Button("Open result in Workshop", () => { if (_resultPack == null) throw new InvalidOperationException("Review a recording first."); openPack?.Invoke(_resultPack); });
        workshop.Enabled = openPack != null; actions.Controls.Add(workshop);
        review.Click += async (_, _) =>
        {
            if (_pack == null || _recording == null) { _status.Text = "Load a calibrated teacher pack and a recording first."; return; }
            var pack = _pack; var recording = _recording; var phases = _phases; var expected = (int)_length.Value;
            await Work(async token =>
            {
                var progress = new Progress<string>(message => { if (!IsDisposed) _status.Text = message; });
                var ticket = PetDanceOperation.Submit(ApplicationOperations.For(_dataRoot), pack, recording, phases, expected, _dataRoot, progress);
                using var registration = token.Register(ticket.Cancel);
                _status.Text = "Teacher routine queued · one worker · Stop cancels this job.";
                var result = await ticket.Completion;
                _status.Text = result.State + " · " + result.Message;
                if (result.State != OperationState.Completed) return;
                _lastReport = result.Artifacts[0]; _resultPack = result.Artifacts[1]; _report.Enabled = true;
                if (openPack != null) _status.Text += " Pack is available in the Workshop library.";
            });
        };
        _stop.Click += (_, _) => _cancel?.Cancel();
        _report.Click += (_, _) => Guard(() => { if (_lastReport != null) Process.Start(new ProcessStartInfo(_lastReport) { UseShellExecute = true }); });
        _frame.ValueChanged += (_, _) => Guard(ShowFrame);
        _image.MouseDown += (_, e) => { if (_cancel == null && e.Button == MouseButtons.Left && _image.Image != null) _drag = e.Location; };
        _image.MouseUp += (_, e) => Guard(() =>
        {
            if (_drag is not Point start || _image.Image == null || _cancel != null) return; _drag = null;
            var image = _image.Image; var scale = Math.Min((double)_image.Width / image.Width, (double)_image.Height / image.Height);
            var left = (_image.Width - image.Width * scale) / 2; var top = (_image.Height - image.Height * scale) / 2;
            int X(int x) => Math.Clamp((int)((x - left) / scale), 0, image.Width); int Y(int y) => Math.Clamp((int)((y - top) / scale), 0, image.Height);
            var region = Rectangle.FromLTRB(X(Math.Min(start.X, e.X)), Y(Math.Min(start.Y, e.Y)), X(Math.Max(start.X, e.X)), Y(Math.Max(start.Y, e.Y)));
            if (region.Width is < 20 or > 256 || region.Height is < 20 or > 256) throw new InvalidDataException("Select a 20–256 pixel area containing the whole arrow, with a small border.");
            _region = region; _image.Invalidate(); _status.Text = $"Arrow area: {region.X}, {region.Y}, {region.Width} × {region.Height}.";
        });
        _image.Paint += (_, e) =>
        {
            if (_region is not Rectangle region || _image.Image == null) return;
            var scale = Math.Min((float)_image.Width / _image.Image.Width, (float)_image.Height / _image.Image.Height);
            var left = (_image.Width - _image.Image.Width * scale) / 2; var top = (_image.Height - _image.Image.Height * scale) / 2;
            using var pen = new Pen(Color.LimeGreen, 2); e.Graphics.DrawRectangle(pen, left + region.X * scale, top + region.Y * scale, region.Width * scale, region.Height * scale);
        };
        FormClosing += (_, e) => { if (_cancel != null) { e.Cancel = true; _closeRequested = true; _cancel.Cancel(); } };
        FormClosed += (_, _) => _image.Image?.Dispose();

        async Task Work(Func<CancellationToken, Task> operation)
        {
            if (_cancel != null) return; using var cancel = new CancellationTokenSource(); _cancel = cancel;
            _commands.Enabled = _calibration.Enabled = review.Enabled = _length.Enabled = _report.Enabled = false; _stop.Enabled = true;
            try { _status.Text = "Reviewing locally · one worker · no model or game input…"; await operation(cancel.Token); }
            catch (OperationCanceledException) { _status.Text = "Stopped. Source recording and human labels are unchanged."; }
            catch (Exception error) { _status.Text = "Could not complete: " + error.Message; }
            finally
            {
                _cancel = null; _commands.Enabled = _calibration.Enabled = review.Enabled = _length.Enabled = true; _report.Enabled = _lastReport != null; _stop.Enabled = false;
                if (_closeRequested) Close();
            }
        }
    }
    private string? Pick(string title, string filter, string folder)
    {
        using var dialog = new OpenFileDialog { Title = title, Filter = filter, InitialDirectory = Path.Combine(_dataRoot, folder) };
        return dialog.ShowDialog(this) == DialogResult.OK ? dialog.FileName : null;
    }
    private void Guard(Action action) { try { action(); } catch (Exception error) { _status.Text = error.Message; } }
    private void ShowFrame()
    {
        if (_recording == null) return;
        var image = _recording.ReadFrame((int)_frame.Value); _image.Image?.Dispose(); _image.Image = image;
        var frame = _recording.Frames[(int)_frame.Value]; _status.Text = $"{frame.TimeMs:0} ms · phase label: {(frame.State.Length == 0 ? "unlabeled" : frame.State)} · drag around the arrow area.";
    }
    private void UpdateContext() => _context.Text = $"Teacher: {(_pack == null ? "not selected" : Path.GetFileName(Path.GetDirectoryName(_pack)))}\nRecording: {(_recording == null ? "not selected" : _recording.Workspace + " · " + _recording.Frames.Count + " frames")} · Phases: {(_phases?.PackName ?? "per-frame human labels")}";

    internal void RenderForVerification(string output)
    {
        Directory.CreateDirectory(output); _ = Handle; PerformLayout();
        void CreateHandles(Control control) { _ = control.Handle; foreach (Control child in control.Controls) CreateHandles(child); }
        CreateHandles(this); PerformLayout();
        using var image = new Bitmap(ClientSize.Width, ClientSize.Height);
        foreach (Control child in Controls) child.DrawToBitmap(image, child.Bounds);
        image.Save(Path.Combine(output, "pet-dance-teacher.png"));
        GameExperiments.WriteAtomic(Path.Combine(output, "pet-dance-ui.json"), new { title = Text, active = _cancel != null, stop_enabled = _stop.Enabled, pack_selected = _pack != null, recording_selected = _recording != null, first_round_cues = _length.Value, input_capability = false });
    }
}
