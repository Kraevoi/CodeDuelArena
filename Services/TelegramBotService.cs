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
        private readonly Dictionary<long, string> _userStates = new();

        public TelegramBotService(IConfiguration config, IServiceProvider serviceProvider, ILogger<TelegramBotService> logger)
        {
            _botToken = config["TelegramBotToken"] ?? "";
            _supportChatId = config["Telegram:SupportChatId"] ?? "";
            _serviceProvider = serviceProvider;
            _logger = logger;
            _http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("=== TELEGRAM BOT STARTED ===");
            await Task.Delay(5000, stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var url = $"https://api.telegram.org/bot{_botToken}/getUpdates?offset={_lastUpdateId + 1}&timeout=10";
                    var json = await _http.GetStringAsync(url, stoppingToken);
                    var root = JObject.Parse(json);

                    if (root["ok"]?.Value<bool>() == true)
                    {
                        var results = root["result"] as JArray;
                        if (results != null && results.Count > 0)
                        {
                            foreach (var update in results)
                            {
                                var updateId = update["update_id"]?.Value<long>() ?? 0;
                                if (updateId > _lastUpdateId) _lastUpdateId = updateId;
                                
                                _ = Task.Run(() => ProcessUpdate(update), stoppingToken);
                            }
                        }
                    }
                }
                catch (TaskCanceledException) { }
                catch (Exception ex)
                {
                    _logger.LogError($"Poll error: {ex.Message}");
                }
                await Task.Delay(2000, stoppingToken);
            }
        }

        private async Task ProcessUpdate(JToken update)
        {
            try
            {
                // Callback query
                if (update["callback_query"] != null)
                {
                    var cb = update["callback_query"]!;
                    var data = cb["data"]?.Value<string>() ?? "";
                    var cbChatId = cb["message"]?["chat"]?["id"]?.Value<long>() ?? 0;
                    var cbId = cb["id"]?.Value<string>() ?? "";
                    var cbUsername = cb["from"]?["first_name"]?.Value<string>() ?? "User";

                    await AnswerCallback(cbId);
                    await HandleCallback(cbChatId, data, cbUsername);
                    return;
                }

                // Message
                var msg = update["message"];
                if (msg == null) return;

                var chatId = msg["chat"]?["id"]?.Value<long>() ?? 0;
                var text = msg["text"]?.Value<string>() ?? "";
                var firstName = msg["from"]?["first_name"]?.Value<string>() ?? "User";
                var username = msg["from"]?["username"]?.Value<string>() ?? firstName;

                _logger.LogInformation($"[BOT] {firstName} (chat={chatId}): {text}");

                // Reply from support group
                if (chatId.ToString() == _supportChatId || chatId.ToString() == _supportChatId.Replace("-", ""))
                {
                    var replyTo = msg["reply_to_message"];
                    if (replyTo != null)
                    {
                        var replyText = replyTo["text"]?.Value<string>() ?? "";
                        var match = System.Text.RegularExpressions.Regex.Match(replyText, @"#ID(\d+)");
                        if (match.Success)
                        {
                            var targetId = match.Groups[1].Value;
                            var reply = msg["text"]?.Value<string>() ?? "";
                            var adminName = msg["from"]?["first_name"]?.Value<string>() ?? "Support";
                            await SendMessage(targetId, $"🛡️ *Support reply:*\n\n{reply}\n\n_— {adminName}_");
                        }
                    }
                    return;
                }

                // User states
                if (_userStates.ContainsKey(chatId))
                {
                    var state = _userStates[chatId];
                    _userStates.Remove(chatId);

                    if (state == "waiting_support")
                    {
                        await ForwardToSupport(chatId, username, text);
                        return;
                    }
                    if (state == "waiting_link")
                    {
                        await LinkAccount(chatId, text);
                        return;
                    }
                }

                // Commands
                if (text.StartsWith("/"))
                {
                    var cmd = text.Split(' ')[0].Split('@')[0].ToLower();
                    switch (cmd)
                    {
                        case "/start": await SendWelcome(chatId, firstName); break;
                        case "/menu": await ShowMenu(chatId, firstName); break;
                        case "/stats": await ShowStats(chatId, username); break;
                        case "/support":
                            _userStates[chatId] = "waiting_support";
                            await SendMessage(chatId, "🆘 Describe your problem in one message:");
                            break;
                        case "/link":
                            _userStates[chatId] = "waiting_link";
                            await SendMessage(chatId, "Enter your CodeDuel Arena username:");
                            break;
                        case "/tournaments":
                            await SendMessage(chatId, "🏆 *Tournaments*\n\nWeekly Cup — every Saturday 18:00 UTC.\nBe online and join duel queue!");
                            break;
                        case "/stop":
                            _userStates.Remove(chatId);
                            await SendMessage(chatId, "Session ended. /menu for main menu.");
                            break;
                        default:
                            await ShowMenu(chatId, firstName);
                            break;
                    }
                }
                else
                {
                    await ShowMenu(chatId, firstName);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"ProcessUpdate error: {ex.Message}");
            }
        }

        private async Task HandleCallback(long chatId, string data, string firstName)
        {
            switch (data)
            {
                case "menu": await ShowMenu(chatId, firstName); break;
                case "stats": await ShowStats(chatId, firstName); break;
                case "support":
                    _userStates[chatId] = "waiting_support";
                    await SendMessage(chatId, "Describe your problem:");
                    break;
                case "link":
                    _userStates[chatId] = "waiting_link";
                    await SendMessage(chatId, "Enter your CodeDuel Arena username:");
                    break;
                case "tournaments":
                    await SendMessage(chatId, "🏆 *Tournaments*\n\nWeekly Cup — every Saturday 18:00 UTC.");
                    break;
            }
        }

        private async Task SendWelcome(long chatId, string firstName)
        {
            var text = $"⚔️ *CodeDuel Arena Bot*\n\nWelcome, {Escape(firstName)}!\n\n" +
                       "• /stats — Your stats\n" +
                       "• /support — Contact support\n" +
                       "• /link — Link your game account\n" +
                       "• /tournaments — Events info";

            var kb = new { inline_keyboard = new[] {
                new[] { Btn("📊 Stats", "stats"), Btn("🆘 Support", "support") },
                new[] { Btn("🔗 Link Account", "link"), Btn("🏆 Tournaments", "tournaments") }
            }};

            await SendWithKeyboard(chatId, text, kb);
        }

        private async Task ShowMenu(long chatId, string firstName)
        {
            var text = $"⚔️ *Main Menu*\n\nChoose an option, {Escape(firstName)}:";

            var kb = new { inline_keyboard = new[] {
                new[] { Btn("📊 Stats", "stats"), Btn("🆘 Support", "support") },
                new[] { Btn("🔗 Link Account", "link"), Btn("🏆 Tournaments", "tournaments") }
            }};

            await SendWithKeyboard(chatId, text, kb);
        }

        private async Task ShowStats(long chatId, string username)
        {
            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var settings = await db.UserSettings.FirstOrDefaultAsync(s => s.TelegramChatId == chatId.ToString());
            var user = settings != null
                ? await db.Users.FirstOrDefaultAsync(u => u.Username == settings.Username)
                : await db.Users.FirstOrDefaultAsync(u => u.Username == username);

            if (user == null)
            {
                await SendMessage(chatId, "⚠️ Account not found. Link your account: /link");
                return;
            }

            var league = await db.UserLeagues.FirstOrDefaultAsync(l => l.Username == user.Username);
            var wr = user.Wins + user.Losses > 0 ? Math.Round((double)user.Wins / (user.Wins + user.Losses) * 100, 1) : 0;

            var text = $"📊 *{Escape(user.Username)}*\n\n" +
                       $"⭐ Score: *{user.Score:N0}*\n" +
                       $"🏆 Wins: *{user.Wins}*  |  💀 Losses: *{user.Losses}*\n" +
                       $"📈 Win Rate: *{wr}%*\n" +
                       $"🏅 League: *{league?.League ?? "Bronze"}*";

            await SendMessage(chatId, text);
        }

        private async Task ForwardToSupport(long chatId, string username, string text)
        {
            if (string.IsNullOrEmpty(_supportChatId))
            {
                await SendMessage(chatId, "⚠️ Support unavailable.");
                return;
            }

            var supportMsg = $"🆘 *Support Request* #ID{chatId}\n\n" +
                            $"👤 {Escape(username)} (ID: `{chatId}`)\n" +
                            $"📝 {Escape(text)}\n\n" +
                            "_Reply to this message to answer_";

            await SendMessage(_supportChatId, supportMsg);
            await SendMessage(chatId, "✅ Sent to support. Reply coming soon.");
        }

        private async Task LinkAccount(long chatId, string gameUsername)
        {
            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var user = await db.Users.FirstOrDefaultAsync(u => u.Username == gameUsername);
            if (user == null)
            {
                await SendMessage(chatId, "❌ User not found. Register at codeduelarena.onrender.com first.");
                return;
            }

            var settings = await db.UserSettings.FirstOrDefaultAsync(s => s.Username == gameUsername);
            if (settings == null)
            {
                settings = new CodeDuelArena.Models.UserSettings { Username = gameUsername };
                db.UserSettings.Add(settings);
            }
            settings.TelegramChatId = chatId.ToString();
            await db.SaveChangesAsync();

            await SendMessage(chatId, $"✅ Account *{Escape(gameUsername)}* linked! Use /stats to check.");
        }

        private async Task SendMessage(object chatId, string text)
        {
            var payload = new { chat_id = chatId.ToString(), text, parse_mode = "Markdown" };
            var content = new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json");
            var response = await _http.PostAsync($"https://api.telegram.org/bot{_botToken}/sendMessage", content);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError($"SendMessage failed: {await response.Content.ReadAsStringAsync()}");
            }
        }

        private async Task SendWithKeyboard(object chatId, string text, object kb)
        {
            var payload = new { chat_id = chatId.ToString(), text, parse_mode = "Markdown", reply_markup = kb };
            var content = new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json");
            await _http.PostAsync($"https://api.telegram.org/bot{_botToken}/sendMessage", content);
        }

        private async Task AnswerCallback(string cbId)
        {
            await _http.GetAsync($"https://api.telegram.org/bot{_botToken}/answerCallbackQuery?callback_query_id={cbId}");
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