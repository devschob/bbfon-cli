namespace BBFon.Services;

public interface INotificationService
{
    Task SendAsync(string message, IReadOnlyList<string>? attachments = null);
}
