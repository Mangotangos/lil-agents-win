using System.Diagnostics;
using System.Text;
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

    public Task SendAsync(string message, CancellationToken ct = default)
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
            UseShellExecute        = false,
            RedirectStandardInput  = true,
            RedirectStandardOutput = true,
            RedirectStandardError  = true,
            CreateNoWindow         = true,
            StandardOutputEncoding = Encoding.UTF8,
        };
        psi.Environment["CI"] = "true"; // suppress interactive prompts/hooks

        // npm-installed CLIs on Windows are .cmd scripts — must run via cmd.exe
        if (ShellEnvironment.NeedsCmdWrapper(binary))
        {
            psi.FileName = "cmd.exe";
            psi.ArgumentList.Add("/c");
            psi.ArgumentList.Add(binary);
        }
        else
        {
            psi.FileName = binary;
        }

        psi.ArgumentList.Add("--output-format");
        psi.ArgumentList.Add("text");
        psi.ArgumentList.Add("--print");
        psi.ArgumentList.Add(prompt);

        _process = Process.Start(psi)
            ?? throw new InvalidOperationException("Failed to start claude process.");

        _process.StandardInput.Close(); // signal EOF so claude doesn't wait for input

        _ = StreamOutputAsync(_cts.Token);
        _ = DrainStderrAsync(_cts.Token);
        return Task.CompletedTask;
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

    // ─── Internal ─────────────────────────────────────────────────────────────

    private async Task StreamOutputAsync(CancellationToken ct)
    {
        var assistant = new StringBuilder();

        // Cancel reading 1 s after the main process exits so hook subprocesses
        // cannot hold the stdout pipe open indefinitely.
        using var drainCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _ = Task.Run(async () =>
        {
            try
            {
                await _process!.WaitForExitAsync(ct);
                await Task.Delay(1000, ct);
            }
            catch { }
            finally { drainCts.Cancel(); }
        }, ct);

        try
        {
            while (!drainCts.Token.IsCancellationRequested)
            {
                var line = await _process!.StandardOutput.ReadLineAsync(drainCts.Token);
                if (line is null) break;
                assistant.Append(line + "\n");
                OnOutput?.Invoke(line + "\n");
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { OnError?.Invoke($"Stream error: {ex.Message}"); }
        finally
        {
            if (assistant.Length > 0)
                _history.Add(new ConversationTurn("assistant", assistant.ToString()));
            OnDone?.Invoke();
        }
    }

    private async Task DrainStderrAsync(CancellationToken ct)
    {
        try
        {
            while (!_process!.StandardError.EndOfStream && !ct.IsCancellationRequested)
            {
                var line = await _process.StandardError.ReadLineAsync(ct);
                if (line is not null && line.Length > 0)
                    OnError?.Invoke(line);
            }
        }
        catch (OperationCanceledException) { }
        catch { }
    }

    private record ConversationTurn(string role, string content);
}
