using System.Diagnostics;

namespace BBFon.Services;

public class WhatsAppNotificationService : INotificationService
{
    private readonly WhatsAppConfig _config;
    private const int TimeoutSeconds = 30;

    public WhatsAppNotificationService(WhatsAppConfig config) => _config = config;

    public async Task SendAsync(string message, IReadOnlyList<string>? attachments = null)
    {
        var cliPath = Path.IsPathRooted(_config.CliPath)
            ? _config.CliPath
            : Path.Combine(AppContext.BaseDirectory, _config.CliPath);

        if (!string.IsNullOrEmpty(message))
            await RunMudslideAsync(cliPath, ["send", _config.Recipient, message]);

        if (attachments != null)
            foreach (var a in attachments)
                await RunMudslideAsync(cliPath, ["send-file", _config.Recipient, Path.GetFullPath(a)]);
    }

    private static async Task RunMudslideAsync(string cliPath, IEnumerable<string> arguments, int timeoutSeconds = TimeoutSeconds)
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
                $"mudslide konnte nicht gestartet werden – Pfad prüfen: \"{cliPath}\"\n" +
                $"mudslide herunterladen: https://github.com/robvanderleek/mudslide/releases");
        }

        if (process == null)
            throw new Exception(
                $"mudslide konnte nicht gestartet werden – Pfad prüfen: \"{cliPath}\"\n" +
                $"mudslide herunterladen: https://github.com/robvanderleek/mudslide/releases");

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
        try
        {
            await process.WaitForExitAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            process.Kill();
            throw new Exception($"mudslide hat nach {timeoutSeconds}s nicht geantwortet (Timeout).");
        }

        if (process.ExitCode != 0)
        {
            var err = await process.StandardError.ReadToEndAsync();
            throw new Exception($"mudslide Fehler (Exit {process.ExitCode}): {err}");
        }
    }
}
