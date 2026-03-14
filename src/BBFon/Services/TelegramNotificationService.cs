using System.Text;
using System.Text.Json;

namespace BBFon.Services;

public class TelegramNotificationService : INotificationService
{
    private readonly TelegramConfig _config;
    private static readonly HttpClient Http = new();

    public TelegramNotificationService(TelegramConfig config) => _config = config;

    public async Task SendAsync(string message, IReadOnlyList<string>? attachments = null)
    {
        ConsoleLog.Info("[BBFon] Sende Telegram...");
        if (!string.IsNullOrEmpty(message))
            await SendTextAsync(message);

        if (attachments != null)
            foreach (var file in attachments)
                await SendDocumentAsync(file);
    }

    private async Task SendTextAsync(string message)
    {
        var url     = $"https://api.telegram.org/bot{_config.BotToken}/sendMessage";
        var payload = JsonSerializer.Serialize(new { chat_id = _config.ChatId, text = message });
        var content = new StringContent(payload, Encoding.UTF8, "application/json");
        var response = await Http.PostAsync(url, content);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();
            throw new Exception($"Telegram Fehler ({(int)response.StatusCode}): {body}");
        }
    }

    private async Task SendDocumentAsync(string filePath)
    {
        var url = $"https://api.telegram.org/bot{_config.BotToken}/sendDocument";

        using var form       = new MultipartFormDataContent();
        using var fileStream = File.OpenRead(filePath);

        form.Add(new StringContent(_config.ChatId),                              "chat_id");
        form.Add(new StreamContent(fileStream), "document", Path.GetFileName(filePath));

        var response = await Http.PostAsync(url, form);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();
            throw new Exception($"Telegram sendDocument Fehler ({(int)response.StatusCode}): {body}");
        }
    }
}
