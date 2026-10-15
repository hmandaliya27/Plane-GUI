namespace PlaneGUI.Instruments;

/// <summary>Scrolling line chart of recent altitude samples.</summary>
public sealed class AltitudeChart : InstrumentControl
{
    readonly Queue<float> samples = new();

    public int Capacity { get; set; } = 240;

    public void Add(float altitude)
    {
        samples.Enqueue(altitude);
        while (samples.Count > Capacity) samples.Dequeue();
        Invalidate();
    }

    public void Clear()
    {
        samples.Clear();
        Invalidate();
    }

    protected override void Draw(Graphics g)
    {
        using var font = new Font(Theme.MonoFontFamily, Math.Clamp(Math.Min(Height / 180f, Width / 700f) * 12f, 8f, 28f), GraphicsUnit.Pixel);
        using var muted = new SolidBrush(Theme.Muted);
        using var grid = new Pen(Theme.PanelBorder);

        float left = g.MeasureString("00,000 ft", font).Width + 8, pad = font.Height / 2f + 2;
        var plot = new RectangleF(left, pad, Width - left - pad, Height - pad * 2);
        if (plot.Width < 20 || plot.Height < 20) return;

        float min = samples.Count > 0 ? samples.Min() : 0;
        float max = samples.Count > 0 ? samples.Max() : 100;
        // Keep at least a 100 ft span and round to tidy gridlines.
        float mid = (min + max) / 2;
        float span = Math.Max(100, max - min) * 1.2f;
        float step = NiceStep(span / 4);
        float lo = (float)Math.Floor((mid - span / 2) / step) * step;
        float hi = (float)Math.Ceiling((mid + span / 2) / step) * step;
        if (lo < 0 && min >= 0) { hi -= lo; lo = 0; }

        float Y(float v) => plot.Bottom - (v - lo) / (hi - lo) * plot.Height;

        for (float v = lo; v <= hi + step / 2; v += step)
        {
            float y = Y(v);
            g.DrawLine(grid, plot.Left, y, plot.Right, y);
            string label = $"{v:#,0} ft";
            var sz = g.MeasureString(label, font);
            g.DrawString(label, font, muted, plot.Left - sz.Width - 4, y - sz.Height / 2);
        }

        if (samples.Count < 2) return;
        float dx = plot.Width / (Capacity - 1);
        float x0 = plot.Right - (samples.Count - 1) * dx;
        var points = samples.Select((v, i) => new PointF(x0 + i * dx, Y(v))).ToArray();
        using var line = new Pen(Theme.Accent, 2f);
        g.DrawLines(line, points);
    }

    static float NiceStep(float raw)
    {
        double mag = Math.Pow(10, Math.Floor(Math.Log10(raw)));
        double norm = raw / mag;
        double nice = norm < 1.5 ? 1 : norm < 3 ? 2 : norm < 7 ? 5 : 10;
        return (float)(nice * mag);
    }
}
