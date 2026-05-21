using System.Net.Http;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using CodeDuelArena.Data;
using Microsoft.EntityFrameworkCore;

namespace CodeDuelArena.Services
{
    public class TelegramBotService : BackgroundService
    {
        private readonly string _botToken;
        private readonly string _supportChatId;
        private readonly HttpClient _http;
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<TelegramBotService> _logger;
        private long _lastUpdateId = 0;

        public TelegramBotService(IConfiguration config, IServiceProvider serviceProvider, ILogger<TelegramBotService> logger)
        {
            _botToken = config["TelegramBotToken"] ?? "";
            _supportChatId = config["Telegram:SupportChatId"] ?? "";
            _serviceProvider = serviceProvider;
            _logger = logger;
            _http = new HttpClient { BaseAddress = new Uri($"https://api.telegram.org/bot{_botToken}/") };
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Telegram bot started with Long Polling");

            // Ждём 5 секунд чтобы сервер точно запустился
            await Task.Delay(5000, stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var updates = await GetUpdates(stoppingToken);
                    foreach (var update in updates)
                    {
                        _ = Task.Run(() => HandleUpdate(update), stoppingToken);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Polling error: {ex.Message}");
                }

                await Task.Delay(2000, stoppingToken);
            }
        }

        private async Task<List<JObject>> GetUpdates(CancellationToken ct)
        {
            var url = $"getUpdates?offset={_lastUpdateId + 1}&timeout=10";
            var response = await _http.GetAsync(url, ct);
            var json = await response.Content.ReadAsStringAsync(ct);
            var result = JObject.Parse(json);

            var updates = new List<JObject>();
            if (result["ok"]?.Value<bool>() == true && result["result"] != null)
            {
                foreach (var update in result["result"])
                {
                    var updateId = update["update_id"]?.Value<long>() ?? 0;
                    if (updateId > _lastUpdateId)
                        _lastUpdateId = updateId;
                    updates.Add((JObject)update);
                }
            }
            return updates;
        }

        private async Task HandleUpdate(JObject update)
        {
            try
            {
                // Callback query (кнопки)
                if (update["callback_query"] != null)
                {
                    await HandleCallback(update["callback_query"]);
                    return;
                }

                var message = update["message"];
                if (message == null) return;

                var chatId = message["chat"]?["id"]?.Value<long>() ?? 0;
                var text = message["text"]?.Value<string>() ?? "";
                var username = message["from"]?["username"]?.Value<string>() ??
                               message["from"]?["first_name"]?.Value<string>() ?? "User";

                // Ответ админа из группы поддержки (reply на сообщение)
                var replyTo = message["reply_to_message"];
                if (replyTo != null && chatId.ToString() == _supportChatId.Replace("-100", "").Replace("-", ""))
                {
                    await HandleAdminReply(message, replyTo, chatId);
                    return;
                }

                // Сообщение из группы поддержки (не reply)
                if (chatId.ToString() == _supportChatId || chatId.ToString() == _supportChatId.Replace("-100", "").Replace("-", ""))
                {
                    return; // Игнорируем обычные сообщения из группы
                }

                // Команды
                if (text.StartsWith("/"))
                {
                    await HandleCommand(chatId, text, username);
                    return;
                }

                // Обычный текст — показываем меню
                await ShowMainMenu(chatId, username);
            }
            catch (Exception ex)
            {
                _logger.LogError($"HandleUpdate error: {ex.Message}");
            }
        }

        private async Task HandleCommand(long chatId, string text, string username)
        {
            var command = text.Split(' ')[0].ToLower().Split('@')[0]; // убираем @botname

            switch (command)
            {
                case "/start":
                    await SendWelcome(chatId, username);
                    break;
                case "/menu":
                    await ShowMainMenu(chatId, username);
                    break;
                case "/stats":
                    await ShowStats(chatId, username);
                    break;
                case "/support":
                    await StartSupport(chatId, username);
                    break;
                case "/link":
                    await SendMessage(chatId, "To link your account, send me your CodeDuel Arena username.\nExample: _MyUsername_");
                    break;
                default:
                    await ShowMainMenu(chatId, username);
                    break;
            }
        }

        private async Task HandleAdminReply(JToken message, JToken replyTo, long chatId)
        {
            var replyText = replyTo["text"]?.Value<string>() ?? "";
            // Ищем #ID123456 в тексте
            var match = System.Text.RegularExpressions.Regex.Match(replyText, @"#ID(\d+)");
            if (!match.Success) return;

            var targetUserId = match.Groups[1].Value;
            var adminText = message["text"]?.Value<string>() ?? "";
            var adminName = message["from"]?["first_name"]?.Value<string>() ?? "Support";

            await SendMessage(targetUserId, $"🛡️ *Support reply:*\n\n{adminText}\n\n_— {adminName}_");
        }

        // ==================== МЕНЮ ====================

