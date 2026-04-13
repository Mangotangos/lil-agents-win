using System.Windows;
using LilAgentsWin.Core;
using LilAgentsWin.UI;

namespace LilAgentsWin;

public partial class App : Application
{
    private SystemTrayManager?     _tray;
    private List<WalkerCharacter>  _characters = [];

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // WinForms required for NotifyIcon
        System.Windows.Forms.Application.EnableVisualStyles();

        _tray = new SystemTrayManager(OnQuit, OnThemeChanged);

        // Bruce = Claude (orange), Jazz = Copilot (blue-purple)
        _characters.Add(new WalkerCharacter("Bruce", 0, AgentProvider.Claude,  WalkerWindow.ClaudePalette));
        _characters.Add(new WalkerCharacter("Jazz",  1, AgentProvider.Copilot, WalkerWindow.CopilotPalette));

        foreach (var c in _characters)
            c.Start();
    }

    private void OnQuit()
    {
        foreach (var c in _characters)
            c.Stop();
        _tray?.Dispose();
        Shutdown();
    }

    private void OnThemeChanged(PopoverTheme theme)
    {
        foreach (var c in _characters)
            c.SetTheme(theme);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _tray?.Dispose();
        base.OnExit(e);
    }
}
