using System.ComponentModel;

namespace PlaneGUI.Instruments;

/// <summary>Directional gyro: a compass card that turns under a fixed aircraft symbol.</summary>
public sealed class HeadingIndicator : InstrumentControl
{
    float heading;

    /// <summary>Heading in degrees, 0 = north.</summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public float Heading { get => heading; set => Set(ref heading, FlightModel.Wrap360(value)); }

    // Aircraft outline (right half, pointing up), in units of the symbol's half-length.
    static readonly PointF[] PlaneHalf =
    {
        new(0f, -1f), new(0.07f, -0.9f), new(0.08f, -0.3f), new(0.85f, 0.05f), new(0.85f, 0.18f),
        new(0.08f, 0.05f), new(0.06f, 0.6f), new(0.3f, 0.78f), new(0.3f, 0.88f), new(0f, 0.82f),
    };

    protected override void Draw(Graphics g)
    {
        float size = Math.Min(Width, Height) - 4;
        if (size < 60) return;
        float cx = Width / 2f, cy = Height / 2f, r = size / 2f;
        float card = r * 0.9f;

        DrawBezel(g, cx, cy, r);
        using (var face = new SolidBrush(Color.FromArgb(38, 40, 46)))
            g.FillEllipse(face, cx - card, cy - card, card * 2, card * 2);

        g.TranslateTransform(cx, cy);
        using (var northBrush = new SolidBrush(Theme.Accent))
        using (var tick = new Pen(Color.White, Math.Max(1f, r * 0.01f)))
        using (var numberFont = new Font(Theme.FontFamily, Math.Max(8f, r * 0.1f), FontStyle.Bold, GraphicsUnit.Pixel))
        using (var cardinalFont = new Font(Theme.FontFamily, Math.Max(9f, r * 0.14f), FontStyle.Bold, GraphicsUnit.Pixel))
        {
            for (int deg = 0; deg < 360; deg += 5)
            {
                var s = g.Save();
                g.RotateTransform(deg - heading);
                float len = deg % 10 == 0 ? card * 0.1f : card * 0.05f;
                g.DrawLine(tick, 0, -card, 0, -card + len);
                if (deg % 30 == 0)
                {
                    bool cardinal = deg % 90 == 0;
                    string label = deg switch { 0 => "N", 90 => "E", 180 => "S", 270 => "W", _ => (deg / 10).ToString() };
                    DrawCentered(g, label, cardinal ? cardinalFont : numberFont,
                                 deg == 0 ? northBrush : Brushes.White, 0, -card * 0.74f);
                }
                g.Restore(s);
            }
        }

        // Fixed lubber line at the top
        float tw = r * 0.06f;
        using (var accent = new SolidBrush(Theme.Accent))
        {
            g.FillPolygon(accent, new[] { new PointF(0, -card + 2), new PointF(-tw, -card - tw * 1.2f), new PointF(tw, -card - tw * 1.2f) });

            // Aircraft symbol in the centre
            float k = card * 0.32f;
            var outline = PlaneHalf.Select(p => new PointF(p.X * k, p.Y * k))
                .Concat(PlaneHalf.Reverse().Skip(1).Take(PlaneHalf.Length - 2).Select(p => new PointF(-p.X * k, p.Y * k)))
                .ToArray();
            g.FillPolygon(accent, outline);
        }
        g.ResetTransform();

        using var readoutFont = new Font(Theme.MonoFontFamily, Math.Max(9f, r * 0.11f), FontStyle.Bold, GraphicsUnit.Pixel);
        DrawCentered(g, $"{Math.Round(heading) % 360:000}°", readoutFont, Brushes.White, cx, cy + card * 0.5f);
    }
}
