using System.Diagnostics;

namespace BBFon.Services;

public class WhatsAppNotificationService : INotificationService
{
    private readonly WhatsAppConfig _config;
    private const int TimeoutSeconds = 30;

    public WhatsAppNotificationService(WhatsAppConfig config) => _config = config;

    public async Task SendAsync(string message, IReadOnlyList<string>? attachments = null)
    {
        ConsoleLog.Info("[BBFon] Sende WhatsApp...");
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
        var doneTcs = new TaskCompletionSource<bool>();

        // Stdout und Stderr live lesen; bei "Done" sofort als gesendet werten
        var outputLines = new System.Collections.Concurrent.ConcurrentBag<string>();
        Task ReadLinesAsync(StreamReader reader) => Task.Run(async () =>
        {
            string? line;
            while ((line = await reader.ReadLineAsync()) != null)
            {
                outputLines.Add(line);
                if (line.Contains("Done", StringComparison.OrdinalIgnoreCase))
                    doneTcs.TrySetResult(true);
            }
        });

        var stdoutTask = ReadLinesAsync(process.StandardOutput);
        var stderrTask = ReadLinesAsync(process.StandardError);

        var exitTask = process.WaitForExitAsync(cts.Token);

        // Warten auf: "Done" in Ausgabe ODER Prozessende
        var completed = await Task.WhenAny(doneTcs.Task, exitTask);

        if (completed == doneTcs.Task)
        {
            ConsoleLog.Success("[BBFon] WhatsApp Nachricht gesendet.");
            using var graceCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            try { await process.WaitForExitAsync(graceCts.Token); }
            catch (OperationCanceledException) { process.Kill(); }
            return;
        }

        // Prozess ist beendet – Exit-Code prüfen
        if (cts.IsCancellationRequested)
        {
            process.Kill();
            throw new Exception($"mudslide hat nach {timeoutSeconds}s nicht geantwortet (Timeout).");
        }

        await Task.WhenAll(stdoutTask, stderrTask);

        if (process.ExitCode != 0)
        {
            var err = string.Join("\n", outputLines);
            throw new Exception($"mudslide Fehler (Exit {process.ExitCode}): {err}");
        }
    }
}
