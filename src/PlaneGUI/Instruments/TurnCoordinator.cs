using System.ComponentModel;
using System.Drawing.Drawing2D;

namespace PlaneGUI.Instruments;

/// <summary>
/// Turn coordinator: the little aeroplane tilts with rate of turn (lined up with
/// the L/R marks = standard rate, 3°/s, a 2-minute full circle) and the ball shows slip/skid.
/// </summary>
public sealed class TurnCoordinator : InstrumentControl
{
    float turnRate, slip;

    /// <summary>Degrees per second, positive = turning right.</summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public float TurnRate { get => turnRate; set => Set(ref turnRate, value); }

    /// <summary>Ball position, -1 (full left) .. +1 (full right).</summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public float Slip { get => slip; set => Set(ref slip, Math.Clamp(value, -1f, 1f)); }

    const float StandardRateTilt = 20f; // symbol tilt at 3°/s

    protected override void Draw(Graphics g)
    {
        float size = Math.Min(Width, Height) - 4;
        if (size < 60) return;
        float cx = Width / 2f, cy = Height / 2f, r = size / 2f;
        float face = r * 0.9f;

        DrawBezel(g, cx, cy, r);
        DrawFace(g, cx, cy, face);
        g.TranslateTransform(cx, cy);

        // Wings-level and standard-rate marks on both sides
        using (var mark = new Pen(Color.White, r * 0.03f))
        {
            foreach (float a in new[] { -90f, -90f - StandardRateTilt, 90f, 90f + StandardRateTilt })
            {
                var s = g.Save();
                g.RotateTransform(a);
                g.DrawLine(mark, 0, -face * 0.95f, 0, -face * 0.75f);
                g.Restore(s);
            }
        }

        using (var labelFont = new Font(Theme.FontFamily, Math.Max(8f, r * 0.13f), FontStyle.Bold, GraphicsUnit.Pixel))
        {
            DrawCentered(g, "L", labelFont, Brushes.White, -face * 0.6f, face * 0.62f);
            DrawCentered(g, "R", labelFont, Brushes.White, face * 0.6f, face * 0.62f);
        }
        using (var titleFont = new Font(Theme.FontFamily, Math.Max(7f, r * 0.075f), FontStyle.Bold, GraphicsUnit.Pixel))
        using (var muted = new SolidBrush(Theme.Muted))
        {
            DrawCentered(g, "TURN COORDINATOR", titleFont, muted, 0, -face * 0.55f);
            DrawCentered(g, "2 MIN", titleFont, muted, 0, face * 0.78f);
        }

        // Aeroplane symbol, tilted by rate of turn
        var ps = g.Save();
        g.RotateTransform(Math.Clamp(turnRate / 3f * StandardRateTilt, -45f, 45f));
        using (var wing = new Pen(Color.White, r * 0.05f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
        {
            g.DrawLine(wing, -face * 0.68f, 0, face * 0.68f, 0);
            g.DrawLine(wing, 0, 0, 0, -face * 0.18f);
        }
        float body = r * 0.09f;
        g.FillEllipse(Brushes.White, -body, -body, body * 2, body * 2);
        g.Restore(ps);

        // Inclinometer (ball in a tube)
        var tube = new RectangleF(-face * 0.45f, face * 0.36f, face * 0.9f, face * 0.2f);
        using (var path = RoundedRect(tube, tube.Height / 2))
        {
            using var glass = new SolidBrush(Color.FromArgb(225, 228, 232));
            g.FillPath(glass, path);
        }
        float ballR = tube.Height * 0.42f;
        using (var refPen = new Pen(Color.FromArgb(60, 60, 60), Math.Max(1f, r * 0.012f)))
        {
            g.DrawLine(refPen, -ballR * 1.1f, tube.Top, -ballR * 1.1f, tube.Bottom);
            g.DrawLine(refPen, ballR * 1.1f, tube.Top, ballR * 1.1f, tube.Bottom);
        }
        float bx = slip * (tube.Width / 2 - ballR * 1.1f);
        using (var ball = new SolidBrush(Color.FromArgb(20, 20, 22)))
            g.FillEllipse(ball, bx - ballR, tube.Top + tube.Height / 2 - ballR, ballR * 2, ballR * 2);

        g.ResetTransform();
    }

    static GraphicsPath RoundedRect(RectangleF r, float radius)
    {
        var p = new GraphicsPath();
        float d = radius * 2;
        p.AddArc(r.X, r.Y, d, d, 180, 90);
        p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }
}
