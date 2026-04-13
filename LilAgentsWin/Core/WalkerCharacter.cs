using LilAgentsWin.UI;

namespace LilAgentsWin.Core;

/// <summary>
/// Manages the lifecycle of one walker: creates its window, exposes provider/theme setters.
/// Separated from WalkerWindow so App.cs can manage multiple characters cleanly.
/// </summary>
public sealed class WalkerCharacter
{
    private readonly WalkerWindow _window;

    public string Name { get; }

    public WalkerCharacter(string name, int index, AgentProvider provider, WalkerWindow.CharacterPalette palette)
    {
        Name    = name;
        _window = new WalkerWindow(index, provider, palette);
    }

    public void Start()
    {
        _window.Show();
    }

    public void Stop()
    {
        _window.Stop();
    }

    public void SetProvider(AgentProvider provider) => _window.SetProvider(provider);

    public void SetTheme(PopoverTheme theme) => _window.SetTheme(theme);
}
