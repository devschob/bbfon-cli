using System.Text;
using NAudio.Wave;

namespace BBFon.Services;

public sealed class AudioMonitorService : IDisposable
{
    private readonly AppConfig _config;
    private readonly INotificationService _notification;
    private readonly bool _debugMode;
    private readonly CameraRecorderService? _camera;
    private WaveInEvent? _waveIn;
    private volatile bool _stopping;

    private bool _isRecording;
    private WaveFileWriter? _waveWriter;
    private string? _currentWavPath;
    private DateTime _recordingStopAt;
    private Task<string?>? _currentCameraTask;

    private sealed class TriggerState
    {
        public DateTime LastSent = DateTime.MinValue;
        public readonly List<DateTime> Timestamps = [];
        public DateTime? AboveThresholdSince = null;
    }

    private TriggerState[] _triggerStates;

    public AudioMonitorService(AppConfig config, INotificationService notification, bool debugMode, CameraRecorderService? camera = null)
    {
        _config = config;
        _notification = notification;
        _debugMode = debugMode;
        _camera = camera;
        _triggerStates = CreateStates(config.Triggers.Count);
    }

    private static TriggerState[] CreateStates(int count)
    {
        var states = new TriggerState[count];
        for (int i = 0; i < count; i++) states[i] = new TriggerState();
        return states;
    }

