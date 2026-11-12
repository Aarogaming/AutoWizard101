using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom;

internal sealed class TeachingReviewForm : Form
{
    private readonly TeachingRecording _recording;
    private readonly ListBox _frames = new() { Dock = DockStyle.Fill };
    private readonly PictureBox _image = new() { Dock = DockStyle.Fill, SizeMode = PictureBoxSizeMode.Zoom, BackColor = Color.FromArgb(28, 30, 34) };
    private readonly TextBox _label = new() { Width = 300, MaxLength = 120, AccessibleName = "State label for selected frame" };
    private readonly Label _status = new() { AutoSize = true, MaximumSize = new Size(980, 0) };
    private readonly TextBox _packId = new() { Text = "my-teaching-pack", Width = 200, MaxLength = 64 };
    private readonly TextBox _gameId = new() { Width = 160, MaxLength = 64 };
    private readonly TextBox _title = new() { Text = "My teaching examples", Width = 240, MaxLength = 120 };
    private int _selectedIndex = -1;
    private Point? _regionStart;
    private readonly Dictionary<int, LabelSuggestion> _suggestions = new();
    private readonly Button _acceptSuggestion = new() { Text = "Use suggestion", AutoSize = true, Enabled = false };
    private CancellationTokenSource? _screening;
    private CancellationTokenSource? _tracking;

