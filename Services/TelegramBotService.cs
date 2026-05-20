using System.Net.Http;
using System.Text;
using Newtonsoft.Json;

namespace CodeDuelArena.Services
{
    public class TelegramBotService
    {
        private readonly string _botToken;
        private readonly string _supportChatId = "@CodeDuelArena_bot"; // ID чата поддержки
        private readonly HttpClient _http;

        public TelegramBotService(IConfiguration config)
        {
            _botToken = config["TelegramBotToken"] ?? "";
            _http = new HttpClient { BaseAddress = new Uri($"https://api.telegram.org/bot{_botToken}/") };
        }

        public async Task SendSupportNotification(string username, string issue)
        {
            var message = $"🚨 *New Support Request*\n\n" +
                         $"👤 *User:* {EscapeMarkdown(username)}\n" +
                         $"📝 *Issue:* {EscapeMarkdown(issue)}\n" +
                         $"🕐 *Time:* {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC";
            await SendMessage(_supportChatId, message);
        }

        public async Task SendDuelNotification(string winner, string loser, int points)
        {
            var message = $"⚔️ *Duel Result*\n\n" +
                         $"🏆 *Winner:* {EscapeMarkdown(winner)} (+{points} pts)\n" +
                         $"💀 *Loser:* {EscapeMarkdown(loser)}\n" +
                         $"🕐 *Time:* {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC";
            await SendMessage(_supportChatId, message);
        }

        public async Task SendRegistrationNotification(string username, int totalUsers)
        {
            var message = $"🆕 *New Player Registered*\n\n" +
                         $"👤 *Username:* {EscapeMarkdown(username)}\n" +
                         $"👥 *Total Players:* {totalUsers}\n" +
                         $"🕐 *Time:* {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC";
            await SendMessage(_supportChatId, message);
        }

        public async Task SendBugReport(string username, string bugText)
        {
            var message = $"🐛 *Bug Report*\n\n" +
                         $"👤 *From:* {EscapeMarkdown(username)}\n" +
                         $"📝 *Report:* {EscapeMarkdown(bugText)}\n" +
                         $"🕐 *Time:* {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC";
            await SendMessage(_supportChatId, message);
        }

        public async Task SendAdminAlert(string alert)
        {
            var message = $"⚠️ *Admin Alert*\n\n{alert}";
            await SendMessage(_supportChatId, message);
        }

        private async Task SendMessage(string chatId, string text)
        {
            var payload = new { chat_id = chatId, text = text, parse_mode = "Markdown" };
            var json = JsonConvert.SerializeObject(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            await _http.PostAsync("sendMessage", content);
        }

        private static string EscapeMarkdown(string text)
        {
            char[] specialChars = { '_', '*', '[', ']', '(', ')', '~', '`', '>', '#', '+', '-', '=', '|', '{', '}', '.', '!' };
            foreach (var c in specialChars)
                text = text.Replace(c.ToString(), "\\" + c);
            return text;
        }
    }
}