        private async Task SendWelcome(long chatId, string username)
        {
            var text = $"⚔️ *CodeDuel Arena Bot*\n\n" +
                       $"Welcome, {Escape(username)}!\n\n" +
                       $"🔹 /menu — Main menu\n" +
                       $"🔹 /stats — Your statistics\n" +
                       $"🔹 /support — Contact support\n" +
                       $"🔹 /link — Link your game account";

            var keyboard = new
            {
                inline_keyboard = new[]
                {
                    new[] { Btn("📊 Statistics", "stats"), Btn("🆘 Support", "support") },
                    new[] { Btn("🔗 Link Account", "link"), Btn("🏠 Menu", "menu") }
                }
            };

            await SendWithKeyboard(chatId, text, keyboard);
        }

        private async Task ShowMainMenu(long chatId, string username)
        {
            var text = $"⚔️ *Main Menu*\n\nChoose an action:";

            var keyboard = new
            {
                inline_keyboard = new[]
                {
                    new[] { Btn("📊 Statistics", "stats"), Btn("🆘 Support", "support") },
                    new[] { Btn("🔗 Link Account", "link") }
                }
            };

            await SendWithKeyboard(chatId, text, keyboard);
        }

        private async Task ShowStats(long chatId, string username)
        {
            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            // Ищем по TelegramChatId или username
            var settings = await db.UserSettings.FirstOrDefaultAsync(s => s.TelegramChatId == chatId.ToString());
            var user = settings != null
                ? await db.Users.FirstOrDefaultAsync(u => u.Username == settings.Username)
                : await db.Users.FirstOrDefaultAsync(u => u.Username == username);

            if (user == null)
            {
                await SendMessage(chatId, "⚠️ *Account not found.*\nLink your account using /link command.");
                return;
            }

            var league = await db.UserLeagues.FirstOrDefaultAsync(l => l.Username == user.Username);
            var winRate = user.Wins + user.Losses > 0
                ? Math.Round((double)user.Wins / (user.Wins + user.Losses) * 100, 1)
                : 0;

            var text = $"📊 *{Escape(user.Username)}*\n\n" +
                       $"⭐ Score: *{user.Score:N0}*\n" +
                       $"🏆 Wins: *{user.Wins}*\n" +
                       $"💀 Losses: *{user.Losses}*\n" +
                       $"📈 Win Rate: *{winRate}%*\n" +
                       $"🏅 League: *{league?.League ?? "Bronze"}*\n" +
                       $"📅 Registered: {user.RegisteredAt:dd.MM.yyyy}";

            await SendMessage(chatId, text);
        }

        private async Task StartSupport(long chatId, string username)
        {
            if (string.IsNullOrEmpty(_supportChatId))
            {
                await SendMessage(chatId, "⚠️ Support is temporarily unavailable.");
                return;
            }

            await SendMessage(chatId, "🆘 *Support*\n\nDescribe your problem in one message and I will forward it to the support team.");

            // Ждём следующее сообщение от этого пользователя
            _ = Task.Run(async () =>
            {
                await Task.Delay(500);
                // Простой способ — ждём сообщение через getUpdates
                // Но проще попросить пользователя написать /support с текстом
            });
        }

        private async Task HandleCallback(JToken callback)
        {
            var data = callback["data"]?.Value<string>() ?? "";
            var chatId = callback["message"]?["chat"]?["id"]?.Value<long>() ?? 0;
            var username = callback["from"]?["username"]?.Value<string>() ??
                           callback["from"]?["first_name"]?.Value<string>() ?? "User";
            var callbackId = callback["id"]?.Value<string>() ?? "";

            // Отвечаем на callback
            await _http.GetAsync($"answerCallbackQuery?callback_query_id={callbackId}");

            switch (data)
            {
                case "stats": await ShowStats(chatId, username); break;
                case "support": await StartSupport(chatId, username); break;
                case "link":
                    await SendMessage(chatId, "Send me your CodeDuel Arena username to link accounts.\nExample: _MyUsername_");
                    break;
                case "menu": await ShowMainMenu(chatId, username); break;
            }
        }

        // ==================== ВСПОМОГАТЕЛЬНЫЕ ====================

        private async Task SendMessage(object chatId, string text)
        {
            try
            {
                var payload = new { chat_id = chatId.ToString(), text, parse_mode = "Markdown" };
                var json = JsonConvert.SerializeObject(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                await _http.PostAsync("sendMessage", content);
            }
            catch (Exception ex)
            {
                _logger.LogError($"SendMessage error: {ex.Message}");
            }
        }

        private async Task SendWithKeyboard(object chatId, string text, object keyboard)
        {
            try
            {
                var payload = new { chat_id = chatId.ToString(), text, parse_mode = "Markdown", reply_markup = keyboard };
                var json = JsonConvert.SerializeObject(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                await _http.PostAsync("sendMessage", content);
            }
            catch (Exception ex)
            {
                _logger.LogError($"SendWithKeyboard error: {ex.Message}");
            }
        }

        private object Btn(string text, string data) => new { text, callback_data = data };

        private static string Escape(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            foreach (var c in "_*[]()~`>#+-=|{}.!")
                text = text.Replace(c.ToString(), "\\" + c);
            return text;
        }
    }
}