    public TeachingReviewForm(TeachingRecording recording, string dataRoot, Action<string>? openWorkshop, Action<string>? learnPack = null, Action<TeachingRecording>? checkRecording = null)
    {
        _recording = recording;
        _gameId.Text = GamePackNaming.SuggestGameId(recording.Workspace);
        Text = "Maelstrom · Review and label recording";
        Size = new Size(1100, 820);
        MinimumSize = new Size(900, 720);
        Font = new Font("Segoe UI", 10);
        AutoScaleMode = AutoScaleMode.Dpi;
        var body = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(18), ColumnCount = 1, RowCount = 5 };
        body.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        body.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        body.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        body.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        body.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        body.Controls.Add(new Label { Text = "Teach from what you see\nSelect a frame, name its state, and save the label. Examples: enemy-high, board-ready, round-won.", AutoSize = true, Margin = new Padding(0, 0, 0, 12) });
        var split = new SplitContainer { Size = new Size(1000, 450), Dock = DockStyle.Fill, FixedPanel = FixedPanel.Panel1, SplitterDistance = 230 };
        split.Panel1.Controls.Add(_frames);
        split.Panel2.Controls.Add(_image);
        body.Controls.Add(split);
        var labels = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill };
        labels.Controls.Add(new Label { Text = "State", AutoSize = true, Padding = new Padding(0, 6, 0, 0) });
        labels.Controls.Add(_label);
        var save = new Button { Text = "Save label", AutoSize = true };
        save.Click += (_, _) => Guard(() =>
        {
            var index = _frames.SelectedIndex;
            if (index < 0) return;
            _recording.Label(index, _label.Text);
            RefreshFrames(index);
            _status.Text = "Label saved locally. Clear the text and save to leave a frame unlabeled.";
        });
        labels.Controls.Add(save);
        var suggest = new Button { Text = "Suggest labels", AutoSize = true };
        var motion = new Button { Text = "Suggest motion", AutoSize = true };
        var track = new Button { Text = "Track motion forward", AutoSize = true };
        var transitions = new Button { Text = "Suggest taught transitions", AutoSize = true };
        var cancelTracking = new Button { Text = "Stop tracking", AutoSize = true, Enabled = false };
        cancelTracking.Click += (_, _) => _tracking?.Cancel();
        var accept = _acceptSuggestion;
        var ai = new Button { Text = "Pre-screen recording with AI", AutoSize = true };
        var cancelScreening = new Button { Text = "Stop pre-screening", AutoSize = true, Enabled = false };
        cancelScreening.Click += (_, _) => _screening?.Cancel();
        ai.Click += async (_, _) =>
        {
            if (_screening != null) return;
            var cancellation = new CancellationTokenSource();
            _screening = cancellation;
            try
            {
                SavePendingLabel();
                ai.Enabled = false;
                suggest.Enabled = motion.Enabled = track.Enabled = transitions.Enabled = false;
                _frames.Enabled = _label.Enabled = _image.Enabled = save.Enabled = accept.Enabled = false;
                cancelScreening.Enabled = true;
                _status.Text = "Starting local AI pre-screening · up to 30 model requests with a 0.5 second pause between requests…";
                var result = await VisionScreening.RunAsync(_recording,
                    (index, token) => LocalVision.SuggestAsync(_recording, index, token),
                    suggestion => { if (!IsDisposed) _suggestions[suggestion.Index] = suggestion; },
                    progress => { if (!IsDisposed) _status.Text = $"AI pre-screened {progress.Completed}/{progress.Total} · {progress.ModelRequests} model requests · {progress.Reused} repeats reused · {progress.Rejected} unusable results skipped. Stop at any time."; },
                    cancellation.Token);
                if (!IsDisposed) _status.Text = $"AI pre-screened {result.Completed}/{result.Total} frames using {result.ModelRequests} model requests; {result.Reused} repeats reused and {result.Rejected} unusable results skipped. Each run is limited to 30 model requests. Review suggestions before accepting them.";
            }
            catch (OperationCanceledException) { if (!IsDisposed) _status.Text = "Pre-screening stopped. Completed suggestions remain available for review."; }
            catch (Exception ex) { if (!IsDisposed) MessageBox.Show(this, ex.Message, "Local vision"); }
            finally
            {
                _screening = null;
                cancellation.Dispose();
                if (!IsDisposed)
                {
                    ai.Enabled = suggest.Enabled = motion.Enabled = track.Enabled = transitions.Enabled = true;
                    cancelScreening.Enabled = false;
                    _frames.Enabled = _label.Enabled = _image.Enabled = save.Enabled = true;
                    var message = _status.Text;
                    RefreshFrames(Math.Max(0, _frames.SelectedIndex));
                    _status.Text = message;
                }
            }
        };
        motion.Click += async (_, _) =>
        {
            var index = _frames.SelectedIndex;
            if (index < 1) { _status.Text = "Select a later frame; first mark the target region on the immediately preceding frame."; return; }
            var frames = _recording.Frames;
            var region = frames[index - 1].Region;
            if (region is not { Length: 4 }) { _status.Text = "On the previous frame, drag a rectangle around the object to track; then return to this frame."; return; }
            var interval = frames[index].TimeMs - frames[index - 1].TimeMs;
            if (interval <= 0 || interval > 2000) { _status.Text = "Choose adjacent frames less than two seconds apart."; return; }
            try
            {
                SavePendingLabel();
                motion.Enabled = track.Enabled = transitions.Enabled = suggest.Enabled = ai.Enabled = _frames.Enabled = _image.Enabled = _label.Enabled = save.Enabled = accept.Enabled = false;
                _status.Text = "Comparing the selected region across the two frames…";
                var result = await Task.Run(() =>
                {
                    using var earlier = _recording.ReadFrame(index - 1);
                    using var later = _recording.ReadFrame(index);
                    return MotionSuggestions.Compare(earlier, later, new Rectangle(region[0], region[1], region[2], region[3]));
                });
                if (IsDisposed) return;
                if (result.Label != null && string.IsNullOrWhiteSpace(_recording.Frames[index].State))
                {
                    _suggestions[index] = new(index, result.Label, result.Similarity, result.Margin);
                    RefreshFrames(index);
                }
                _status.Text = $"{result.Reason} Offset: {result.DeltaX}, {result.DeltaY} pixels · match {result.Similarity:P0}; similarity is not confidence.";
            }
            catch (Exception ex) { if (!IsDisposed) _status.Text = "Could not compare motion: " + ex.Message; }
            finally
            {
                if (!IsDisposed)
                {
                    motion.Enabled = track.Enabled = transitions.Enabled = suggest.Enabled = ai.Enabled = _frames.Enabled = _image.Enabled = _label.Enabled = save.Enabled = true;
                    accept.Enabled = _suggestions.ContainsKey(_frames.SelectedIndex) && string.IsNullOrWhiteSpace(_label.Text);
                }
            }
        };
        track.Click += async (_, _) =>
        {
            var index = _frames.SelectedIndex;
            var frames = _recording.Frames;
            if (index < 0 || index >= frames.Count - 1 || frames[index].Region is not { Length: 4 } region)
            {
                _status.Text = "Select a frame with a later frame and drag a rectangle around the object to track.";
                return;
            }
            var cancellation = new CancellationTokenSource();
            _tracking = cancellation;
            try
            {
                SavePendingLabel();
                motion.Enabled = track.Enabled = transitions.Enabled = suggest.Enabled = ai.Enabled = _frames.Enabled = _image.Enabled = _label.Enabled = save.Enabled = accept.Enabled = false;
                cancelTracking.Enabled = true;
                _status.Text = "Tracking selected object across later samples…";
                var progress = new Progress<int>(count => { if (!IsDisposed && count % 10 == 0) _status.Text = $"Compared {count} frames · suggestions require review."; });
                var result = await Task.Run(() => MotionSuggestions.Track(_recording, index, new Rectangle(region[0], region[1], region[2], region[3]), cancellation.Token, progress));
                if (IsDisposed) return;
                foreach (var proposal in result.Suggestions) _suggestions[proposal.Index] = proposal;
                RefreshFrames(index);
                _status.Text = $"{result.Suggestions.Count} screen-motion suggestions across {result.Compared} comparisons. {result.StopReason} Review each suggestion; motion is not proof of an action.";
            }
            catch (OperationCanceledException) { if (!IsDisposed) _status.Text = "Motion tracking stopped; no unfinished batch was applied."; }
            catch (Exception ex) { if (!IsDisposed) _status.Text = "Motion tracking failed: " + ex.Message; }
            finally
            {
                _tracking = null;
                cancellation.Dispose();
                if (!IsDisposed)
                {
                    cancelTracking.Enabled = false;
                    motion.Enabled = track.Enabled = transitions.Enabled = suggest.Enabled = ai.Enabled = _frames.Enabled = _image.Enabled = _label.Enabled = save.Enabled = true;
                    accept.Enabled = _suggestions.ContainsKey(_frames.SelectedIndex) && string.IsNullOrWhiteSpace(_label.Text);
                }
            }
        };
        transitions.Click += async (_, _) =>
        {
            var cancellation = new CancellationTokenSource();
            _tracking = cancellation;
            try
            {
                SavePendingLabel();
                motion.Enabled = track.Enabled = transitions.Enabled = suggest.Enabled = ai.Enabled = _frames.Enabled = _image.Enabled = _label.Enabled = save.Enabled = accept.Enabled = false;
                cancelTracking.Enabled = true;
                _status.Text = "Comparing consecutive frames with your labeled transitions…";
                var proposals = await Task.Run(() => TransitionSuggestions.Suggest(_recording, cancellation.Token));
                if (IsDisposed) return;
                foreach (var proposal in proposals) _suggestions[proposal.Index] = proposal;
                var selected = _frames.SelectedIndex;
                RefreshFrames(Math.Max(0, selected));
                _status.Text = $"{proposals.Count} suggested labels from similar taught transitions. Review each one; same-recording matches do not establish accuracy.";
            }
            catch (OperationCanceledException) { if (!IsDisposed) _status.Text = "Transition comparison stopped; no unfinished batch was applied."; }
            catch (Exception ex) { if (!IsDisposed) _status.Text = "Could not suggest transitions: " + ex.Message; }
            finally
            {
                _tracking = null;
                cancellation.Dispose();
                if (!IsDisposed)
                {
                    cancelTracking.Enabled = false;
                    motion.Enabled = track.Enabled = transitions.Enabled = suggest.Enabled = ai.Enabled = _frames.Enabled = _image.Enabled = _label.Enabled = save.Enabled = true;
                    accept.Enabled = _suggestions.ContainsKey(_frames.SelectedIndex) && string.IsNullOrWhiteSpace(_label.Text);
                }
            }
        };
        suggest.Click += async (_, _) =>
        {
            try
            {
                SavePendingLabel();
                suggest.Enabled = motion.Enabled = track.Enabled = transitions.Enabled = ai.Enabled = _frames.Enabled = _image.Enabled = _label.Enabled = save.Enabled = accept.Enabled = false;
                _status.Text = "Comparing frames with your labeled examples…";
                var proposals = await Task.Run(() => LabelSuggestions.Suggest(_recording));
                if (IsDisposed) return;
                _suggestions.Clear();
                foreach (var proposal in proposals) _suggestions[proposal.Index] = proposal;
                var index = _frames.SelectedIndex;
                RefreshFrames(Math.Max(0, index));
                _status.Text = $"{proposals.Count} suggestions. Select a frame and use or edit its suggested label; no suggestions are saved automatically.";
            }
            catch (Exception ex) { if (!IsDisposed) MessageBox.Show(this, ex.Message, "Label suggestions"); }
            finally
            {
                if (!IsDisposed)
                {
                    suggest.Enabled = motion.Enabled = track.Enabled = transitions.Enabled = ai.Enabled = _frames.Enabled = _image.Enabled = _label.Enabled = save.Enabled = true;
                    accept.Enabled = _suggestions.ContainsKey(_frames.SelectedIndex) && string.IsNullOrWhiteSpace(_label.Text);
                }
            }
        };
        accept.Click += (_, _) => Guard(() =>
        {
            if (!_suggestions.TryGetValue(_frames.SelectedIndex, out var proposal)) return;
            _label.Text = proposal.State;
            _recording.Label(_frames.SelectedIndex, proposal.State);
            _suggestions.Remove(_frames.SelectedIndex);
            RefreshFrames(_frames.SelectedIndex);
            _status.Text = "Suggestion accepted as a reviewed label.";
        });
        labels.Controls.Add(suggest);
        labels.Controls.Add(motion);
        labels.Controls.Add(track);
        labels.Controls.Add(transitions);
        labels.Controls.Add(cancelTracking);
        labels.Controls.Add(ai);
        labels.Controls.Add(cancelScreening);
        labels.Controls.Add(accept);
        labels.Controls.Add(new Label { Text = "Drag over the image to select a template region.", AutoSize = true, Padding = new Padding(8, 6, 0, 0) });
        _image.MouseDown += (_, e) => { if (e.Button == MouseButtons.Left && _image.Image != null) _regionStart = e.Location; };
        _image.MouseUp += (_, e) => Guard(() =>
        {
            if (_regionStart is not Point start || _image.Image == null || _frames.SelectedIndex < 0) return;
            _regionStart = null;
            var image = _image.Image;
            var scale = Math.Min((double)_image.ClientSize.Width / image.Width, (double)_image.ClientSize.Height / image.Height);
            var left = (_image.ClientSize.Width - image.Width * scale) / 2;
            var top = (_image.ClientSize.Height - image.Height * scale) / 2;
            int x1 = Math.Clamp((int)Math.Floor((Math.Min(start.X, e.X) - left) / scale), 0, image.Width);
            int y1 = Math.Clamp((int)Math.Floor((Math.Min(start.Y, e.Y) - top) / scale), 0, image.Height);
            int x2 = Math.Clamp((int)Math.Ceiling((Math.Max(start.X, e.X) - left) / scale), 0, image.Width);
            int y2 = Math.Clamp((int)Math.Ceiling((Math.Max(start.Y, e.Y) - top) / scale), 0, image.Height);
            if (x2 - x1 < 4 || y2 - y1 < 4) { _status.Text = "Select an image region at least 4 pixels wide and high."; return; }
            _recording.SelectRegion(_frames.SelectedIndex, new Rectangle(x1, y1, x2 - x1, y2 - y1));
            _status.Text = $"Saved {x2 - x1} × {y2 - y1} region with this frame. Label it, then save the teaching pack.";
        });
        body.Controls.Add(labels);
        var pack = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, Padding = new Padding(0, 8, 0, 8) };
        foreach (var pair in new[] { ("Pack ID", _packId), ("Game ID", _gameId), ("Title", _title) })
        {
            pack.Controls.Add(new Label { Text = pair.Item1, AutoSize = true, Padding = new Padding(0, 6, 0, 0) });
            pack.Controls.Add(pair.Item2);
        }
        var export = new Button { Text = "Save teaching pack", AutoSize = true };
        var workshop = new Button { Text = "Edit rules in Workshop", AutoSize = true, Enabled = false };
        string? savedPack = null;
        export.Click += (_, _) => Guard(() =>
        {
            SavePendingLabel();
            savedPack = _recording.ExportPack(dataRoot, _packId.Text.Trim(), _gameId.Text.Trim(), _title.Text.Trim());
            workshop.Enabled = openWorkshop != null;
            _status.Text = "Saved a new draft version with labeled images. No rules or visual detector are generated. Add rules in Workshop, then replay the included session.\n" + savedPack;
        });
        workshop.Click += (_, _) => { if (savedPack != null) openWorkshop?.Invoke(Path.Combine(savedPack, "pack.json")); };
        pack.Controls.Add(export);
        pack.Controls.Add(workshop);
        var learn = new Button { Text = "Learn this pack in Observe", AutoSize = true, Enabled = false };
        learn.Click += (_, _) => { if (savedPack != null) { learnPack?.Invoke(Path.Combine(savedPack, "pack.json")); Close(); } };
        export.Click += (_, _) => learn.Enabled = savedPack != null && learnPack != null;
        if (learnPack != null) pack.Controls.Add(learn);
        var plan = GameplaySamplePlan.Load(recording.DirectoryPath);
        if (plan?.Purpose == "check")
        {
            suggest.Visible = false;
            motion.Visible = false;
            track.Visible = false;
            transitions.Visible = false;
            cancelTracking.Visible = false;
            ai.Visible = false;
            cancelScreening.Visible = false;
            accept.Visible = false;
            pack.Controls.Clear();
            var check = new Button { Text = "Check recognition on these labels", AutoSize = true, Enabled = checkRecording != null };
            check.Click += (_, _) => Guard(() => { SavePendingLabel(); checkRecording?.Invoke(recording); Close(); });
            pack.Controls.Add(check);
            pack.Controls.Add(new Label { AutoSize = true, Text = "Independent check · excluded from teaching-pack export in this workflow." });
        }
        body.Controls.Add(pack);
        _status.Text = "Only labeled frames enter the pack. Each export creates a new draft; existing versions and rules stay unchanged.";
        if (plan != null) _status.Text = $"Gameplay Sampler · {plan.Purpose}\nGoal: {plan.Goal}\nLabel each frame from what you see. Check samples do not train the recognizer.";
        try
        {
            var savedSuggestions = ReviewSuggestionsFile.Load(recording);
            foreach (var suggestion in savedSuggestions) _suggestions[suggestion.Index] = suggestion;
            if (savedSuggestions.Count > 0) _status.Text = $"Loaded {savedSuggestions.Count} suggestions. Review each frame before accepting; none are labels yet.";
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            _status.Text = "Could not load review suggestions: " + ex.Message;
        }
        body.Controls.Add(_status);
        Controls.Add(body);
        _frames.SelectedIndexChanged += (_, _) => Guard(() =>
        {
            if (_selectedIndex >= 0) _recording.Label(_selectedIndex, _label.Text);
            _selectedIndex = _frames.SelectedIndex;
            if (_frames.SelectedIndex < 0) return;
            var image = _recording.ReadFrame(_frames.SelectedIndex);
            var old = _image.Image;
            _image.Image = image;
            old?.Dispose();
            _label.Text = _recording.Frames[_frames.SelectedIndex].State;
            accept.Enabled = _suggestions.TryGetValue(_frames.SelectedIndex, out var proposed) && string.IsNullOrWhiteSpace(_label.Text);
            if (accept.Enabled) _status.Text = double.IsNaN(proposed!.Similarity) ? $"{proposed.Source ?? "Review"} suggests: {proposed.State}; compare the image before accepting." : $"{proposed.Source ?? "Suggested"}: {proposed.State} · similarity {proposed.Similarity:P0}; compare the image before accepting.";
        });
        FormClosing += (_, e) => { try { SavePendingLabel(); } catch (Exception ex) { e.Cancel = true; _status.Text = ex.Message; } };
        FormClosed += (_, _) => { _screening?.Cancel(); _tracking?.Cancel(); _image.Image?.Dispose(); };
        RefreshFrames(0);
    }

    private void SavePendingLabel()
    {
        if (_frames.SelectedIndex >= 0) _recording.Label(_frames.SelectedIndex, _label.Text);
    }

    internal void VerifyAcceptSuggestedLabel(int index, string expectedLabel, string expectedGameId)
    {
        if (_gameId.Text != expectedGameId) throw new InvalidOperationException("Review opened with the wrong game ID.");
        _frames.SelectedIndex = index;
        if (!_acceptSuggestion.Enabled || !_suggestions.TryGetValue(index, out var suggestion) || suggestion.State != expectedLabel)
            throw new InvalidOperationException("Review did not display the expected suggestion.");
        _acceptSuggestion.PerformClick();
        if (_recording.Frames[index].State != expectedLabel) throw new InvalidOperationException("Review did not save the accepted label.");
    }

    private void RefreshFrames(int selected)
    {
        _selectedIndex = -1;
        _frames.Items.Clear();
        var frames = _recording.Frames;
        for (var i = 0; i < frames.Count; i++)
        {
            var frame = frames[i];
            var display = frame.State.Length != 0 ? frame.State : _suggestions.TryGetValue(i, out var suggestion) ? $"suggested: {suggestion.State}" : "unlabeled";
            _frames.Items.Add($"{frame.TimeMs / 1000:0.0}s · {display}");
        }
        if (_frames.Items.Count > 0) _frames.SelectedIndex = selected;
    }

    private void Guard(Action action)
    {
        try { action(); }
        catch (Exception ex) { _status.Text = ex.Message; MessageBox.Show(this, ex.Message, "Teaching examples", MessageBoxButtons.OK, MessageBoxIcon.Information); }
    }
}
