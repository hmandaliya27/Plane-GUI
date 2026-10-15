using System.ComponentModel;
using System.Drawing.Drawing2D;

namespace PlaneGUI.Instruments;

/// <summary>Round airspeed dial (0–800 kt) with green / amber arcs and a red never-exceed line.</summary>
public sealed class AirspeedIndicator : InstrumentControl
{
    float airspeed;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public float Airspeed { get => airspeed; set => Set(ref airspeed, Math.Max(0f, value)); }

    public float MaxSpeed { get; set; } = 800f;
    public float GreenFrom { get; set; } = 130f;  // normal operating range
    public float AmberFrom { get; set; } = 450f;  // caution range
    public float NeverExceed { get; set; } = 580f;

    const float SweepStart = -160f, SweepRange = 320f; // degrees clockwise from 12 o'clock

    float DialAngle(float kt) => SweepStart + Math.Clamp(kt / MaxSpeed, 0f, 1.05f) * SweepRange;
    static float Gdi(float dialAngle) => dialAngle - 90f; // GDI+ arcs start at 3 o'clock

    protected override void Draw(Graphics g)
    {
        float size = Math.Min(Width, Height) - 4;
        if (size < 60) return;
        float cx = Width / 2f, cy = Height / 2f, r = size / 2f;
        float face = r * 0.9f;

        DrawBezel(g, cx, cy, r);
        using (var faceBrush = new SolidBrush(Color.FromArgb(38, 40, 46)))
            g.FillEllipse(faceBrush, cx - face, cy - face, face * 2, face * 2);

        // Coloured speed ranges
        float arcR = face * 0.9f, arcW = r * 0.06f;
        var arcRect = new RectangleF(cx - arcR, cy - arcR, arcR * 2, arcR * 2);
        void Arc(Color c, float from, float to)
        {
            using var pen = new Pen(c, arcW);
            g.DrawArc(pen, arcRect, Gdi(DialAngle(from)), DialAngle(to) - DialAngle(from));
        }
        Arc(Theme.Good, GreenFrom, AmberFrom);
        Arc(Color.FromArgb(255, 160, 0), AmberFrom, NeverExceed);

        g.TranslateTransform(cx, cy);
        using (var tick = new Pen(Color.White, Math.Max(1f, r * 0.012f)))
        using (var red = new Pen(Theme.Bad, r * 0.03f))
        using (var labelFont = new Font(Theme.FontFamily, Math.Max(8f, r * 0.1f), FontStyle.Bold, GraphicsUnit.Pixel))
        {
            for (int kt = 0; kt <= MaxSpeed; kt += 20)
            {
                var s = g.Save();
                g.RotateTransform(DialAngle(kt));
                bool major = kt % 100 == 0;
                g.DrawLine(tick, 0, -face, 0, -face + (major ? face * 0.16f : face * 0.08f));
                g.Restore(s);
                if (major && kt > 0)
                {
                    double a = FlightModel.DegToRad(DialAngle(kt));
                    float lr = face * 0.66f;
                    DrawCentered(g, kt.ToString(), labelFont, Brushes.White, (float)(Math.Sin(a) * lr), (float)(-Math.Cos(a) * lr));
                }
            }
            var vs = g.Save();
            g.RotateTransform(DialAngle(NeverExceed));
            g.DrawLine(red, 0, -face, 0, -face + face * 0.2f);
            g.Restore(vs);
        }

        using (var titleFont = new Font(Theme.FontFamily, Math.Max(7f, r * 0.08f), FontStyle.Bold, GraphicsUnit.Pixel))
        using (var muted = new SolidBrush(Theme.Muted))
        {
            DrawCentered(g, "AIRSPEED", titleFont, muted, 0, -face * 0.3f);
            DrawCentered(g, "KNOTS", titleFont, muted, 0, -face * 0.18f);
        }
        using (var readoutFont = new Font(Theme.MonoFontFamily, Math.Max(9f, r * 0.12f), FontStyle.Bold, GraphicsUnit.Pixel))
        using (var readout = new SolidBrush(airspeed >= NeverExceed ? Theme.Bad : Color.White))
            DrawCentered(g, $"{airspeed:0}", readoutFont, readout, 0, face * 0.38f);

        // Needle
        var ns = g.Save();
        g.RotateTransform(DialAngle(airspeed));
        float w = r * 0.035f;
        using (var needle = new SolidBrush(Color.White))
            g.FillPolygon(needle, new[] { new PointF(-w, face * 0.12f), new PointF(0, -face * 0.86f), new PointF(w, face * 0.12f) });
        g.Restore(ns);
        float hub = r * 0.08f;
        using (var hubBrush = new SolidBrush(Color.FromArgb(90, 93, 100)))
            g.FillEllipse(hubBrush, -hub, -hub, hub * 2, hub * 2);
        g.ResetTransform();
    }
}
