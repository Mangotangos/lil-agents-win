using System.IO;

namespace LilAgentsWin.Core;

/// <summary>Resolves AI CLI binary locations on Windows.</summary>
public static class ShellEnvironment
{
    private static readonly string UserProfile =
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    private static readonly string AppData =
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

    private static readonly string LocalAppData =
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

    /// <summary>
    /// Extra directories to probe beyond the system PATH.
    /// These cover the most common install locations for AI CLIs on Windows.
    /// </summary>
    private static readonly IReadOnlyList<string> ExtraPaths =
    [
        // npm global (both roaming and local)
        Path.Combine(AppData,      "npm"),
        Path.Combine(LocalAppData, "npm"),
        // Claude Code installer
        Path.Combine(LocalAppData, "Programs", "claude"),
        Path.Combine(AppData,      "claude", "bin"),
        // Scoop
        Path.Combine(UserProfile, "scoop", "shims"),
        // Volta
        Path.Combine(LocalAppData, "Volta", "bin"),
        // nvm-windows shims
        Path.Combine(AppData, "nvm"),
        // pnpm
        Path.Combine(LocalAppData, "pnpm"),
        // misc global bin
        Path.Combine(UserProfile, ".local", "bin"),
    ];

    private static readonly string[] ExeExtensions = [".exe", ".cmd", ".bat", ""];

    /// <summary>Returns the full path to <paramref name="name"/> or <c>null</c> if not found.</summary>
    public static string? FindBinary(string name)
    {
        var pathVar   = Environment.GetEnvironmentVariable("PATH") ?? "";
        var searchDirs = pathVar
            .Split(';', StringSplitOptions.RemoveEmptyEntries)
            .Concat(ExtraPaths)
            .Distinct(StringComparer.OrdinalIgnoreCase);

        foreach (var dir in searchDirs)
        {
            foreach (var ext in ExeExtensions)
            {
                var full = Path.Combine(dir, name + ext);
                if (File.Exists(full))
                    return full;
            }
        }
        return null;
    }

    public static bool IsBinaryAvailable(string name) => FindBinary(name) is not null;

    /// <summary>
    /// Returns true when the binary needs to be launched via <c>cmd.exe /c</c>
    /// (i.e. it is a .cmd or .bat script — common for npm-installed CLIs on Windows).
    /// </summary>
    public static bool NeedsCmdWrapper(string binaryPath) =>
        binaryPath.EndsWith(".cmd", StringComparison.OrdinalIgnoreCase) ||
        binaryPath.EndsWith(".bat", StringComparison.OrdinalIgnoreCase);

    /// <summary>Returns the install hint for a provider whose binary is missing.</summary>
    public static string InstallHint(AgentProvider provider) => provider switch
    {
        AgentProvider.Claude  => "Install Claude Code from https://claude.ai/download",
        AgentProvider.Gemini  => "npm install -g @google/gemini-cli",
        AgentProvider.Codex   => "npm install -g @openai/codex",
        AgentProvider.Copilot => "Install GitHub CLI (https://cli.github.com) then run: gh extension install github/gh-copilot",
        _                     => "Install the required CLI tool.",
    };
}
