namespace ProjectMaelstrom.Utilities;

/// <summary>Renders owned forms to saved bitmaps without showing, activating,
/// attaching to, capturing or sending input to any desktop window.</summary>
internal static class PetTrainingUiReview
{
    public static void Run(string output)
    {
        if (Directory.Exists(output)) throw new IOException("Choose a new UI review folder.");
        Directory.CreateDirectory(output);
        using var player = new PetDancePlayerForm(Path.Combine(output, "isolated-settings"));
        using var settings = new PetTrainingPreferencesForm(new());
        using var dodge = new DodgeWorkspaceForm(Path.Combine(output, "isolated-settings"));
        Render(player, Path.Combine(output, "pet-dance.png"));
        Render(settings, Path.Combine(output, "pet-settings.png"));
        Render(dodge, Path.Combine(output, "dodge-workspace.png"));
        GameExperiments.WriteAtomic(Path.Combine(output, "review.json"), new
        { inputs_sent = 0, windows_shown = 0, game_capture_started = false, kind = "owned-form-offline-layout-review" });
    }

    internal static void Render(Form form, string path)
    {
        void Prepare(Control control)
        {
            _ = control.Handle;
            foreach (Control child in control.Controls) Prepare(child);
            control.PerformLayout();
        }
        Prepare(form);
        using var bitmap = new Bitmap(form.ClientSize.Width, form.ClientSize.Height);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.Clear(form.BackColor);
        void PaintChildren(Control parent, Point offset)
        {
            var saved = graphics.Save();
            graphics.SetClip(new Rectangle(offset, parent.ClientSize), System.Drawing.Drawing2D.CombineMode.Intersect);
            foreach (Control child in parent.Controls.Cast<Control>().Reverse())
            {
                if (child.Width <= 0 || child.Height <= 0) continue;
                var location = new Point(offset.X + child.Left, offset.Y + child.Top);
                using var rendered = new Bitmap(child.Width, child.Height);
                child.DrawToBitmap(rendered, new Rectangle(Point.Empty, child.Size));
                graphics.DrawImageUnscaled(rendered, location);
                PaintChildren(child, location);
            }
            graphics.Restore(saved);
        }
        PaintChildren(form, Point.Empty);
        bitmap.Save(path);
    }
}
