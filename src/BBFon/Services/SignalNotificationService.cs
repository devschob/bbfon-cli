using System.Diagnostics;

namespace BBFon.Services;

public class SignalNotificationService : INotificationService
{
    private readonly SignalConfig _config;
    private const int TimeoutSeconds = 15;

    public SignalNotificationService(SignalConfig config) => _config = config;

    public async Task SendAsync(string message, IReadOnlyList<string>? attachments = null)
    {
        var cliPath = Path.IsPathRooted(_config.CliPath)
            ? _config.CliPath
            : Path.Combine(AppContext.BaseDirectory, _config.CliPath);

        var attachmentArgs = attachments?.Count > 0
            ? " " + string.Join(" ", attachments.Select(a => $"--attachment \"{Path.GetFullPath(a)}\""))
            : "";

        var psi = new ProcessStartInfo
        {
            FileName = cliPath,
            Arguments = $"send -m \"{message}\"{attachmentArgs} -u {_config.Sender} {_config.Recipient}",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        using var process = Process.Start(psi)
            ?? throw new InvalidOperationException("signal-cli konnte nicht gestartet werden.");

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(TimeoutSeconds));
        try
        {
            await process.WaitForExitAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            process.Kill();
            throw new Exception($"signal-cli hat nach {TimeoutSeconds}s nicht geantwortet (Timeout).");
        }

        if (process.ExitCode != 0)
        {
            var err = await process.StandardError.ReadToEndAsync();
            throw new Exception($"signal-cli Fehler (Exit {process.ExitCode}): {err}");
        }
    }
}
