using System.Diagnostics;

namespace BBFon.Services;

public class SignalNotificationService : INotificationService
{
    private readonly SignalConfig _config;
    private const int TimeoutSeconds = 15;

    public SignalNotificationService(SignalConfig config) => _config = config;

    public async Task SendAsync(string message, IReadOnlyList<string>? attachments = null)
    {
        ConsoleLog.Info("[BBFon] Sende Signal...");
        var cliPath = Path.IsPathRooted(_config.CliPath)
            ? _config.CliPath
            : Path.Combine(AppContext.BaseDirectory, _config.CliPath);

        try
        {
            await RunSignalCliAsync(cliPath,
                ["-u", _config.Sender, "receive", "--timeout", "1", "--ignore-attachments"],
                timeoutSeconds: 5);
        }
        catch { /* receive ist optional – Fehler ignorieren */ }

        var sendArgs = new List<string> { "-u", _config.Sender, "send", "-m", message, _config.Recipient };
        if (attachments?.Count > 0)
            foreach (var a in attachments)
            {
                sendArgs.Add("--attachment");
                sendArgs.Add(Path.GetFullPath(a));
            }

        await RunSignalCliAsync(cliPath, sendArgs);
    }

    private async Task RunSignalCliAsync(string cliPath, IEnumerable<string> arguments, int timeoutSeconds = TimeoutSeconds)
    {
        var psi = new ProcessStartInfo
        {
            FileName = cliPath,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        foreach (var arg in arguments)
            psi.ArgumentList.Add(arg);

        Process? process;
        try
        {
            process = Process.Start(psi);
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or FileNotFoundException)
        {
            throw new Exception(
                $"signal-cli konnte nicht gestartet werden – Pfad prüfen: \"{cliPath}\"\n" +
                $"signal-cli herunterladen: https://github.com/AsamK/signal-cli/releases");
        }

        if (process == null)
            throw new Exception(
                $"signal-cli konnte nicht gestartet werden – Pfad prüfen: \"{cliPath}\"\n" +
                $"signal-cli herunterladen: https://github.com/AsamK/signal-cli/releases");

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
        try
        {
            await process.WaitForExitAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            process.Kill();
            throw new Exception($"signal-cli hat nach {timeoutSeconds}s nicht geantwortet (Timeout).");
        }

        if (process.ExitCode != 0)
        {
            var err = await process.StandardError.ReadToEndAsync();
            throw new Exception($"signal-cli Fehler (Exit {process.ExitCode}): {err}");
        }
    }
}
