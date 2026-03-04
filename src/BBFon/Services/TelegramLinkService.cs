using System.Text.Json;
using System.Text.Json.Nodes;

namespace BBFon.Services;

public sealed class TelegramLinkService
{
    private static readonly HttpClient Http = new();

    public async Task RunAsync(string botToken)
    {
        ConsoleLog.Info("[BBFon] Rufe Telegram getUpdates ab...");

        JsonDocument doc;
        try
        {
            var url      = $"https://api.telegram.org/bot{botToken}/getUpdates";
            var response = await Http.GetAsync(url);
            var body     = await response.Content.ReadAsStringAsync();
            doc = JsonDocument.Parse(body);

            if (!response.IsSuccessStatusCode)
            {
                var description = doc.RootElement.TryGetProperty("description", out var d) ? d.GetString() : body;
                ConsoleLog.Error($"[BBFon] Telegram API Fehler: {description}");
                return;
            }
        }
        catch (Exception ex)
        {
            ConsoleLog.Error($"[BBFon] Verbindung fehlgeschlagen: {ex.Message}");
            return;
        }

        using (doc)
        {
            if (!doc.RootElement.TryGetProperty("result", out var result) || result.GetArrayLength() == 0)
            {
                ConsoleLog.Warning("[BBFon] Keine Nachrichten gefunden.");
                ConsoleLog.Info("[BBFon] Senden Sie zuerst eine Nachricht an den Bot, dann erneut ausführen.");
                return;
            }

            // Alle einzigartigen Chat-IDs sammeln (in Reihenfolge des ersten Auftretens)
            var found = new List<(long Id, string Name)>();
            var seen  = new HashSet<long>();

            foreach (var update in result.EnumerateArray())
            {
                if (!update.TryGetProperty("message", out var message)) continue;
                if (!message.TryGetProperty("chat",   out var chat))    continue;
                if (!chat.TryGetProperty("id",        out var idProp))  continue;

                var id = idProp.GetInt64();
                if (!seen.Add(id)) continue;

                found.Add((id, GetChatName(chat)));
            }

            if (found.Count == 0)
            {
                ConsoleLog.Warning("[BBFon] Keine auswertbaren Nachrichten im Ergebnis.");
                ConsoleLog.Info("[BBFon] Senden Sie zuerst eine Nachricht an den Bot, dann erneut ausführen.");
                return;
            }

            foreach (var (id, name) in found)
                ConsoleLog.Success($"[BBFon] Chat-ID gefunden: {id}  ({name})");

            var (saveId, saveName) = found[0];
            if (found.Count > 1)
                ConsoleLog.Info($"[BBFon] Mehrere Chats gefunden – erste ID wird gespeichert: {saveId}");

            SaveToSettings(botToken, saveId.ToString());
            ConsoleLog.Success($"[BBFon] appsettings.json aktualisiert: BotToken + ChatId ({saveId}).");
        }
    }

    private static void SaveToSettings(string botToken, string chatId)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
        var json = JsonNode.Parse(File.ReadAllText(path))
            ?? throw new InvalidOperationException("appsettings.json konnte nicht geparst werden.");

        json["Telegram"]!["BotToken"] = botToken;
        json["Telegram"]!["ChatId"]   = chatId;

        File.WriteAllText(path, json.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
    }

    private static string GetChatName(JsonElement chat)
    {
        var type = chat.TryGetProperty("type", out var t) ? t.GetString() : "?";

        if (type == "private")
        {
            var first = chat.TryGetProperty("first_name", out var f) ? f.GetString() ?? "" : "";
            var last  = chat.TryGetProperty("last_name",  out var l) ? l.GetString() ?? "" : "";
            var user  = chat.TryGetProperty("username",   out var u) ? $"@{u.GetString()}" : "";
            return $"{first} {last} {user}".Trim();
        }

        var title = chat.TryGetProperty("title", out var ti) ? ti.GetString() : "";
        return $"{type}: {title}".Trim();
    }
}
