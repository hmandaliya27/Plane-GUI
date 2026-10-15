using System.ComponentModel;

namespace PlaneGUI.Instruments;

/// <summary>
/// Horizontal bar with a title, value readout, optional scale labels and a warning zone.
/// With <see cref="Interactive"/> set it becomes a lever you can drag (used for the throttle).
/// Text scales with the control's size.
/// </summary>
public sealed class BarGauge : InstrumentControl
{
    float value;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public float Value { get => value; set => Set(ref this.value, Math.Clamp(value, Minimum, Maximum)); }

    public string Title { get; set; } = "";
    public string Format { get; set; } = "0";
    public string Unit { get; set; } = "";
    public float Minimum { get; set; }
    public float Maximum { get; set; } = 100;
    /// <summary>Bar turns red at or below this value (fuel).</summary>
    public float? WarnBelow { get; set; }
    /// <summary>Bar turns red at or above this value (temperature).</summary>
    public float? WarnAbove { get; set; }
    /// <summary>Optional labels spread evenly under the bar, e.g. Empty / ½ / Full.</summary>
    public string[] ScaleLabels { get; set; } = Array.Empty<string>();
    /// <summary>When true the user can click / drag the bar to set the value.</summary>
    public bool Interactive { get; set; }

    public event EventHandler? ValueChanged;

    protected override void OnMouseEnter(EventArgs e)
    {
        base.OnMouseEnter(e);
        Cursor = Interactive ? Cursors.Hand : Cursors.Default;
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (Interactive && e.Button == MouseButtons.Left) SetFromMouse(e.X);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (Interactive && e.Button == MouseButtons.Left) SetFromMouse(e.X);
    }

    void SetFromMouse(int x)
    {
        float frac = Math.Clamp(x / (float)Math.Max(1, Width - 1), 0f, 1f);
        Value = Minimum + frac * (Maximum - Minimum);
        ValueChanged?.Invoke(this, EventArgs.Empty);
    }

    protected override void Draw(Graphics g)
    {
        // Designed at roughly 290 x 80 px; scale text to the space we actually have.
        float u = Math.Clamp(Math.Min(Height / 80f, Width / 290f), 0.45f, 3f);
        using var titleFont = new Font(Theme.FontFamily, 12f * u, FontStyle.Bold, GraphicsUnit.Pixel);
        using var valueFont = new Font(Theme.MonoFontFamily, 16f * u, FontStyle.Bold, GraphicsUnit.Pixel);
        using var scaleFont = new Font(Theme.FontFamily, 11f * u, GraphicsUnit.Pixel);
        using var muted = new SolidBrush(Theme.Muted);

        bool warn = (WarnBelow is float lo && value <= lo) || (WarnAbove is float hi && value >= hi);
        using var text = new SolidBrush(warn ? Theme.Bad : Theme.Text);

        string readout = value.ToString(Format) + Unit;
        var sz = g.MeasureString(readout, valueFont);
        g.DrawString(Title.ToUpperInvariant(), titleFont, muted, 0, (sz.Height - titleFont.GetHeight(g)) / 2);
        g.DrawString(readout, valueFont, text, Width - sz.Width, 0);

        float top = sz.Height + 3 * u;
        float scaleH = ScaleLabels.Length > 0 ? scaleFont.GetHeight(g) + 2 : 0;
        var track = new RectangleF(0, top, Width - 1, Math.Max(6, Height - top - 3 - scaleH));

        for (int i = 0; i < ScaleLabels.Length; i++)
        {
            var ls = g.MeasureString(ScaleLabels[i], scaleFont);
            float at = ScaleLabels.Length == 1 ? 0 : i / (float)(ScaleLabels.Length - 1);
            float x = Math.Clamp(track.Width * at - ls.Width / 2, 0, Math.Max(0, Width - ls.Width));
            g.DrawString(ScaleLabels[i], scaleFont, muted, x, track.Bottom + 2);
        }

        using (var trackBrush = new SolidBrush(Theme.BezelDark)) g.FillRectangle(trackBrush, track);

        float frac = Math.Clamp((value - Minimum) / (Maximum - Minimum), 0f, 1f);
        Color fillColor = warn ? Theme.Bad : Interactive ? Color.FromArgb(70, 140, 230) : Theme.Good;
        if (frac > 0)
            using (var fill = new SolidBrush(fillColor))
                g.FillRectangle(fill, track.X, track.Y, track.Width * frac, track.Height);

        float? limit = WarnBelow ?? WarnAbove;
        if (limit is float l)
        {
            float x = track.X + track.Width * Math.Clamp((l - Minimum) / (Maximum - Minimum), 0f, 1f);
            using var pen = new Pen(Theme.Accent, 2f);
            g.DrawLine(pen, x, track.Top - 2, x, track.Bottom + 2);
        }

        if (Interactive)
        {
            // Lever handle
            float hw = Math.Max(6f, 10f * u), x = track.X + track.Width * frac;
            x = Math.Clamp(x, hw / 2, Width - 1 - hw / 2);
            using var handle = new SolidBrush(Color.White);
            g.FillRectangle(handle, x - hw / 2, track.Top - 3, hw, track.Height + 6);
        }

        using (var border = new Pen(Theme.PanelBorder)) g.DrawRectangle(border, track.X, track.Y, track.Width, track.Height);
    }
}
