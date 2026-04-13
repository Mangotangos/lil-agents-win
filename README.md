# lil-agents-win

Animated AI companions that walk across your Windows taskbar. Click one to open a terminal-style chat powered by Claude, Gemini, Codex, or Copilot.

Inspired by [lil-agents](https://github.com/ryanstephen/lil-agents) for macOS.

![Two animated robot characters walking on the Windows taskbar](docs/preview.png)

---

## Features

- Two animated characters walk back and forth on your taskbar
- Click a character → opens a themed terminal chat window
- Right-click system tray icon → switch AI provider or color theme
- Multi-turn conversation memory per character
- Slash commands: `/clear`, `/copy`, `/help`
- 4 built-in themes: Peach · Midnight · Cloud · Moss
- Fully click-through when not hovering — never blocks your workflow

---

## Requirements

- Windows 10 / 11 (x64)
- [.NET 8 Runtime](https://dotnet.microsoft.com/download/dotnet/8.0)
- At least one AI CLI installed:

| Provider | Install |
|----------|---------|
| **Claude** (recommended) | [claude.ai/download](https://claude.ai/download) |
| Gemini | `npm install -g @google/gemini-cli` |
| Codex | `npm install -g @openai/codex` |
| Copilot | `npm install -g @githubnext/github-copilot-cli` |

---

## Quick start

```powershell
# Clone and run
git clone https://github.com/YOUR_USERNAME/lil-agents-win.git
cd lil-agents-win\LilAgentsWin
dotnet run
```

Or download the latest `.exe` from [Releases](../../releases).

---

## Building

```powershell
cd LilAgentsWin
dotnet build -c Release
dotnet publish -c Release -r win-x64 --self-contained false -o ..\dist
```

---

## Customizing characters

Drop sprite sheets into `LilAgentsWin/Assets/` and update `WalkerWindow.xaml` to reference them.
The default characters are drawn with WPF vector shapes — no external assets required.

---

## Architecture

```
LilAgentsWin/
├── App.xaml(.cs)           Entry point, wires tray + characters
├── WalkerWindow.xaml(.cs)  Transparent taskbar overlay, walking animation
├── ChatWindow.xaml(.cs)    Terminal-style chat UI
├── Win32/
│   └── NativeMethods.cs    Taskbar detection (SHAppBarMessage) + click-through (WS_EX_TRANSPARENT)
├── Core/
│   ├── AgentSession.cs     IAgentSession interface + AgentProvider enum
│   ├── ShellEnvironment.cs Windows PATH / binary discovery
│   └── WalkerCharacter.cs  Lifecycle wrapper per character
├── Sessions/
│   ├── ClaudeSession.cs    Claude CLI — NDJSON streaming
│   ├── GeminiSession.cs    Gemini CLI
│   ├── CodexSession.cs     OpenAI Codex CLI
│   ├── CopilotSession.cs   GitHub Copilot CLI
│   └── SessionFactory.cs   Provider → session factory
└── UI/
    ├── PopoverTheme.cs     Color schemes (Peach/Midnight/Cloud/Moss)
    └── SystemTrayManager.cs WinForms NotifyIcon + context menu
```

---

## Contributing

PRs welcome. To add a new AI provider:
1. Implement `IAgentSession` in `Sessions/`
2. Add an entry to `AgentProvider` enum
3. Register in `SessionFactory.Create()`
4. Add install hint in `ShellEnvironment.InstallHint()`
5. Add tray menu item in `SystemTrayManager.BuildMenu()`

---

## License

MIT — see [LICENSE](LICENSE)
