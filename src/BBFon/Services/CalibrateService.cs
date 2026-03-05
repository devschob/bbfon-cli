using NAudio.Wave;

namespace BBFon.Services;

public sealed class CalibrateService
{
    public async Task RunAsync()
    {
        ConsoleLog.Info("[BBFon] Kalibrierung startet in 3 Sekunden...");
        ConsoleLog.Info("[BBFon] Bitte STILLE halten – kein Sprechen, keine Geräusche.\n");
        await Task.Delay(3000);

        var samples = new List<float>();
        var done = new TaskCompletionSource();

        using var waveIn = new WaveInEvent
        {
            WaveFormat = new WaveFormat(16000, 1),
            BufferMilliseconds = 100
        };

        waveIn.DataAvailable += (_, e) =>
        {
            float rms = CalculateRms(e.Buffer, e.BytesRecorded);
            samples.Add(rms);
            double elapsed = samples.Count * 0.1;
            ConsoleLog.Inline($"\r[BBFon] Messe... {elapsed:F1}s / 10s   Pegel: {rms * AppConfig.ThresholdScale:F3}   ",
                rms > 0.05f ? ConsoleColor.Yellow : ConsoleColor.Gray);
        };

        waveIn.RecordingStopped += (_, _) => done.TrySetResult();
        waveIn.StartRecording();

        await Task.Delay(10000);
        waveIn.StopRecording();
        await done.Task;

        Console.WriteLine();

        if (samples.Count == 0)
        {
            ConsoleLog.Error("[BBFon] Keine Samples aufgenommen. Mikrofon verfügbar?");
            return;
        }

        float mean   = samples.Average();
        float stddev = MathF.Sqrt(samples.Select(s => (s - mean) * (s - mean)).Average());
        float max    = samples.Max();
        float suggested = mean + 3 * stddev;
        suggested = Math.Max(suggested, 0.05f); // Minimum
        float suggestedScaled = MathF.Round(suggested * AppConfig.ThresholdScale, 3);

        ConsoleLog.Info("\n[BBFon] Kalibrierungsergebnis:");
        ConsoleLog.Info($"  Durchschnittspegel:    {mean * AppConfig.ThresholdScale:F3}");
        ConsoleLog.Info($"  Maximaler Pegel:       {max * AppConfig.ThresholdScale:F3}");
        ConsoleLog.Info($"  Standardabweichung:    {stddev * AppConfig.ThresholdScale:F3}");
        ConsoleLog.Success($"  Empfohlener Threshold: {suggestedScaled:F3}");
        ConsoleLog.Info($"\n[BBFon] Trage in appsettings.json ein:");
        ConsoleLog.Info($"  \"Threshold\": {suggestedScaled:F3}");

        if (max > suggested)
            ConsoleLog.Warning($"\n[BBFon] Hinweis: Es gab Spitzen bis {max * AppConfig.ThresholdScale:F3} während der Stille. War es wirklich ruhig?");

        DrawBarChart(samples, suggested);
    }

    private static void DrawBarChart(List<float> samples, float threshold)
    {
        const int barWidth    = 42;
        float maxVal          = samples.Max() * AppConfig.ThresholdScale;
        float thresholdScaled = threshold * AppConfig.ThresholdScale;
        float scale           = Math.Max(maxVal, thresholdScaled) * 1.1f;
        int threshPos         = Math.Clamp((int)(thresholdScaled / scale * barWidth), 0, barWidth - 1);

        ConsoleLog.Info("\n[BBFon] Verlauf (100ms Intervalle):");

        for (int i = 0; i < samples.Count; i++)
        {
            float val  = samples[i] * AppConfig.ThresholdScale;
            int filled = (int)(val / scale * barWidth);
            bool over  = val >= thresholdScaled;

            var bar = new char[barWidth];
            for (int j = 0; j < barWidth; j++)
                bar[j] = j == threshPos ? '|' : (j < filled ? '█' : '░');

            var color = over ? ConsoleColor.Red : ConsoleColor.Gray;
            ConsoleLog.Inline($"  [{i * 0.1:F1}s] {new string(bar)}  {val,7:F3}{(over ? " !" : "")}\n", color);
        }

        // Legende
        ConsoleLog.Info($"  {"".PadLeft(8 + threshPos)}^ {thresholdScaled:F3} (Schwellwert)");
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
}
