using System.Diagnostics;
using LilAgentsWin.Core;

namespace LilAgentsWin.Sessions;

public sealed class GeminiSession : IAgentSession
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

        var binary = ShellEnvironment.FindBinary("gemini")
            ?? throw new InvalidOperationException(ShellEnvironment.InstallHint(AgentProvider.Gemini));

        var psi = BuildPsi(binary);
        psi.ArgumentList.Add("-p");
        psi.ArgumentList.Add(message);

        _process = Process.Start(psi)!;
        _process.StandardInput.Close();
        _ = StreamAsync(_cts.Token);
        _ = DrainStderrAsync(_cts.Token);
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

    private static ProcessStartInfo BuildPsi(string binary)
    {
        var psi = new ProcessStartInfo
        {
            UseShellExecute        = false,
            RedirectStandardInput  = true,
            RedirectStandardOutput = true,
            RedirectStandardError  = true,
            CreateNoWindow         = true,
        };
        if (ShellEnvironment.NeedsCmdWrapper(binary))
        {
            psi.FileName = "cmd.exe";
            psi.ArgumentList.Add("/c");
            psi.ArgumentList.Add(binary);
        }
        else psi.FileName = binary;
        return psi;
    }
}
