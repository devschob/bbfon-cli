using System.Diagnostics;
using System.Text.RegularExpressions;

namespace BBFon.Services;

public sealed class CameraRecorderService
{
    private readonly CameraConfig _config;
    private string? _resolvedDevice;

    public CameraRecorderService(CameraConfig config) => _config = config;

    /// <summary>
    /// Nimmt Video auf und gibt den Pfad der fertigen Datei zurück (null bei Fehler).
    /// Bei Format "gif": erst MP4 aufnehmen, dann konvertieren.
    /// </summary>
    public async Task<string?> RecordAsync(string timestamp)
    {
        var ffmpegPath = ResolveFfmpegPath();
        var device = await ResolveDeviceAsync(ffmpegPath);
        if (device == null) return null;

        bool makeGif = _config.Format.Equals("gif", StringComparison.OrdinalIgnoreCase);

        var videoExt  = makeGif ? "mp4" : _config.Format.TrimStart('.').ToLowerInvariant();
        var videoName = makeGif ? $"{timestamp}_cam_tmp.mp4" : $"{timestamp}_cam.{videoExt}";
        var videoPath = Path.Combine(AppContext.BaseDirectory, videoName);

        var recordArgs = $"-f dshow -i video=\"{device}\" -t {_config.DurationSeconds} -y \"{videoPath}\"";
        if (!await RunFfmpegAsync(ffmpegPath, recordArgs, _config.DurationSeconds + 15, "Kamera-Aufnahme"))
            return null;

        if (!makeGif)
            return videoPath;

        // MP4 → GIF
        var gifPath = Path.Combine(AppContext.BaseDirectory, $"{timestamp}_cam.gif");
        var gifArgs = $"-i \"{videoPath}\" -vf \"fps=10,scale=320:-1:flags=lanczos\" -y \"{gifPath}\"";
        var gifOk = await RunFfmpegAsync(ffmpegPath, gifArgs, 60, "GIF-Konvertierung");
        try { File.Delete(videoPath); } catch { /* ignore */ }
        return gifOk ? gifPath : null;
    }

    /// <summary>
    /// Bettet WAV-Audio in eine Videodatei ein. Überschreibt die Originaldatei.
    /// Nicht möglich bei GIF (kein Audio-Support).
    /// </summary>
    public async Task<string?> MuxAsync(string videoPath, string wavPath)
    {
        if (_config.Format.Equals("gif", StringComparison.OrdinalIgnoreCase))
        {
            ConsoleLog.Warning("[BBFon] Muxing übersprungen: GIF unterstützt kein Audio.");
            return null;
        }

        if (!File.Exists(videoPath) || !File.Exists(wavPath))
            return null;

        var ffmpegPath = ResolveFfmpegPath();
        var tempPath   = videoPath + ".mux_tmp" + Path.GetExtension(videoPath);

        try { File.Move(videoPath, tempPath); }
        catch (Exception ex)
        {
            ConsoleLog.Warning($"[BBFon] Muxing vorbereitung fehlgeschlagen: {ex.Message}");
            return null;
        }

        var args = $"-i \"{tempPath}\" -i \"{wavPath}\" -c:v copy -c:a aac -shortest -y \"{videoPath}\"";
        var ok = await RunFfmpegAsync(ffmpegPath, args, 60, "Audio-Muxing");

        try { File.Delete(tempPath); } catch { /* ignore */ }

        if (!ok)
        {
            try { if (!File.Exists(videoPath)) File.Move(tempPath, videoPath); } catch { /* ignore */ }
            return null;
        }

        return videoPath;
    }

    public async Task<List<string>> ListDevicesAsync()
        => await QueryDevicesAsync(ResolveFfmpegPath());

    private string ResolveFfmpegPath() =>
        Path.IsPathRooted(_config.FfmpegPath)
            ? _config.FfmpegPath
            : Path.Combine(AppContext.BaseDirectory, _config.FfmpegPath);

    private async Task<string?> ResolveDeviceAsync(string ffmpegPath)
    {
        if (!string.IsNullOrWhiteSpace(_config.DeviceName))
            return _config.DeviceName;

        if (_resolvedDevice != null)
            return _resolvedDevice;

        var devices = await QueryDevicesAsync(ffmpegPath);
        if (devices.Count == 0)
        {
            ConsoleLog.Warning("[BBFon] Kamera: Kein DirectShow-Videogerät gefunden.");
            return null;
        }

        _resolvedDevice = devices[0];
        ConsoleLog.Info($"[BBFon] Kamera: Gerät automatisch erkannt: \"{_resolvedDevice}\"");
        return _resolvedDevice;
    }

    private async Task<bool> RunFfmpegAsync(string ffmpegPath, string args, int timeoutSeconds, string label)
    {
        var psi = new ProcessStartInfo
        {
            FileName               = ffmpegPath,
            Arguments              = args,
            UseShellExecute        = false,
            RedirectStandardOutput = true,
            RedirectStandardError  = true,
            CreateNoWindow         = true
        };

        try
        {
            using var process = Process.Start(psi)
                ?? throw new InvalidOperationException("ffmpeg konnte nicht gestartet werden.");

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
            await process.WaitForExitAsync(cts.Token);

            if (process.ExitCode != 0)
            {
                var stderr = await process.StandardError.ReadToEndAsync();
                throw new InvalidOperationException($"Exit {process.ExitCode}: {stderr.Trim()}");
            }

            return true;
        }
        catch (Exception ex)
        {
            ConsoleLog.Warning($"[BBFon] {label} fehlgeschlagen: {ex.Message}");
            return false;
        }
    }

    private static async Task<List<string>> QueryDevicesAsync(string ffmpegPath)
    {
        var psi = new ProcessStartInfo
        {
            FileName               = ffmpegPath,
            Arguments              = "-list_devices true -f dshow -i dummy",
            UseShellExecute        = false,
            RedirectStandardOutput = true,
            RedirectStandardError  = true,
            CreateNoWindow         = true
        };

        var devices = new List<string>();

        try
        {
            using var process = Process.Start(psi)
                ?? throw new InvalidOperationException("ffmpeg nicht gefunden.");

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var stderr = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync(cts.Token);

            bool inVideoSection = false;
            foreach (var line in stderr.Split('\n'))
            {
                if (line.Contains("DirectShow video devices"))
                {
                    inVideoSection = true;
                    continue;
                }
                if (line.Contains("DirectShow audio devices"))
                    break;

                if (inVideoSection)
                {
                    var match = Regex.Match(line, "\"([^\"]+)\"");
                    if (match.Success && !line.Contains("Alternative name"))
                        devices.Add(match.Groups[1].Value);
                }
            }
        }
        catch (Exception ex)
        {
            ConsoleLog.Warning($"[BBFon] Kamera-Geräteerkennung fehlgeschlagen: {ex.Message}");
        }

        return devices;
    }
}
