using System.ComponentModel;

namespace PlaneGUI.Instruments;

/// <summary>Vertical speed indicator, ±6,000 ft/min, zero at 9 o'clock.</summary>
public sealed class VerticalSpeedIndicator : InstrumentControl
{
    float verticalSpeed;

    /// <summary>Feet per minute, positive = climbing.</summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public float VerticalSpeed { get => verticalSpeed; set => Set(ref verticalSpeed, value); }

    const float Range = 6000f, Sweep = 170f;

    static float DialAngle(float fpm) => -90f + Math.Clamp(fpm, -Range * 1.03f, Range * 1.03f) / Range * Sweep;

    protected override void Draw(Graphics g)
    {
        float size = Math.Min(Width, Height) - 4;
        if (size < 60) return;
        float cx = Width / 2f, cy = Height / 2f, r = size / 2f;
        float face = r * 0.9f;

        DrawBezel(g, cx, cy, r);
        DrawFace(g, cx, cy, face);
        g.TranslateTransform(cx, cy);

        using (var tick = new Pen(Color.White, Math.Max(1f, r * 0.012f)))
        using (var digitFont = new Font(Theme.FontFamily, Math.Max(8f, r * 0.13f), FontStyle.Bold, GraphicsUnit.Pixel))
        {
            for (int fpm = -6000; fpm <= 6000; fpm += 500)
            {
                var s = g.Save();
                g.RotateTransform(DialAngle(fpm));
                bool major = fpm % 1000 == 0;
                g.DrawLine(tick, 0, -face, 0, -face + (major ? face * 0.15f : face * 0.07f));
                g.Restore(s);
                if (major)
                {
                    double a = FlightModel.DegToRad(DialAngle(fpm));
                    float lr = face * 0.7f;
                    DrawCentered(g, Math.Abs(fpm / 1000).ToString(), digitFont, Brushes.White, (float)(Math.Sin(a) * lr), (float)(-Math.Cos(a) * lr));
                }
            }
        }

        using (var titleFont = new Font(Theme.FontFamily, Math.Max(7f, r * 0.075f), FontStyle.Bold, GraphicsUnit.Pixel))
        using (var muted = new SolidBrush(Theme.Muted))
        {
            DrawCentered(g, "UP", titleFont, muted, -face * 0.3f, -face * 0.13f);
            DrawCentered(g, "DN", titleFont, muted, -face * 0.3f, face * 0.13f);
            DrawCentered(g, "VERTICAL SPEED", titleFont, muted, face * 0.1f, -face * 0.28f);
            DrawCentered(g, "×1000 FT/MIN", titleFont, muted, face * 0.1f, face * 0.28f);
        }

        DrawPointer(g, DialAngle(verticalSpeed), face * 0.86f, r * 0.03f, Color.White);
        float hub = r * 0.06f;
        using (var hubBrush = new SolidBrush(Color.FromArgb(90, 93, 100)))
            g.FillEllipse(hubBrush, -hub, -hub, hub * 2, hub * 2);
        g.ResetTransform();
    }
}
