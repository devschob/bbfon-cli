using System.Net.NetworkInformation;

namespace BBFon.Services;

public sealed class RetryNotificationService : INotificationService
{
    private readonly INotificationService _inner;
    private const int MaxAttempts       = 3;
    private const int RetryDelaySeconds = 5;
    private const int NetworkWaitSeconds = 30;

    public RetryNotificationService(INotificationService inner) => _inner = inner;

    public async Task SendAsync(string message)
    {
        for (int attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            await WaitForNetworkAsync();

            try
            {
                await _inner.SendAsync(message);
                return;
            }
            catch (Exception ex)
            {
                if (attempt == MaxAttempts)
                    throw;

                ConsoleLog.Warning($"[BBFon] Senden fehlgeschlagen (Versuch {attempt}/{MaxAttempts}): {ex.Message}");
                ConsoleLog.Warning($"[BBFon] Nächster Versuch in {RetryDelaySeconds}s...");
                await Task.Delay(TimeSpan.FromSeconds(RetryDelaySeconds));
            }
        }
    }

    private static async Task WaitForNetworkAsync()
    {
        if (NetworkInterface.GetIsNetworkAvailable()) return;

        ConsoleLog.Warning("[BBFon] Kein Netzwerk erkannt, warte auf Verbindung...");

        var deadline = DateTime.Now.AddSeconds(NetworkWaitSeconds);
        while (!NetworkInterface.GetIsNetworkAvailable() && DateTime.Now < deadline)
            await Task.Delay(2000);

        if (!NetworkInterface.GetIsNetworkAvailable())
            throw new Exception($"Netzwerk nach {NetworkWaitSeconds}s nicht verfügbar.");

        ConsoleLog.Success("[BBFon] Netzwerk verfügbar.");
    }
}
