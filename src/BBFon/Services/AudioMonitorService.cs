using NAudio.Wave;

namespace BBFon.Services;

public sealed class AudioMonitorService : IDisposable
{
    private readonly AppConfig _config;
    private readonly INotificationService _notification;
    private readonly bool _recordingEnabled;
    private readonly bool _debugMode;
    private WaveInEvent? _waveIn;
    private DateTime _lastSent = DateTime.MinValue;
    private volatile bool _stopping;

    private bool _isRecording;
    private WaveFileWriter? _waveWriter;
    private string? _currentWavPath;
    private DateTime _recordingStopAt;

    private readonly List<DateTime> _triggerTimestamps = [];

    public AudioMonitorService(AppConfig config, INotificationService notification, bool recordingEnabled, bool debugMode)
    {
        _config = config;
        _notification = notification;
        _recordingEnabled = recordingEnabled;
        _debugMode = debugMode;
    }

    public void Start(CancellationToken ct)
    {
        _waveIn = CreateWaveIn();
        _waveIn.StartRecording();

        ConsoleLog.Info($"[BBFon] Überwache Standard-Mikrofon... (Schwellwert: {_config.Threshold:F2})");

        if (_config.Analysis.Enabled)
            ConsoleLog.Info($"[BBFon] Analyse aktiv: mind. {_config.Analysis.MinTriggerCount}x Trigger in {_config.Analysis.WindowSeconds}s");

        if (_recordingEnabled)
        {
            var limits = new List<string>();
            if (_config.Recording.MaxFiles > 0) limits.Add($"max. {_config.Recording.MaxFiles} Dateien");
            if (_config.Recording.MaxAgeDays > 0) limits.Add($"max. {_config.Recording.MaxAgeDays} Tage");
            var limitInfo = limits.Count > 0 ? $", Bereinigung: {string.Join(", ", limits)}" : "";
            ConsoleLog.Info($"[BBFon] Aufnahme bei Alarm: aktiv (max. 10s, WAV neben EXE{limitInfo})");
        }

        if (_debugMode)
            ConsoleLog.Debug("[BBFon] DEBUG-Modus: Nachrichten werden NICHT gesendet.");

        ConsoleLog.Info("[BBFon] Beenden: Strg+C, Q oder Esc\n");

        ct.WaitHandle.WaitOne();

        _stopping = true;
        _waveIn.StopRecording();
        if (_isRecording)
            StopRecording();
    }

    private WaveInEvent CreateWaveIn()
    {
        var waveIn = new WaveInEvent
        {
            WaveFormat = new WaveFormat(16000, 1),
            BufferMilliseconds = 100
        };
        waveIn.DataAvailable += OnDataAvailable;
        waveIn.RecordingStopped += OnRecordingStopped;
        return waveIn;
    }

    private void OnRecordingStopped(object? sender, StoppedEventArgs e)
    {
        if (e.Exception != null && !_stopping)
        {
            ConsoleLog.Warning($"\n[BBFon] Mikrofon getrennt: {e.Exception.Message}");
            _ = TryReconnectAsync();
        }
    }

    private async Task TryReconnectAsync()
    {
        int attempt = 0;
        while (!_stopping)
        {
            attempt++;
            await Task.Delay(3000);
            if (_stopping) return;

            ConsoleLog.Info($"[BBFon] Mikrofon-Reconnect, Versuch {attempt}...");
            try
            {
                _waveIn?.Dispose();
                _waveIn = CreateWaveIn();
                _waveIn.StartRecording();
                ConsoleLog.Success("[BBFon] Mikrofon wiederverbunden.");
                return;
            }
            catch (Exception ex)
            {
                ConsoleLog.Warning($"[BBFon] Mikrofon nicht verfügbar ({ex.Message}), nächster Versuch in 3s...");
            }
        }
    }

    private void OnDataAvailable(object? sender, WaveInEventArgs e)
    {
        var now = DateTime.Now;
        float rms = CalculateRms(e.Buffer, e.BytesRecorded);

        // Laufende Aufnahme bedienen
        if (_isRecording)
        {
            if (now <= _recordingStopAt)
                _waveWriter!.Write(e.Buffer, 0, e.BytesRecorded);
            else
                StopRecording();
        }

        // Trigger-Analyse
        bool aboveThreshold = rms >= _config.Threshold;
        int triggerCount = 0;

        if (_config.Analysis.Enabled)
        {
            if (aboveThreshold)
                _triggerTimestamps.Add(now);
            _triggerTimestamps.RemoveAll(t => (now - t).TotalSeconds > _config.Analysis.WindowSeconds);
            triggerCount = _triggerTimestamps.Count;
        }

        bool analysisOk  = _config.Analysis.Enabled ? triggerCount >= _config.Analysis.MinTriggerCount : aboveThreshold;
        bool cooldownOk  = (now - _lastSent).TotalSeconds >= _config.CooldownSeconds;

        // Rollende Konsolenzeile
        var lineColor = analysisOk ? ConsoleColor.Red : aboveThreshold ? ConsoleColor.Yellow : ConsoleColor.Gray;
        if (_config.Analysis.Enabled)
        {
            string cooldownHint = _debugMode && analysisOk && !cooldownOk
                ? $" [Cooldown: {(_config.CooldownSeconds - (now - _lastSent).TotalSeconds):F0}s]" : "";
            ConsoleLog.Inline($"\r[{now:HH:mm:ss}] Lautstärke: {rms:F3}  Trigger: {triggerCount}/{_config.Analysis.MinTriggerCount} (letzte {_config.Analysis.WindowSeconds}s){cooldownHint}   ", lineColor);
        }
        else
        {
            string marker = _debugMode && aboveThreshold ? " [!]" : "";
            string cooldownHint = _debugMode && aboveThreshold && !cooldownOk
                ? $" [Cooldown: {(_config.CooldownSeconds - (now - _lastSent).TotalSeconds):F0}s]" : "";
            ConsoleLog.Inline($"\r[{now:HH:mm:ss}] Lautstärke: {rms:F3}{marker}{cooldownHint}   ", lineColor);
        }

        // Alarm auslösen
        if (analysisOk && cooldownOk)
        {
            _lastSent = now;
            _triggerTimestamps.Clear();

            ConsoleLog.Alarm($"\n[{now:HH:mm:ss}] ALARM! Lautstärke {rms:F3} >= {_config.Threshold:F2}. Sende Nachricht...");

            if (_recordingEnabled && !_isRecording)
                StartRecording(e.Buffer, e.BytesRecorded);

            _ = SendNotificationAsync();
        }
    }

