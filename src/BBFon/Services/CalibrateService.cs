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
            ConsoleLog.Inline($"\r[BBFon] Messe... {elapsed:F1}s / 10s   Pegel: {rms:F3}   ",
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
        float suggested = MathF.Round(mean + 3 * stddev, 2);
        suggested = Math.Max(suggested, 0.05f); // Minimum

        ConsoleLog.Info("\n[BBFon] Kalibrierungsergebnis:");
        ConsoleLog.Info($"  Durchschnittspegel:    {mean:F3}");
        ConsoleLog.Info($"  Maximaler Pegel:       {max:F3}");
        ConsoleLog.Info($"  Standardabweichung:    {stddev:F3}");
        ConsoleLog.Success($"  Empfohlener Threshold: {suggested:F2}");
        ConsoleLog.Info($"\n[BBFon] Trage in appsettings.json ein:");
        ConsoleLog.Info($"  \"Threshold\": {suggested:F2}");

        if (max > suggested)
            ConsoleLog.Warning($"\n[BBFon] Hinweis: Es gab Spitzen bis {max:F3} während der Stille. War es wirklich ruhig?");
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
