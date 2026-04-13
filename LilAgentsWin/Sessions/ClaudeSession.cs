using System.Diagnostics;
using System.Text;
using System.Text.Json;
using LilAgentsWin.Core;

namespace LilAgentsWin.Sessions;

/// <summary>
/// Drives the <c>claude</c> CLI in streaming JSON mode.
/// Supports multi-turn conversation by replaying history on each call.
/// </summary>
public sealed class ClaudeSession : IAgentSession
{
    public event Action<string>? OnOutput;
    public event Action<string>? OnError;
    public event Action? OnDone;

    private readonly List<ConversationTurn> _history = [];
    private Process? _process;
    private CancellationTokenSource _cts = new();

    // ─── IAgentSession ────────────────────────────────────────────────────────

    public async Task SendAsync(string message, CancellationToken ct = default)
    {
        Cancel();
        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);

        var binary = ShellEnvironment.FindBinary("claude")
            ?? throw new InvalidOperationException(ShellEnvironment.InstallHint(AgentProvider.Claude));

        _history.Add(new ConversationTurn("user", message));

        // Build conversation context: prefix with prior turns separated by newlines
        var prompt = BuildPrompt(message);

        var psi = new ProcessStartInfo
        {
            FileName               = binary,
            Arguments              = $"--output-format stream-json --print --no-update-check \"{EscapeArg(prompt)}\"",
            UseShellExecute        = false,
            RedirectStandardOutput = true,
            RedirectStandardError  = true,
            CreateNoWindow         = true,
            StandardOutputEncoding = Encoding.UTF8,
        };

        _process = Process.Start(psi)
            ?? throw new InvalidOperationException("Failed to start claude process.");

        _ = StreamOutputAsync(_cts.Token);
    }

    public void Cancel()
    {
        _cts.Cancel();
        try   { _process?.Kill(entireProcessTree: true); } catch { }
        _process?.Dispose();
        _process = null;
    }

    public void Dispose() => Cancel();

    // ─── Helpers ──────────────────────────────────────────────────────────────

    private string BuildPrompt(string latest)
    {
        if (_history.Count <= 1) return latest;
        var sb = new StringBuilder();
        foreach (var t in _history[..^1])
            sb.AppendLine($"{t.role}: {t.content}");
        sb.Append(latest);
        return sb.ToString();
    }

    private static string EscapeArg(string s) =>
        s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "").Replace("\n", " ");

    // ─── Internal ─────────────────────────────────────────────────────────────

    private async Task StreamOutputAsync(CancellationToken ct)
    {
        var assistant = new StringBuilder();
        try
        {
            while (!_process!.StandardOutput.EndOfStream && !ct.IsCancellationRequested)
            {
                var line = await _process.StandardOutput.ReadLineAsync(ct);
                if (string.IsNullOrEmpty(line)) continue;
                ParseLine(line, assistant);
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            OnError?.Invoke($"Stream error: {ex.Message}");
        }
        finally
        {
            if (assistant.Length > 0)
                _history.Add(new ConversationTurn("assistant", assistant.ToString()));
            OnDone?.Invoke();
        }
    }

    private void ParseLine(string line, StringBuilder assistant)
    {
        try
        {
            using var doc  = JsonDocument.Parse(line);
            var root = doc.RootElement;

            if (!root.TryGetProperty("type", out var typeEl)) return;

            switch (typeEl.GetString())
            {
                case "content_block_delta":
                    if (root.TryGetProperty("delta", out var delta) &&
                        delta.TryGetProperty("text", out var text))
                    {
                        var chunk = text.GetString() ?? string.Empty;
                        assistant.Append(chunk);
                        OnOutput?.Invoke(chunk);
                    }
                    break;

                case "message_stop":
                    // signal handled in finally block
                    break;
            }
        }
        catch (JsonException) { /* ignore non-JSON lines (e.g. progress spinners) */ }
    }

    private record ConversationTurn(string role, string content);
}
