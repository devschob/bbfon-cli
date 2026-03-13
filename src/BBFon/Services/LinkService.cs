using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Net.Codecrete.QrCodeGenerator;

namespace BBFon.Services;

public sealed class LinkService
{
    private readonly SignalConfig _config;

    public LinkService(SignalConfig config) => _config = config;

    public async Task RunAsync(string? phoneNumber = null)
    {
        if (phoneNumber != null)
            SaveNumberToAppSettings(phoneNumber);
        var cliPath = Path.IsPathRooted(_config.CliPath)
            ? _config.CliPath
            : Path.Combine(AppContext.BaseDirectory, _config.CliPath);

        ConsoleLog.Info("[BBFon] Starte Signal-Verlinkung...");
        ConsoleLog.Info("[BBFon] signal-cli wird gestartet, bitte warten...\n");

        var psi = new ProcessStartInfo
        {
            FileName = cliPath,
            Arguments = "link -n \"BBFon\"",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        using var process = Process.Start(psi)
            ?? throw new InvalidOperationException("signal-cli konnte nicht gestartet werden.");

        // URL kommt je nach signal-cli Version auf stdout oder stderr
        var urlTcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

        _ = Task.Run(async () =>
        {
            while (!process.StandardOutput.EndOfStream)
            {
                var line = await process.StandardOutput.ReadLineAsync();
                if (line != null && line.Contains("sgnl://"))
                    urlTcs.TrySetResult(line.Trim());
            }
        });

        _ = Task.Run(async () =>
        {
            while (!process.StandardError.EndOfStream)
            {
                var line = await process.StandardError.ReadLineAsync();
                if (line != null && line.Contains("sgnl://"))
                    urlTcs.TrySetResult(line.Trim());
            }
        });

        string url;
        try
        {
            url = await urlTcs.Task.WaitAsync(TimeSpan.FromSeconds(30));
        }
        catch (TimeoutException)
        {
            ConsoleLog.Error("[BBFon] Fehler: signal-cli hat keine Verlinkungs-URL geliefert (Timeout 30s).");
            ConsoleLog.Error("[BBFon] Prüfe ob signal-cli korrekt installiert ist und Java verfügbar ist.");
            process.Kill();
            return;
        }
        catch (TaskCanceledException)
        {
            ConsoleLog.Error("[BBFon] Fehler: Keine URL von signal-cli erhalten.");
            process.Kill();
            return;
        }

        DisplayQrCode(url);

        ConsoleLog.Info($"[BBFon] URL: {url}\n");
        ConsoleLog.Info("[BBFon] Scanne den QR-Code jetzt mit deiner Signal-App:");
        ConsoleLog.Info("[BBFon]   Einstellungen → Verknüpfte Geräte → (+) Gerät hinzufügen");
        ConsoleLog.Info("[BBFon] Warte auf Verlinkung...\n");

        await process.WaitForExitAsync();

        if (process.ExitCode == 0)
            ConsoleLog.Success("[BBFon] Erfolgreich verknüpft! BBFon kann jetzt Signal nutzen.");
        else
            ConsoleLog.Error($"[BBFon] Verlinkung fehlgeschlagen (Exit {process.ExitCode}). Signal-App nochmal versuchen.");
    }

    private static void SaveNumberToAppSettings(string phoneNumber)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
        var json = File.ReadAllText(path);
        var root = JsonNode.Parse(json)?.AsObject()
            ?? throw new InvalidOperationException("appsettings.json konnte nicht geparst werden.");

        var signal = root["Signal"]?.AsObject() ?? new JsonObject();
        signal["Sender"]    = phoneNumber;
        signal["Recipient"] = phoneNumber;
        root["Signal"] = signal;

        var opts = new JsonSerializerOptions { WriteIndented = true };
        File.WriteAllText(path, root.ToJsonString(opts));

        ConsoleLog.Info($"[BBFon] Nummer {phoneNumber} in appsettings.json eingetragen (Sender + Recipient).");
    }

    private static void DisplayQrCode(string url)
    {
        var qr = QrCode.EncodeText(url, QrCode.Ecc.Medium);
        const int border = 2;
        var sb = new StringBuilder();

        for (int y = -border; y < qr.Size + border; y++)
        {
            for (int x = -border; x < qr.Size + border; x++)
                sb.Append(qr.GetModule(x, y) ? "██" : "  ");
            sb.AppendLine();
        }

        Console.WriteLine(sb.ToString());
    }
}
