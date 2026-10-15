using System.ComponentModel;
using System.Drawing.Drawing2D;

namespace PlaneGUI.Instruments;

/// <summary>
/// Artificial horizon: sky/ground card that rolls and pitches behind a fixed
/// aircraft symbol, with a pitch ladder and bank-angle scale.
/// </summary>
public sealed class AttitudeIndicator : InstrumentControl
{
    float roll, pitch;

    /// <summary>Bank angle in degrees, positive = right wing down.</summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public float Roll { get => roll; set => Set(ref roll, value); }

    /// <summary>Pitch angle in degrees, positive = nose up.</summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public float Pitch { get => pitch; set => Set(ref pitch, Math.Clamp(value, -90f, 90f)); }

    static readonly int[] BankMarks = { -60, -45, -30, -20, -10, 10, 20, 30, 45, 60 };

    protected override void Draw(Graphics g)
    {
        float size = Math.Min(Width, Height) - 4;
        if (size < 60) return;
        float cx = Width / 2f, cy = Height / 2f, r = size / 2f;
        float inner = r * 0.84f;     // radius of the moving horizon window
        float ppd = inner / 35f;     // pixels per degree: about ±35° visible

        DrawBezel(g, cx, cy, r);

        // ---- Moving card: sky, ground, horizon line, pitch ladder ----
        var saved = g.Save();
        using (var clip = new GraphicsPath())
        {
            clip.AddEllipse(cx - inner, cy - inner, inner * 2, inner * 2);
            g.SetClip(clip);
        }
        g.TranslateTransform(cx, cy);
        g.RotateTransform(-roll);             // GDI+ rotates clockwise, so bank right = card turns anticlockwise
        g.TranslateTransform(0, pitch * ppd); // nose up = horizon moves down

        float ext = inner * 2 + 90 * ppd;     // big enough to cover the window at any attitude
        using (var sky = new SolidBrush(Theme.Sky)) g.FillRectangle(sky, -ext, -ext, ext * 2, ext);
        using (var ground = new SolidBrush(Theme.Ground)) g.FillRectangle(ground, -ext, 0, ext * 2, ext);
        using (var horizon = new Pen(Color.White, Math.Max(2f, r * 0.012f))) g.DrawLine(horizon, -ext, 0, ext, 0);

        using (var ladder = new Pen(Color.White, Math.Max(1.5f, r * 0.008f)))
        using (var font = new Font(Theme.FontFamily, Math.Max(7f, r * 0.055f), FontStyle.Bold, GraphicsUnit.Pixel))
        {
            for (int deg = -90; deg <= 90; deg += 5)
            {
                if (deg == 0) continue;
                float y = -deg * ppd;
                bool major = deg % 10 == 0;
                float half = major ? inner * 0.26f : inner * 0.12f;
                g.DrawLine(ladder, -half, y, half, y);
                if (major)
                {
                    string label = Math.Abs(deg).ToString();
                    var sz = g.MeasureString(label, font);
                    g.DrawString(label, font, Brushes.White, -half - sz.Width - 2, y - sz.Height / 2);
                    g.DrawString(label, font, Brushes.White, half + 2, y - sz.Height / 2);
                }
            }
        }
        g.Restore(saved);

        // ---- Fixed bank scale ----
        g.TranslateTransform(cx, cy);
        using (var tick = new Pen(Color.White, Math.Max(1.5f, r * 0.012f)))
        {
            g.DrawArc(tick, -inner, -inner, inner * 2, inner * 2, -150, 120);
            foreach (int a in BankMarks)
            {
                var s = g.Save();
                g.RotateTransform(a);
                float len = Math.Abs(a) % 30 == 0 ? r * 0.12f : r * 0.07f;
                g.DrawLine(tick, 0, -inner, 0, -inner - len);
                g.Restore(s);
            }
        }
        // Zero-bank index (fixed, white, pointing down at the top)
        float tw = r * 0.06f;
        using (var white = new SolidBrush(Color.White))
            g.FillPolygon(white, new[] { new PointF(0, -inner), new PointF(-tw, -inner - tw * 1.6f), new PointF(tw, -inner - tw * 1.6f) });

        // Bank pointer (moves with the card)
        var ps = g.Save();
        g.RotateTransform(-roll);
        using (var accent = new SolidBrush(Theme.Accent))
            g.FillPolygon(accent, new[] { new PointF(0, -inner + 2), new PointF(-tw, -inner + tw * 1.7f), new PointF(tw, -inner + tw * 1.7f) });
        g.Restore(ps);
        g.ResetTransform();

        // ---- Fixed aircraft symbol ----
        float wing = inner * 0.55f, gap = inner * 0.16f, drop = inner * 0.08f;
        using (var shadow = new Pen(Color.FromArgb(120, 0, 0, 0), r * 0.05f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
        using (var plane = new Pen(Theme.Accent, r * 0.035f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
        {
            foreach (var pen in new[] { shadow, plane })
            {
                g.DrawLines(pen, new[] { new PointF(cx - wing, cy), new PointF(cx - gap, cy), new PointF(cx - gap, cy + drop) });
                g.DrawLines(pen, new[] { new PointF(cx + wing, cy), new PointF(cx + gap, cy), new PointF(cx + gap, cy + drop) });
            }
        }
        float dot = r * 0.035f;
        using (var accent = new SolidBrush(Theme.Accent)) g.FillEllipse(accent, cx - dot, cy - dot, dot * 2, dot * 2);

        using (var ring = new Pen(Theme.BezelDark, 3f)) g.DrawEllipse(ring, cx - inner, cy - inner, inner * 2, inner * 2);
    }
}
