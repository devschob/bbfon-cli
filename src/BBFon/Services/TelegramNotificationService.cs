using System.Text;
using System.Text.Json;

namespace BBFon.Services;

public class TelegramNotificationService : INotificationService
{
    private readonly TelegramConfig _config;
    private static readonly HttpClient Http = new();

    public TelegramNotificationService(TelegramConfig config) => _config = config;

    public async Task SendAsync(string message)
    {
        var url = $"https://api.telegram.org/bot{_config.BotToken}/sendMessage";

        var payload = JsonSerializer.Serialize(new
        {
            chat_id = _config.ChatId,
            text = message
        });

        var content = new StringContent(payload, Encoding.UTF8, "application/json");
        var response = await Http.PostAsync(url, content);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();
            throw new Exception($"Telegram Fehler ({(int)response.StatusCode}): {body}");
        }
    }
}
