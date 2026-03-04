using System.Diagnostics;

namespace BBFon.Services;

public sealed class AudioCompressorService
{
    private readonly CompressionConfig _config;

    public AudioCompressorService(CompressionConfig config) => _config = config;

    /// <summary>
    /// Compresses a WAV file using ffmpeg. Returns the path of the compressed file,
    /// or null if compression failed.
    /// </summary>
    public async Task<string?> CompressAsync(string wavPath)
    {
        var ffmpegPath = Path.IsPathRooted(_config.FfmpegPath)
            ? _config.FfmpegPath
            : Path.Combine(AppContext.BaseDirectory, _config.FfmpegPath);

        var ext = _config.Format.ToLowerInvariant() switch
        {
            "mp3"  => ".mp3",
            "aac"  => ".m4a",
            "opus" => ".ogg",
            _      => $".{_config.Format}"
        };

        var outPath = Path.ChangeExtension(wavPath, ext);

        // ffmpeg codec per format
        var codec = _config.Format.ToLowerInvariant() switch
        {
            "mp3"  => "libmp3lame",
            "aac"  => "aac",
            "opus" => "libopus",
            _      => _config.Format
        };

        var args = $"-y -i \"{wavPath}\" -c:a {codec} -b:a {_config.BitrateKbps}k \"{outPath}\"";

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

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            await process.WaitForExitAsync(cts.Token);

            if (process.ExitCode != 0)
            {
                var stderr = await process.StandardError.ReadToEndAsync();
                throw new InvalidOperationException($"ffmpeg Exit {process.ExitCode}: {stderr.Trim()}");
            }

            if (!_config.KeepWavAudio)
                File.Delete(wavPath);

            return outPath;
        }
        catch (Exception ex)
        {
            ConsoleLog.Warning($"[BBFon] Komprimierung fehlgeschlagen ({Path.GetFileName(wavPath)}): {ex.Message}");
            return null;
        }
    }
}
