namespace LilAgentsWin.Core;

public enum AgentProvider
{
    Claude,
    Gemini,
    Codex,
    Copilot,
}

public interface IAgentSession : IDisposable
{
    /// <summary>Fires for each streamed text chunk from the model.</summary>
    event Action<string>? OnOutput;

    /// <summary>Fires when an error / warning message is emitted.</summary>
    event Action<string>? OnError;

    /// <summary>Fires when the model has finished its response.</summary>
    event Action? OnDone;

    Task SendAsync(string message, CancellationToken ct = default);

    void Cancel();
}
