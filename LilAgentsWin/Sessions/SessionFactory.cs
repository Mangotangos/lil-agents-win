using LilAgentsWin.Core;

namespace LilAgentsWin.Sessions;

public static class SessionFactory
{
    public static IAgentSession Create(AgentProvider provider) => provider switch
    {
        AgentProvider.Claude  => new ClaudeSession(),
        AgentProvider.Gemini  => new GeminiSession(),
        AgentProvider.Codex   => new CodexSession(),
        AgentProvider.Copilot => new CopilotSession(),
        _                     => throw new ArgumentOutOfRangeException(nameof(provider)),
    };
}
