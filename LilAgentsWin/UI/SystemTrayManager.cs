using System.Windows.Forms;
using DrawingBitmap  = System.Drawing.Bitmap;
using DrawingBrush   = System.Drawing.Brush;
using DrawingBrushes = System.Drawing.Brushes;
using DrawingColor   = System.Drawing.Color;
using DrawingFont    = System.Drawing.Font;
using DrawingFontStyle = System.Drawing.FontStyle;
using DrawingGraphics = System.Drawing.Graphics;
using DrawingIcon    = System.Drawing.Icon;
using DrawingPen     = System.Drawing.Pen;
using DrawingToolStripLabel = System.Windows.Forms.ToolStripLabel;

namespace LilAgentsWin.UI;

/// <summary>
/// Manages the Windows system-tray icon and right-click context menu.
/// Uses WinForms NotifyIcon (available in .NET 8 via UseWindowsForms).
/// </summary>
public sealed class SystemTrayManager : IDisposable
{
    private readonly NotifyIcon _icon;
    private PopoverTheme _theme = PopoverTheme.Midnight;

    public PopoverTheme ActiveTheme => _theme;

    public SystemTrayManager(
        Action onQuit,
        Action<PopoverTheme> onThemeChanged)
    {
        _icon = new NotifyIcon
        {
            Icon    = CreateDefaultIcon(),
            Text    = "lil-agents",
            Visible = true,
        };

        _icon.ContextMenuStrip = BuildMenu(onQuit, onThemeChanged);
    }

    // ─── Menu ─────────────────────────────────────────────────────────────────

    private ContextMenuStrip BuildMenu(
        Action onQuit,
        Action<PopoverTheme> onThemeChanged)
    {
        var menu = new ContextMenuStrip();

        // Header
        menu.Items.Add(new ToolStripLabel("lil-agents  •  Bruce=Claude  |  Jazz=Copilot") { Font = new DrawingFont("Segoe UI", 9, DrawingFontStyle.Bold) });
        menu.Items.Add(new ToolStripSeparator());

        // Theme submenu
        var themeMenu = new ToolStripMenuItem("Theme");
        foreach (var t in PopoverTheme.All)
        {
            var item = new ToolStripMenuItem(t.Name)
            {
                Checked = t.Type == _theme.Type,
                Tag     = t,
            };
            item.Click += (_, _) =>
            {
                _theme = t;
                RefreshThemeChecks(themeMenu, t);
                onThemeChanged(t);
            };
            themeMenu.DropDownItems.Add(item);
        }
        menu.Items.Add(themeMenu);

        menu.Items.Add(new ToolStripSeparator());
        var quit = new ToolStripMenuItem("Quit");
        quit.Click += (_, _) => onQuit();
        menu.Items.Add(quit);

        return menu;
    }

    private static void RefreshThemeChecks(ToolStripMenuItem parent, PopoverTheme active)
    {
        foreach (ToolStripMenuItem item in parent.DropDownItems)
            item.Checked = item.Tag is PopoverTheme t && t.Type == active.Type;
    }

    // ─── Icon ─────────────────────────────────────────────────────────────────

    private static Icon CreateDefaultIcon()
    {
        var bmp = new DrawingBitmap(16, 16);
        using var g = DrawingGraphics.FromImage(bmp);
        g.Clear(DrawingColor.Transparent);
        g.FillEllipse(DrawingBrushes.CornflowerBlue, 2, 2, 12, 12);
        g.FillEllipse(DrawingBrushes.White, 4, 5, 3, 3);
        g.FillEllipse(DrawingBrushes.White, 9, 5, 3, 3);
        g.DrawArc(new DrawingPen(DrawingColor.White, 1.2f), 5, 9, 6, 3, 0, 180);
        return DrawingIcon.FromHandle(bmp.GetHicon());
    }

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.Dispose();
    }
}
