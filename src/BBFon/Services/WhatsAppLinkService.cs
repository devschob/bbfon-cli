using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace BBFon.Services;

public sealed class WhatsAppLinkService
{
    private readonly WhatsAppConfig _config;

    public WhatsAppLinkService(WhatsAppConfig config) => _config = config;

    public async Task RunAsync(string? phoneNumber = null)
    {
        if (phoneNumber != null)
            SaveNumberToAppSettings(phoneNumber);

        var cliPath = Path.IsPathRooted(_config.CliPath)
            ? _config.CliPath
            : Path.Combine(AppContext.BaseDirectory, _config.CliPath);

        ConsoleLog.Info("[BBFon] Starte WhatsApp-Verlinkung...");
        ConsoleLog.Info("[BBFon] mudslide wird gestartet, bitte warten...\n");

        var psi = new ProcessStartInfo
        {
            FileName = cliPath,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = false
        };
        psi.ArgumentList.Add("login");

        Process? process;
        try
        {
            process = Process.Start(psi);
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or FileNotFoundException)
        {
            ConsoleLog.Error($"[BBFon] mudslide konnte nicht gestartet werden – Pfad prüfen: \"{cliPath}\"");
            ConsoleLog.Error("[BBFon] mudslide herunterladen: https://github.com/robvanderleek/mudslide/releases");
            return;
        }

        if (process == null)
        {
            ConsoleLog.Error($"[BBFon] mudslide konnte nicht gestartet werden – Pfad prüfen: \"{cliPath}\"");
            return;
        }

        // stdout und stderr direkt an die Konsole weiterleiten (QR-Code wird von mudslide selbst gerendert)
        _ = Task.Run(async () =>
        {
            while (!process.StandardOutput.EndOfStream)
            {
                var line = await process.StandardOutput.ReadLineAsync();
                if (line != null) Console.WriteLine(line);
            }
        });

        _ = Task.Run(async () =>
        {
            while (!process.StandardError.EndOfStream)
            {
                var line = await process.StandardError.ReadLineAsync();
                if (line != null) Console.Error.WriteLine(line);
            }
        });

        ConsoleLog.Info("[BBFon] Scanne den QR-Code mit deiner WhatsApp-App:");
        ConsoleLog.Info("[BBFon]   WhatsApp → Einstellungen → Verknüpfte Geräte → Gerät hinzufügen");
        ConsoleLog.Info("[BBFon] Warte auf Verlinkung...\n");

        await process.WaitForExitAsync();

        if (process.ExitCode == 0)
            ConsoleLog.Success("[BBFon] Erfolgreich verknüpft! BBFon kann jetzt WhatsApp nutzen.");
        else
            ConsoleLog.Error($"[BBFon] Verlinkung fehlgeschlagen (Exit {process.ExitCode}). WhatsApp-App nochmal versuchen.");
    }

    private static void SaveNumberToAppSettings(string phoneNumber)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
        var json = File.ReadAllText(path);
        var root = JsonNode.Parse(json)?.AsObject()
            ?? throw new InvalidOperationException("appsettings.json konnte nicht geparst werden.");

        var wa = root["WhatsApp"]?.AsObject() ?? new JsonObject();
        wa["Sender"]    = phoneNumber;
        wa["Recipient"] = phoneNumber;
        root["WhatsApp"] = wa;

        var opts = new JsonSerializerOptions { WriteIndented = true };
        File.WriteAllText(path, root.ToJsonString(opts));

        ConsoleLog.Info($"[BBFon] Nummer {phoneNumber} in appsettings.json eingetragen (Sender + Recipient).");
    }
}