    private void StartRecording(byte[] firstBuffer, int bytesRecorded)
    {
        var filename = $"{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.wav";
        var path = Path.Combine(AppContext.BaseDirectory, filename);
        _waveWriter = new WaveFileWriter(path, _waveIn!.WaveFormat);
        _currentWavPath = path;
        _recordingStopAt = DateTime.Now.AddSeconds(10);
        _isRecording = true;
        _waveWriter.Write(firstBuffer, 0, bytesRecorded);
        ConsoleLog.Info($"[{DateTime.Now:HH:mm:ss}] Aufnahme gestartet: {filename}");
    }

    private void StopRecording()
    {
        _isRecording = false;
        _waveWriter?.Dispose();
        _waveWriter = null;
        ConsoleLog.Info($"\n[{DateTime.Now:HH:mm:ss}] Aufnahme beendet (10s).");

        if (_config.Compression.Enabled && _currentWavPath != null)
        {
            var wavPath = _currentWavPath;
            _ = Task.Run(async () =>
            {
                var compressor = new AudioCompressorService(_config.Compression);
                var result = await compressor.CompressAsync(wavPath);
                if (result != null)
                    ConsoleLog.Success($"[BBFon] Komprimiert: {Path.GetFileName(result)}");
                CleanupRecordings();
            });
        }
        else
        {
            CleanupRecordings();
        }

        _currentWavPath = null;
    }

    private void CleanupRecordings()
    {
        var cfg = _config.Recording;
        if (cfg.MaxFiles <= 0 && cfg.MaxAgeDays <= 0) return;

        // Alle Aufnahme-Dateien (WAV + komprimierte Formate) zusammenfassen
        var patterns = new[] { "????-??-??_??-??-??.wav", "????-??-??_??-??-??.mp3",
                                "????-??-??_??-??-??.ogg", "????-??-??_??-??-??.m4a" };
        var files = patterns
            .SelectMany(p => Directory.GetFiles(AppContext.BaseDirectory, p))
            .Select(f => new FileInfo(f))
            .OrderByDescending(f => f.CreationTime)
            .ToList();

        var toDelete = new HashSet<string>();

        if (cfg.MaxAgeDays > 0)
        {
            var cutoff = DateTime.Now.AddDays(-cfg.MaxAgeDays);
            foreach (var f in files.Where(f => f.CreationTime < cutoff))
                toDelete.Add(f.FullName);
        }

        if (cfg.MaxFiles > 0)
        {
            foreach (var f in files.Skip(cfg.MaxFiles))
                toDelete.Add(f.FullName);
        }

        foreach (var path in toDelete)
        {
            try
            {
                File.Delete(path);
                ConsoleLog.Info($"[BBFon] Aufnahme gelöscht: {Path.GetFileName(path)}");
            }
            catch (Exception ex)
            {
                ConsoleLog.Warning($"[BBFon] Löschen fehlgeschlagen ({Path.GetFileName(path)}): {ex.Message}");
            }
        }
    }

    private async Task SendNotificationAsync()
    {
        try
        {
            await _notification.SendAsync(_config.Message);
            ConsoleLog.Success($"[{DateTime.Now:HH:mm:ss}] Nachricht gesendet. Cooldown: {_config.CooldownSeconds}s");
        }
        catch (Exception ex)
        {
            ConsoleLog.Error($"[{DateTime.Now:HH:mm:ss}] Fehler beim Senden: {ex.Message}");
        }
    }

    private static float CalculateRms(byte[] buffer, int bytesRecorded)
    {
        int samples = bytesRecorded / 2;
        if (samples == 0) return 0f;
        float sum = 0f;
        for (int i = 0; i < bytesRecorded - 1; i += 2)
        {
            float normalized = BitConverter.ToInt16(buffer, i) / 32768f;
            sum += normalized * normalized;
        }
        return MathF.Sqrt(sum / samples);
    }

    public void Dispose()
    {
        _waveWriter?.Dispose();
        _waveIn?.Dispose();
    }
}
