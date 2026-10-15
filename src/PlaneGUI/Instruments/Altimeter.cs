using System.ComponentModel;

namespace PlaneGUI.Instruments;

/// <summary>Three-pointer altimeter (100s, 1,000s and 10,000s of feet) with a digital window.</summary>
public sealed class Altimeter : InstrumentControl
{
    float altitude;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public float Altitude { get => altitude; set => Set(ref altitude, value); }

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
        using (var digitFont = new Font(Theme.FontFamily, Math.Max(8f, r * 0.15f), FontStyle.Bold, GraphicsUnit.Pixel))
        {
            for (int i = 0; i < 50; i++) // a tick every 20 ft
            {
                var s = g.Save();
                g.RotateTransform(i * 7.2f);
                bool major = i % 5 == 0;
                g.DrawLine(tick, 0, -face, 0, -face + (major ? face * 0.15f : face * 0.07f));
                g.Restore(s);
                if (major)
                {
                    double a = FlightModel.DegToRad(i * 7.2);
                    float lr = face * 0.7f;
                    DrawCentered(g, (i / 5).ToString(), digitFont, Brushes.White, (float)(Math.Sin(a) * lr), (float)(-Math.Cos(a) * lr));
                }
            }
        }

        using (var titleFont = new Font(Theme.FontFamily, Math.Max(7f, r * 0.075f), FontStyle.Bold, GraphicsUnit.Pixel))
        using (var muted = new SolidBrush(Theme.Muted))
        {
            DrawCentered(g, "ALT", titleFont, muted, 0, -face * 0.4f);
            DrawCentered(g, "FEET", titleFont, muted, 0, -face * 0.28f);
        }

        // Digital window
        var window = new RectangleF(-face * 0.36f, face * 0.24f, face * 0.72f, face * 0.24f);
        using (var bg = new SolidBrush(Theme.BezelDark)) g.FillRectangle(bg, window);
        using (var border = new Pen(Theme.PanelBorder)) g.DrawRectangle(border, window.X, window.Y, window.Width, window.Height);
        using (var readoutFont = new Font(Theme.MonoFontFamily, Math.Max(8f, r * 0.12f), FontStyle.Bold, GraphicsUnit.Pixel))
            DrawCentered(g, $"{altitude:#,0}", readoutFont, Brushes.White, 0, window.Y + window.Height / 2);

        float alt = Math.Max(0f, altitude);
        // 10,000s: thin pointer with a triangle at the rim
        DrawPointer(g, alt / 100000f * 360f, face * 0.95f, r * 0.012f, Theme.Muted, face * 0.08f);
        // 1,000s: short, wide hand
        DrawPointer(g, alt / 10000f * 360f, face * 0.5f, r * 0.05f, Color.White);
        // 100s: long hand
        DrawPointer(g, alt / 1000f * 360f, face * 0.84f, r * 0.03f, Color.White);

        float hub = r * 0.06f;
        using (var hubBrush = new SolidBrush(Color.FromArgb(90, 93, 100)))
            g.FillEllipse(hubBrush, -hub, -hub, hub * 2, hub * 2);
        g.ResetTransform();
    }
}