    public void Start(CancellationToken ct)
    {
        _waveIn = CreateWaveIn();
        _waveIn.StartRecording();

        var deviceLabel = string.IsNullOrWhiteSpace(_config.AudioDevice) ? "Standard-Mikrofon" : $"\"{_config.AudioDevice}\"";
        ConsoleLog.Info($"[BBFon] Überwache {deviceLabel}...");

        for (int i = 0; i < _config.Triggers.Count; i++)
        {
            var t = _config.Triggers[i];
            var parts = new List<string>();
            if (t.Analysis.Enabled)
                parts.Add($"Analyse: mind. {t.Analysis.MinTriggerCount}x in {t.Analysis.WindowSeconds}s");
            if (t.MinDurationSeconds > 0)
                parts.Add($"min. {t.MinDurationSeconds}s Dauer");
            var analyseInfo = parts.Count > 0 ? string.Join(", ", parts) : "direkt";
            var recInfo = t.IsRecording ? ", Aufnahme" : "";
            ConsoleLog.Info($"[BBFon]   T{i + 1}: Schwellwert {t.Threshold:F3}, Cooldown {t.CooldownSeconds}s, {analyseInfo}{recInfo} → \"{t.Message}\"");
        }

        bool anyRecording = _config.Triggers.Any(t => t.IsRecording);
        if (anyRecording)
        {
            var limits = new List<string>();
            if (_config.Recording.MaxFiles > 0) limits.Add($"max. {_config.Recording.MaxFiles} Dateien");
            if (_config.Recording.MaxAgeDays > 0) limits.Add($"max. {_config.Recording.MaxAgeDays} Tage");
            var limitInfo = limits.Count > 0 ? $", Bereinigung: {string.Join(", ", limits)}" : "";
            ConsoleLog.Info($"[BBFon] Audio-Aufnahme bei Alarm: aktiv (max. {_config.Recording.DurationSeconds}s, WAV neben EXE{limitInfo})");
        }

        if (_camera != null)
        {
            var fmt = _config.Camera.Format.ToUpperInvariant();
            var mux = _config.Camera.MuxWithAudio && anyRecording && _config.Camera.Enabled ? ", mit Audio" : "";
            ConsoleLog.Info($"[BBFon] Kamera-Aufnahme bei Alarm: aktiv ({_config.Recording.DurationSeconds}s, {fmt}{mux})");
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

    public static List<string> ListDevices()
    {
        var devices = new List<string>();
        for (int i = 0; i < WaveInEvent.DeviceCount; i++)
            devices.Add(WaveInEvent.GetCapabilities(i).ProductName);
        return devices;
    }

    private WaveInEvent CreateWaveIn()
    {
        int deviceNumber = 0;
        if (!string.IsNullOrWhiteSpace(_config.AudioDevice))
        {
            for (int i = 0; i < WaveInEvent.DeviceCount; i++)
            {
                if (WaveInEvent.GetCapabilities(i).ProductName.Contains(_config.AudioDevice, StringComparison.OrdinalIgnoreCase))
                {
                    deviceNumber = i;
                    break;
                }
            }
        }

        var waveIn = new WaveInEvent
        {
            DeviceNumber       = deviceNumber,
            WaveFormat         = new WaveFormat(16000, 1),
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

        // Hot-reload: Trigger-Zustand anpassen wenn Anzahl geändert
        if (_triggerStates.Length != _config.Triggers.Count)
        {
            var newStates = new TriggerState[_config.Triggers.Count];
            for (int i = 0; i < newStates.Length; i++)
                newStates[i] = i < _triggerStates.Length ? _triggerStates[i] : new TriggerState();
            _triggerStates = newStates;
        }

        // Laufende Aufnahme bedienen
        if (_isRecording)
        {
            if (now <= _recordingStopAt)
                _waveWriter!.Write(e.Buffer, 0, e.BytesRecorded);
            else
                StopRecording();
        }

        // Trigger-Analyse: Timestamps aktualisieren und Zustand berechnen
        int n = _config.Triggers.Count;
        var aboveThreshold = new bool[n];
        var triggerCount   = new int[n];
        var durationSecs   = new double[n];
        var analysisOk     = new bool[n];
        var cooldownOk     = new bool[n];

        for (int i = 0; i < n; i++)
        {
            var trigger = _config.Triggers[i];
            var state   = _triggerStates[i];
            float thr   = trigger.Threshold / AppConfig.ThresholdScale;
            aboveThreshold[i] = rms >= thr;

            // Kontinuierliche Dauer tracken
            if (aboveThreshold[i])
                state.AboveThresholdSince ??= now;
            else
                state.AboveThresholdSince = null;
            durationSecs[i] = state.AboveThresholdSince.HasValue
                ? (now - state.AboveThresholdSince.Value).TotalSeconds : 0;

            if (trigger.Analysis.Enabled)
            {
                if (aboveThreshold[i]) state.Timestamps.Add(now);
                state.Timestamps.RemoveAll(t => (now - t).TotalSeconds > trigger.Analysis.WindowSeconds);
                triggerCount[i] = state.Timestamps.Count;
            }

            bool countOk    = !trigger.Analysis.Enabled || triggerCount[i] >= trigger.Analysis.MinTriggerCount;
            bool durationOk = trigger.MinDurationSeconds <= 0 || durationSecs[i] >= trigger.MinDurationSeconds;
            analysisOk[i]   = countOk && durationOk;
            cooldownOk[i]   = (now - state.LastSent).TotalSeconds >= trigger.CooldownSeconds;
        }

        // Rollende Konsolenzeile
        bool anyAlarmOk = Array.Exists(analysisOk, x => x);
        bool anyAbove   = Array.Exists(aboveThreshold, x => x);
        var lineColor = anyAlarmOk ? ConsoleColor.Red : anyAbove ? ConsoleColor.Yellow : ConsoleColor.Gray;

        var sb = new StringBuilder($"\r[{now:HH:mm:ss}] Lautstärke: {rms * AppConfig.ThresholdScale:F3}");
        for (int i = 0; i < n; i++)
        {
            var trigger = _config.Triggers[i];
            var state   = _triggerStates[i];
            if (trigger.Analysis.Enabled)
                sb.Append($"  T{i + 1}:{triggerCount[i]}/{trigger.Analysis.MinTriggerCount}");
            else if (aboveThreshold[i])
                sb.Append($"  T{i + 1}:[!]");
            if (trigger.MinDurationSeconds > 0 && aboveThreshold[i])
                sb.Append($"({durationSecs[i]:F1}s/{trigger.MinDurationSeconds}s)");
            if (_debugMode && analysisOk[i] && !cooldownOk[i])
                sb.Append($"(cd:{(trigger.CooldownSeconds - (now - state.LastSent).TotalSeconds):F0}s)");
        }
        sb.Append("   ");
        ConsoleLog.Inline(sb.ToString(), lineColor);

        // Alarm auslösen
        string? alarmTimestamp = null;
        for (int i = 0; i < n; i++)
        {
            if (!analysisOk[i] || !cooldownOk[i]) continue;

            var trigger = _config.Triggers[i];

            // Unterdrücken wenn ein Trigger mit höherem Threshold ebenfalls feuert
            if (trigger.SuppressIfHigherFires)
            {
                bool higherFires = false;
                for (int j = 0; j < n; j++)
                {
                    if (j != i && analysisOk[j] && cooldownOk[j] && _config.Triggers[j].Threshold > trigger.Threshold)
                    {
                        higherFires = true;
                        break;
                    }
                }
                if (higherFires) continue;
            }
            var state   = _triggerStates[i];
            state.LastSent = now;
            state.Timestamps.Clear();

            ConsoleLog.Alarm($"\n[{now:HH:mm:ss}] ALARM T{i + 1}! Lautstärke {rms * AppConfig.ThresholdScale:F3} >= {trigger.Threshold:F3}. Sende Nachricht...");

            if (trigger.IsRecording)
            {
                alarmTimestamp ??= now.ToString("yyyy-MM-dd_HH-mm-ss");

                // Kamera starten (nur eine Aufnahme gleichzeitig)
                if (_camera != null && (_currentCameraTask == null || _currentCameraTask.IsCompleted))
                    _currentCameraTask = Task.Run(() => _camera.RecordAsync(alarmTimestamp));

                // Audio-Aufnahme starten (nur eine gleichzeitig)
                if (!_isRecording)
                    StartRecording(e.Buffer, e.BytesRecorded, alarmTimestamp, _config.Recording.DurationSeconds);
            }

            _ = SendNotificationAsync(trigger.Message, trigger.CooldownSeconds);
        }
    }

    private void StartRecording(byte[] firstBuffer, int bytesRecorded, string timestamp, double durationSeconds)
    {
        var filename = $"{timestamp}.wav";
        var path = Path.Combine(AppContext.BaseDirectory, filename);
        _waveWriter = new WaveFileWriter(path, _waveIn!.WaveFormat);
        _currentWavPath = path;
        _recordingStopAt = DateTime.Now.AddSeconds(durationSeconds);
        _isRecording = true;
        _waveWriter.Write(firstBuffer, 0, bytesRecorded);
        ConsoleLog.Info($"[{DateTime.Now:HH:mm:ss}] Audio-Aufnahme gestartet: {filename}");
    }

    private void StopRecording()
    {
        _isRecording = false;
        _waveWriter?.Dispose();
        _waveWriter = null;
        ConsoleLog.Info($"\n[{DateTime.Now:HH:mm:ss}] Audio-Aufnahme beendet ({_config.Recording.DurationSeconds}s).");

        var wavPath    = _currentWavPath;
        var cameraTask = _currentCameraTask;
        _currentWavPath    = null;
        _currentCameraTask = null;

        _ = Task.Run(async () =>
        {
            // Kamera-Aufnahme abwarten
            string? videoPath = null;
            if (cameraTask != null)
            {
                videoPath = await cameraTask;
                if (videoPath != null)
                    ConsoleLog.Success($"[{DateTime.Now:HH:mm:ss}] Kamera-Aufnahme: {Path.GetFileName(videoPath)}");
            }

            // WAV in Video einbetten – vor Komprimierung, damit WAV noch vorhanden ist
            if (_camera != null && videoPath != null && _config.Camera.MuxWithAudio
                && wavPath != null && File.Exists(wavPath))
            {
                var muxed = await _camera.MuxAsync(videoPath, wavPath);
                if (muxed != null)
                    ConsoleLog.Success($"[BBFon] Audio eingebettet: {Path.GetFileName(muxed)}");
            }

            // Audio komprimieren – endgültigen Pfad für Anhang merken
            string? finalAudioPath = wavPath;
            if (_config.Compression.Enabled && wavPath != null)
            {
                var compressor = new AudioCompressorService(_config.Compression, _config.FfmpegPath);
                var result = await compressor.CompressAsync(wavPath);
                if (result != null)
                {
                    ConsoleLog.Success($"[BBFon] Komprimiert: {Path.GetFileName(result)}");
                    finalAudioPath = result;
                }
            }

            // Anhänge senden (nach Muxing + Komprimierung, damit endgültige Dateien vorliegen)
            var attachments = new List<string>();
            if (finalAudioPath != null && File.Exists(finalAudioPath))
                attachments.Add(finalAudioPath);
            if (videoPath != null && File.Exists(videoPath))
                attachments.Add(videoPath);
            await TrySendAttachmentsAsync(attachments);

            CleanupRecordings();
        });
    }

    private void CleanupRecordings()
    {
        var cfg = _config.Recording;
        if (cfg.MaxFiles <= 0 && cfg.MaxAgeDays <= 0) return;

        var patterns = new[] {
            "????-??-??_??-??-??.wav",     "????-??-??_??-??-??.mp3",
            "????-??-??_??-??-??.ogg",     "????-??-??_??-??-??.m4a",
            "????-??-??_??-??-??_cam.mp4", "????-??-??_??-??-??_cam.avi",
            "????-??-??_??-??-??_cam.mkv", "????-??-??_??-??-??_cam.gif"
        };
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

    private async Task TrySendAttachmentsAsync(IReadOnlyList<string> attachments)
    {
        if (!_config.Recording.SendAttachments || attachments.Count == 0) return;
        try
        {
            await _notification.SendAsync("", attachments);
            ConsoleLog.Success($"[BBFon] {attachments.Count} Anhang/Anhänge gesendet.");
        }
        catch (Exception ex)
        {
            ConsoleLog.Error($"[BBFon] Anhänge senden fehlgeschlagen: {ex.Message}");
        }
    }

    private async Task SendNotificationAsync(string message, int cooldownSeconds)
    {
        try
        {
            await _notification.SendAsync(message);
            ConsoleLog.Success($"[{DateTime.Now:HH:mm:ss}] Nachricht gesendet. Cooldown: {cooldownSeconds}s");
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
