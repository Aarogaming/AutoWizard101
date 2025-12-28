using System.Drawing.Drawing2D;

namespace ProjectMaelstrom.Utilities;

internal static class UIStyles
{
    public static void ApplyCardStyle(Control control, Color back, Color border)
    {
        control.BackColor = back;
        control.Paint += (s, e) =>
        {
            using var pen = new Pen(border, 1);
            pen.Alignment = PenAlignment.Inset;
            e.Graphics.DrawRectangle(pen, 0, 0, control.Width - 1, control.Height - 1);
        };
    }
}
