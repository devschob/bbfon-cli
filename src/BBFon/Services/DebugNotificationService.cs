namespace BBFon.Services;

public sealed class DebugNotificationService : INotificationService
{
    public Task SendAsync(string message, IReadOnlyList<string>? attachments = null)
    {
        if (!string.IsNullOrEmpty(message))
            Console.WriteLine($"[DEBUG] Würde senden: \"{message}\"");
        if (attachments?.Count > 0)
            foreach (var a in attachments)
                Console.WriteLine($"[DEBUG] Würde Anhang senden: {Path.GetFileName(a)}");
        return Task.CompletedTask;
    }
}
