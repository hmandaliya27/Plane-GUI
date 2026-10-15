namespace PlaneGUI.Instruments;

/// <summary>Grid of caution / warning lights that glow when active.</summary>
public sealed class Annunciator : InstrumentControl
{
    readonly List<(string Text, bool Warning)> lights = new();
    readonly HashSet<string> active = new();

    public int Columns { get; set; } = 3;

    /// <param name="warning">true = red warning, false = amber caution</param>
    public void Define(string text, bool warning) => lights.Add((text, warning));

    public void SetActive(string text, bool on)
    {
        if (on ? active.Add(text) : active.Remove(text)) Invalidate();
    }

    protected override void Draw(Graphics g)
    {
        if (lights.Count == 0) return;
        int rows = (lights.Count + Columns - 1) / Columns;
        float gap = Math.Max(3f, Width / 100f);
        float w = (Width - gap * (Columns - 1)) / Columns;
        float h = (Height - gap * (rows - 1)) / rows;
        if (w < 10 || h < 8) return;

        using var font = new Font(Theme.FontFamily, Math.Max(6f, Math.Min(h * 0.36f, w * 0.12f)), FontStyle.Bold, GraphicsUnit.Pixel);
        using var dimText = new SolidBrush(Color.FromArgb(90, 95, 108));
        using var litText = new SolidBrush(Color.Black);
        using var off = new SolidBrush(Theme.BezelDark);
        using var red = new SolidBrush(Theme.Bad);
        using var amber = new SolidBrush(Color.FromArgb(255, 170, 0));
        using var border = new Pen(Theme.PanelBorder);
        using var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };

        for (int i = 0; i < lights.Count; i++)
        {
            var (text, warning) = lights[i];
            var rect = new RectangleF((i % Columns) * (w + gap), (i / Columns) * (h + gap), w, h);
            bool on = active.Contains(text);
            g.FillRectangle(on ? (warning ? red : amber) : off, rect);
            g.DrawRectangle(border, rect.X, rect.Y, rect.Width, rect.Height);
            g.DrawString(text, font, on ? litText : dimText, rect, format);
        }
    }
}
