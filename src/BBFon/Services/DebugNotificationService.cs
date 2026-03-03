namespace BBFon.Services;

public sealed class DebugNotificationService : INotificationService
{
    public Task SendAsync(string message)
    {
        Console.WriteLine($"[DEBUG] Würde senden: \"{message}\"");
        return Task.CompletedTask;
    }
}
