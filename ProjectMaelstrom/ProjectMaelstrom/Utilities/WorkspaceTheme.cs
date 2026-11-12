namespace ProjectMaelstrom.Utilities;

/// <summary>Small shared workspace skin. Reuses the configured dark palette when
/// available without changing the application's global theme preference.</summary>
internal static class WorkspaceTheme
{
    internal static ThemeManager.ThemePalette Palette
    {
        get
        {
            var configured = ThemeManager.GetActivePalette();
            return configured.Back.GetBrightness() < 0.5f ? configured : new ThemeManager.ThemePalette
            {
                Back = Color.FromArgb(24, 27, 34), Surface = Color.FromArgb(30, 34, 43),
                ControlBack = Color.FromArgb(40, 46, 58), Border = Color.FromArgb(73, 84, 104),
                Accent = Color.FromArgb(126, 190, 255), AccentAlt = Color.FromArgb(55, 79, 107),
                Fore = Color.FromArgb(234, 239, 247), ControlFore = Color.FromArgb(234, 239, 247),
                TextMuted = Color.FromArgb(179, 190, 207)
            };
        }
    }

    internal static void Apply(Form form)
    {
        var palette = Palette;
        form.BackColor = palette.Back;
        form.ForeColor = palette.Fore;
        void ApplyControl(Control control)
        {
            control.ForeColor = palette.Fore;
            control.BackColor = control is TextBoxBase or ComboBox or NumericUpDown ? palette.ControlBack : palette.Surface;
            if (control is ComboBox combo && combo.DropDownStyle == ComboBoxStyle.DropDownList)
            {
                combo.FlatStyle = FlatStyle.Flat;
                combo.DrawMode = DrawMode.OwnerDrawFixed;
                combo.DrawItem -= DrawChoice;
                combo.DrawItem += DrawChoice;
            }
            if (control is Button button)
            {
                button.UseVisualStyleBackColor = false;
                button.BackColor = palette.ControlBack;
                button.FlatStyle = FlatStyle.Flat;
                button.FlatAppearance.BorderColor = palette.Border;
                button.FlatAppearance.MouseOverBackColor = palette.AccentAlt;
                button.Padding = new Padding(8, 5, 8, 5);
            }
            foreach (Control child in control.Controls) ApplyControl(child);
        }
        foreach (Control control in form.Controls) ApplyControl(control);
    }

    private static void DrawChoice(object? sender, DrawItemEventArgs args)
    {
        if (sender is not ComboBox combo) return;
        using var brush = new SolidBrush(combo.BackColor);
        args.Graphics.FillRectangle(brush, args.Bounds);
        var item = args.Index >= 0 && args.Index < combo.Items.Count ? combo.Items[args.Index] : combo.SelectedItem;
        TextRenderer.DrawText(args.Graphics, item == null ? "" : combo.GetItemText(item), combo.Font,
            Rectangle.Inflate(args.Bounds, -4, 0), combo.ForeColor,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        args.DrawFocusRectangle();
    }
}
