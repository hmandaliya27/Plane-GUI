namespace PlaneGUI.Instruments;

/// <summary>
/// Single-line label that shrinks its font when the text would not fit,
/// so readouts never get cut off when the window is made smaller.
/// </summary>
public sealed class FitLabel : Control
{
    public bool AlignRight { get; set; }

    public FitLabel()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.UserPaint |
                 ControlStyles.ResizeRedraw, true);
        BackColor = Theme.Panel;
        ForeColor = Theme.Text;
    }

    protected override void OnTextChanged(EventArgs e)
    {
        base.OnTextChanged(e);
        Invalidate();
    }

    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(BackColor);
        if (string.IsNullOrEmpty(Text) || Width < 4 || Height < 4) return;

        var flags = TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine |
                    TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding |
                    (AlignRight ? TextFormatFlags.Right : TextFormatFlags.Left);

        var needed = TextRenderer.MeasureText(g, Text, Font, Size.Empty, flags);
        float fit = Math.Min(1f, Math.Min((Width - 2) / (float)Math.Max(1, needed.Width),
                                          Height / (float)Math.Max(1, needed.Height)));
        if (fit >= 1f)
        {
            TextRenderer.DrawText(g, Text, Font, ClientRectangle, ForeColor, flags);
            return;
        }
        using var smaller = new Font(Font.FontFamily, Math.Max(5f, Font.Size * fit * 0.97f), Font.Style);
        TextRenderer.DrawText(g, Text, smaller, ClientRectangle, ForeColor, flags);
    }
}
