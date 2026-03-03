namespace BBFon.Services;

public interface INotificationService
{
    Task SendAsync(string message);
}
