using System.Drawing.Drawing2D;
using System.Drawing.Text;

namespace PlaneGUI.Instruments;

/// <summary>Double-buffered, owner-drawn base for all gauges.</summary>
public abstract class InstrumentControl : Control
{
    protected InstrumentControl()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.UserPaint |
                 ControlStyles.ResizeRedraw, true);
        BackColor = Theme.Panel;
        ForeColor = Theme.Text;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
        g.Clear(BackColor);
        Draw(g);
    }

    protected abstract void Draw(Graphics g);

    /// <summary>Sets a float property and repaints only if it actually changed.</summary>
    protected void Set(ref float field, float value)
    {
        if (Math.Abs(field - value) < 0.01f) return;
        field = value;
        Invalidate();
    }

    protected static void DrawCentered(Graphics g, string text, Font font, Brush brush, float x, float y)
    {
        var size = g.MeasureString(text, font);
        g.DrawString(text, font, brush, x - size.Width / 2f, y - size.Height / 2f);
    }

    protected static void DrawFace(Graphics g, float cx, float cy, float face)
    {
        using var brush = new SolidBrush(Color.FromArgb(38, 40, 46));
        g.FillEllipse(brush, cx - face, cy - face, face * 2, face * 2);
    }

    /// <summary>Draws a tapered pointer from the (already translated) centre at a dial angle (clockwise from 12 o'clock).</summary>
    protected static void DrawPointer(Graphics g, float angle, float length, float width, Color color, float tipTriangle = 0)
    {
        var s = g.Save();
        g.RotateTransform(angle);
        using var brush = new SolidBrush(color);
        g.FillPolygon(brush, new[]
        {
            new PointF(-width, length * 0.12f), new PointF(-width * 0.6f, -length * 0.9f),
            new PointF(0, -length), new PointF(width * 0.6f, -length * 0.9f), new PointF(width, length * 0.12f),
        });
        if (tipTriangle > 0)
            g.FillPolygon(brush, new[] { new PointF(0, -length + tipTriangle), new PointF(-tipTriangle * 0.6f, -length), new PointF(tipTriangle * 0.6f, -length) });
        g.Restore(s);
    }

    protected static void DrawBezel(Graphics g, float cx, float cy, float r)
    {
        using var bezel = new LinearGradientBrush(
            new RectangleF(cx - r, cy - r, r * 2, r * 2), Theme.Bezel, Theme.BezelDark, 90f);
        g.FillEllipse(bezel, cx - r, cy - r, r * 2, r * 2);
        using var rim = new Pen(Color.FromArgb(70, 73, 80), 2f);
        g.DrawEllipse(rim, cx - r, cy - r, r * 2, r * 2);
    }
}
