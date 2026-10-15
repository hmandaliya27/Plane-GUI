namespace PlaneGUI;

static class Theme
{
    public static readonly Color Background = Color.FromArgb(18, 20, 24);
    public static readonly Color Panel = Color.FromArgb(30, 33, 40);
    public static readonly Color PanelBorder = Color.FromArgb(52, 57, 68);
    public static readonly Color Text = Color.FromArgb(226, 229, 236);
    public static readonly Color Muted = Color.FromArgb(140, 147, 162);
    public static readonly Color Accent = Color.FromArgb(255, 196, 0);
    public static readonly Color Good = Color.FromArgb(46, 196, 110);
    public static readonly Color Bad = Color.FromArgb(235, 72, 72);
    public static readonly Color Bezel = Color.FromArgb(44, 46, 52);
    public static readonly Color BezelDark = Color.FromArgb(24, 25, 29);

    // Same sky/ground colours as the original Artificalhorizon.png
    public static readonly Color Sky = Color.FromArgb(0, 164, 232);
    public static readonly Color Ground = Color.FromArgb(185, 123, 88);

    public const string FontFamily = "Segoe UI";
    public const string MonoFontFamily = "Consolas";
}
