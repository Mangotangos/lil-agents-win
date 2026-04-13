using System.Windows.Media;

namespace LilAgentsWin.UI;

public enum PopoverThemeType { Peach, Midnight, Cloud, Moss }

/// <summary>Color scheme for the chat terminal window.</summary>
public sealed class PopoverTheme
{
    public string           Name       { get; }
    public PopoverThemeType Type       { get; }
    public Color            Background { get; }
    public Color            Surface    { get; }
    public Color            Text       { get; }
    public Color            Accent     { get; }
    public Color            InputBg    { get; }
    public Color            Border     { get; }

    private PopoverTheme(string name, PopoverThemeType type,
        Color bg, Color surface, Color text, Color accent, Color inputBg, Color border)
    {
        Name       = name;
        Type       = type;
        Background = bg;
        Surface    = surface;
        Text       = text;
        Accent     = accent;
        InputBg    = inputBg;
        Border     = border;
    }

    // ─── Brushes (created on demand) ─────────────────────────────────────────

    public SolidColorBrush BackgroundBrush => new(Background);
    public SolidColorBrush SurfaceBrush    => new(Surface);
    public SolidColorBrush TextBrush       => new(Text);
    public SolidColorBrush AccentBrush     => new(Accent);
    public SolidColorBrush InputBgBrush    => new(InputBg);
    public SolidColorBrush BorderBrush     => new(Border);

    // ─── Built-in themes ─────────────────────────────────────────────────────

    public static readonly PopoverTheme Peach = new(
        "Peach", PopoverThemeType.Peach,
        Color.FromRgb(0x2D, 0x1B, 0x14),
        Color.FromRgb(0x3D, 0x28, 0x1E),
        Color.FromRgb(0xFF, 0xD8, 0xCC),
        Color.FromRgb(0xFF, 0x9A, 0x70),
        Color.FromRgb(0x35, 0x22, 0x18),
        Color.FromRgb(0x6A, 0x40, 0x2A));

    public static readonly PopoverTheme Midnight = new(
        "Midnight", PopoverThemeType.Midnight,
        Color.FromRgb(0x0D, 0x11, 0x1A),
        Color.FromRgb(0x14, 0x1B, 0x2D),
        Color.FromRgb(0xC8, 0xD8, 0xFF),
        Color.FromRgb(0x5A, 0x8F, 0xFF),
        Color.FromRgb(0x10, 0x16, 0x24),
        Color.FromRgb(0x2A, 0x3A, 0x6A));

    public static readonly PopoverTheme Cloud = new(
        "Cloud", PopoverThemeType.Cloud,
        Color.FromRgb(0xF0, 0xF4, 0xFF),
        Color.FromRgb(0xE0, 0xE8, 0xFF),
        Color.FromRgb(0x1A, 0x20, 0x40),
        Color.FromRgb(0x40, 0x80, 0xFF),
        Color.FromRgb(0xE8, 0xEF, 0xFF),
        Color.FromRgb(0xB0, 0xC0, 0xE8));

    public static readonly PopoverTheme Moss = new(
        "Moss", PopoverThemeType.Moss,
        Color.FromRgb(0x11, 0x1A, 0x14),
        Color.FromRgb(0x18, 0x28, 0x1C),
        Color.FromRgb(0xC8, 0xF0, 0xD0),
        Color.FromRgb(0x50, 0xC8, 0x70),
        Color.FromRgb(0x14, 0x22, 0x17),
        Color.FromRgb(0x2A, 0x5A, 0x34));

    public static IReadOnlyList<PopoverTheme> All => [Peach, Midnight, Cloud, Moss];
}
