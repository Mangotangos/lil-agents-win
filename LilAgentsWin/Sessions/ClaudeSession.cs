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

        _process.StandardInput.Close();

        _ = ReadOutputAsync(_cts.Token);
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

    private async Task ReadOutputAsync(CancellationToken ct)
    {
        var assistant = new StringBuilder();
        try
        {
            // Read stdout and stderr concurrently — avoids pipe deadlock.
            // Use a 90-second hard timeout in case hooks block indefinitely.
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(90));
            using var linked  = CancellationTokenSource.CreateLinkedTokenSource(ct, timeout.Token);

            var stdoutTask = _process!.StandardOutput.ReadToEndAsync(linked.Token);
            var stderrTask = _process!.StandardError.ReadToEndAsync(linked.Token);

            await Task.WhenAll(stdoutTask, stderrTask);

            var text = stdoutTask.Result.Trim();
            if (!string.IsNullOrEmpty(text))
            {
                assistant.Append(text);
                OnOutput?.Invoke(text);
            }
        }
        catch (OperationCanceledException)
        {
            OnError?.Invoke("Timed out waiting for response.");
        }
        catch (Exception ex)
        {
            OnError?.Invoke($"Error: {ex.Message}");
        }
        finally
        {
            if (assistant.Length > 0)
                _history.Add(new ConversationTurn("assistant", assistant.ToString()));
            OnDone?.Invoke();
        }
    }

    private record ConversationTurn(string role, string content);
}
