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

        _tray = new SystemTrayManager(OnQuit, OnProviderChanged, OnThemeChanged);

        _characters.Add(new WalkerCharacter("Bruce", 0, _tray));
        _characters.Add(new WalkerCharacter("Jazz",  1, _tray));

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

    private void OnProviderChanged(AgentProvider provider)
    {
        foreach (var c in _characters)
            c.SetProvider(provider);
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
