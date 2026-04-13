using System.Diagnostics;
using LilAgentsWin.Core;

namespace LilAgentsWin.Sessions;

public sealed class CodexSession : IAgentSession
{
    public event Action<string>? OnOutput;
    public event Action<string>? OnError;
    public event Action? OnDone;

    private Process? _process;
    private CancellationTokenSource _cts = new();

    public async Task SendAsync(string message, CancellationToken ct = default)
    {
        Cancel();
        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);

        var binary = ShellEnvironment.FindBinary("codex")
            ?? throw new InvalidOperationException(ShellEnvironment.InstallHint(AgentProvider.Codex));

        var psi = new ProcessStartInfo
        {
            FileName               = binary,
            Arguments              = $"-q \"{Escape(message)}\"",
            UseShellExecute        = false,
            RedirectStandardOutput = true,
            RedirectStandardError  = true,
            CreateNoWindow         = true,
        };

        _process = Process.Start(psi)!;
        _ = StreamAsync(_cts.Token);
        await Task.CompletedTask;
    }

    public void Cancel()
    {
        _cts.Cancel();
        try   { _process?.Kill(entireProcessTree: true); } catch { }
        _process?.Dispose();
        _process = null;
    }

    public void Dispose() => Cancel();

    private async Task StreamAsync(CancellationToken ct)
    {
        try
        {
            while (!_process!.StandardOutput.EndOfStream && !ct.IsCancellationRequested)
            {
                var line = await _process.StandardOutput.ReadLineAsync(ct);
                if (line is not null) OnOutput?.Invoke(line + "\n");
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { OnError?.Invoke(ex.Message); }
        finally { OnDone?.Invoke(); }
    }

    private static string Escape(string s) => s.Replace("\"", "\\\"").Replace("\n", " ");
}
