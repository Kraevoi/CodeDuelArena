using System.Net.Http;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using CodeDuelArena.Data;
using Microsoft.EntityFrameworkCore;

namespace CodeDuelArena.Services
{
    public class TelegramBotService
    {
        private readonly string _botToken;
        private readonly string _adminChatId;
        private readonly string _webhookUrl;
        private readonly HttpClient _http;
        private readonly IServiceProvider _serviceProvider;

        // Состояния пользователей в боте
        private static readonly Dictionary<long, string> _userStates = new();
        private static readonly Dictionary<long, string> _supportSessions = new(); // user -> admin чат

        public TelegramBotService(IConfiguration config, IServiceProvider serviceProvider)
        {
            _botToken = config["TelegramBotToken"] ?? "";
            _adminChatId = config["Telegram:SupportChatId"] ?? "";
            _webhookUrl = config["Telegram:WebhookUrl"] ?? "";
            _serviceProvider = serviceProvider;
            _http = new HttpClient { BaseAddress = new Uri($"https://api.telegram.org/bot{_botToken}/") };
        }

        // Установка WebHook при старте
        public async Task SetWebhook()
        {
            var url = $"{_webhookUrl}/api/telegram/update";
            var payload = new { url };
            var json = JsonConvert.SerializeObject(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            await _http.PostAsync("setWebhook", content);
        }

        // Обработка входящих сообщений
        public async Task<string> HandleUpdate(JObject update)
        {
            try
            {
                var message = update["message"];
                if (message == null) return "No message";

                var chatId = message["chat"]?["id"]?.Value<long>() ?? 0;
                var text = message["text"]?.Value<string>() ?? "";
                var username = message["from"]?["username"]?.Value<string>() ?? 
                               message["from"]?["first_name"]?.Value<string>() ?? "User";
                var userId = message["from"]?["id"]?.Value<long>() ?? 0;

                // Проверка: сообщение из админского чата (ответ поддержки)
                if (_adminChatId != "" && chatId.ToString() == _adminChatId || chatId.ToString() == _adminChatId.Replace("-100", "").Replace("-", ""))
                {
                    await HandleAdminReply(message, chatId);
                    return "Admin reply processed";
                }

                // Проверка состояний
                if (_userStates.ContainsKey(chatId))
                {
                    await HandleState(chatId, text, username);
                    return "State handled";
                }

                // Обработка команд
                if (text.StartsWith("/"))
                {
                    await HandleCommand(chatId, text, username, userId);
                    return "Command handled";
                }

                // Обычное сообщение — главное меню
                await ShowMainMenu(chatId, username);
                return "Menu shown";
            }
            catch (Exception ex)
            {
                return $"Error: {ex.Message}";
            }
        }

        private async Task HandleCommand(long chatId, string text, string username, long userId)
        {
            var command = text.Split(' ')[0].ToLower();

            switch (command)
            {
                case "/start":
                    await SendWelcomeMessage(chatId, username);
                    break;

                case "/menu":
                case "/help":
                    await ShowMainMenu(chatId, username);
                    break;

                case "/stats":
                    await ShowStats(chatId, username);
                    break;

                case "/support":
                    await StartSupport(chatId, username);
                    break;

                case "/tournaments":
                    await ShowTournaments(chatId);
                    break;

                case "/tech":
                    await ShowTechInfo(chatId);
                    break;

                case "/link":
                    await LinkAccount(chatId, username);
                    break;

                case "/stop":
                    _userStates.Remove(chatId);
                    _supportSessions.Remove(chatId);
                    await SendMessage(chatId, "✅ Session ended. Use /menu to return to main menu.");
                    break;

                default:
                    await ShowMainMenu(chatId, username);
                    break;
            }
        }

        private async Task HandleState(long chatId, string text, string username)
        {
            var state = _userStates.GetValueOrDefault(chatId, "");

            switch (state)
            {
                case "waiting_support_message":
                    _userStates.Remove(chatId);
                    await ForwardToSupport(chatId, username, text);
                    break;

                case "waiting_link_username":
                    _userStates.Remove(chatId);
                    await CompleteLinkAccount(chatId, text, username);
                    break;

                default:
                    _userStates.Remove(chatId);
                    await ShowMainMenu(chatId, username);
                    break;
            }
        }

        private async Task HandleAdminReply(JToken message, long chatId)
        {
            var replyTo = message["reply_to_message"];
            if (replyTo == null) return;

            var replyText = replyTo["text"]?.Value<string>() ?? "";
            // Ищем ID пользователя в ответе (формат: #ID123456)
            var match = System.Text.RegularExpressions.Regex.Match(replyText, @"#ID(\d+)");
            if (!match.Success) return;

            var userId = long.Parse(match.Groups[1].Value);
            var adminText = message["text"]?.Value<string>() ?? "";

            var supportMessage = $"🛡️ *Support Response*\n\n{adminText}\n\n_Reply to this message to continue the conversation, or /stop to end._";
            await SendMessage(userId, supportMessage);
            
            // Устанавливаем сессию поддержки
            _supportSessions[userId] = chatId.ToString();
        }

        // ============ МЕТОДЫ МЕНЮ ============

        private async Task SendWelcomeMessage(long chatId, string username)
        {
            var text = $"⚔️ *Welcome to CodeDuel Arena, {EscapeMarkdown(username)}!*\n\n" +
                       "I am your personal arena assistant. Here you can:\n" +
                       "📊 Check your statistics\n" +
                       "🆘 Contact support\n" +
                       "🏆 Get tournament announcements\n" +
                       "🔧 Receive technical maintenance alerts\n\n" +
                       "Use /menu to see all available options.";

            var keyboard = new
            {
                inline_keyboard = new[]
                {
                    new[] { CreateButton("📊 My Stats", "menu_stats"), CreateButton("🆘 Support", "menu_support") },
                    new[] { CreateButton("🏆 Tournaments", "menu_tournaments"), CreateButton("🔧 Tech Info", "menu_tech") },
                    new[] { CreateButton("🔗 Link Account", "menu_link") }
                }
            };

            await SendMessageWithKeyboard(chatId, text, keyboard);
        }

        private async Task ShowMainMenu(long chatId, string username)
        {
            var text = $"⚔️ *CodeDuel Arena — Main Menu*\n\nWelcome, {EscapeMarkdown(username)}! Choose an option:";

            var keyboard = new
            {
                inline_keyboard = new[]
                {
                    new[] { CreateButton("📊 My Stats", "menu_stats"), CreateButton("🆘 Support", "menu_support") },
                    new[] { CreateButton("🏆 Tournaments", "menu_tournaments"), CreateButton("🔧 Tech Info", "menu_tech") },
                    new[] { CreateButton("🔗 Link Account", "menu_link"), CreateButton("❓ Help", "menu_help") }
                }
            };

            await SendMessageWithKeyboard(chatId, text, keyboard);
        }

        private async Task ShowStats(long chatId, string username)
        {
            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            
            var user = await db.Users.FirstOrDefaultAsync(u => u.Username == username);
            
            if (user == null)
            {
                // Пробуем найти по Telegram ID
                var settings = await db.UserSettings.FirstOrDefaultAsync(s => s.TelegramChatId == chatId.ToString());
                if (settings != null)
                {
                    user = await db.Users.FirstOrDefaultAsync(u => u.Username == settings.Username);
                }
            }

            if (user == null)
            {
                await SendMessage(chatId, "⚠️ *Account not found*\n\nLink your account using /link command to see your statistics.");
                return;
            }

            var league = await db.UserLeagues.FirstOrDefaultAsync(l => l.Username == user.Username);
            var winRate = user.Wins + user.Losses > 0 
                ? Math.Round((double)user.Wins / (user.Wins + user.Losses) * 100, 1) 
                : 0;

            var text = $"📊 *Statistics for {EscapeMarkdown(user.Username)}*\n\n" +
                       $"⭐ *Score:* {user.Score:N0}\n" +
                       $"🏆 *Wins:* {user.Wins}\n" +
                       $"💀 *Losses:* {user.Losses}\n" +
                       $"📈 *Win Rate:* {winRate}%\n" +
                       $"🏅 *League:* {league?.League ?? "Bronze"}\n" +
                       $"📅 *Registered:* {user.RegisteredAt:dd.MM.yyyy}\n" +
                       $"🕐 *Last Login:* {user.LastLogin:dd.MM.yyyy HH:mm}";

            var keyboard = new
            {
                inline_keyboard = new[]
                {
                    new[] { CreateButton("🔄 Refresh", "menu_stats"), CreateButton("🏠 Menu", "menu_main") }
                }
            };

            await SendMessageWithKeyboard(chatId, text, keyboard);
        }

        private async Task StartSupport(long chatId, string username)
        {
            _userStates[chatId] = "waiting_support_message";
            
            var text = $"🆘 *Support Request*\n\n" +
                       "Please describe your issue in one message.\n" +
                       "Our team will respond as soon as possible.\n\n" +
                       "_Send /stop to cancel._";

            await SendMessage(chatId, text);
        }

        private async Task ForwardToSupport(long chatId, string username, string message)
        {
            if (string.IsNullOrEmpty(_adminChatId))
            {
                await SendMessage(chatId, "⚠️ Support is currently unavailable. Please try again later.");
                return;
            }

            var supportText = $"🆘 *New Support Request* #ID{chatId}\n\n" +
                             $"👤 *From:* {EscapeMarkdown(username)}\n" +
                             $"🆔 *User ID:* `{chatId}`\n" +
                             $"🕐 *Time:* {DateTime.UtcNow:dd.MM.yyyy HH:mm} UTC\n\n" +
                             $"📝 *Message:*\n{EscapeMarkdown(message)}\n\n" +
                             "_Reply to this message to answer the user._";

            await SendMessage(_adminChatId, supportText);
            await SendMessage(chatId, "✅ *Your message has been sent to support!*\n\nWe will reply as soon as possible. You will receive a notification here.\n\n_Use /stop to end the session._");
        }

        private async Task ShowTournaments(long chatId)
        {
            var text = $"🏆 *Tournaments & Events*\n\n" +
                       $"🔥 *Upcoming:*\n" +
                       $"• Weekly Duel Cup — Every Saturday 18:00 UTC\n" +
                       $"• Monthly Championship — First Sunday of each month\n\n" +
                       $"📋 *How to participate:*\n" +
                       $"Just be online during tournament hours and join the duel queue!\n\n" +
                       $"💎 *Prizes:*\n" +
                       $"• 1st Place: 1000 points + Diamond Badge\n" +
                       $"• 2nd Place: 500 points + Platinum Badge\n" +
                       $"• 3rd Place: 250 points + Gold Badge\n\n" +
                       $"_Subscribe to notifications to never miss an event!_";

            var keyboard = new
            {
                inline_keyboard = new[]
                {
                    new[] { CreateButton("🔔 Subscribe to Alerts", "sub_tournaments"), CreateButton("🏠 Menu", "menu_main") }
                }
            };

            await SendMessageWithKeyboard(chatId, text, keyboard);
        }

        private async Task ShowTechInfo(long chatId)
        {
            var text = $"🔧 *Technical Status*\n\n" +
                       $"🟢 *Server Status:* Online\n" +
                       $"📡 *API:* Operational\n" +
                       $"🗄️ *Database:* Operational\n\n" +
                       $"📅 *Planned Maintenance:*\n" +
                       $"No maintenance scheduled.\n\n" +
                       $"_You will receive notifications about any technical works._";

            var keyboard = new
            {
                inline_keyboard = new[]
                {
                    new[] { CreateButton("🔔 Tech Alerts", "sub_tech"), CreateButton("🏠 Menu", "menu_main") }
                }
            };

            await SendMessageWithKeyboard(chatId, text, keyboard);
        }

        private async Task LinkAccount(long chatId, string username)
        {
            _userStates[chatId] = "waiting_link_username";
            
            var text = $"🔗 *Link Your Account*\n\n" +
                       "Enter your CodeDuel Arena username to link it with this Telegram account.\n\n" +
                       "_Send /stop to cancel._";

            await SendMessage(chatId, text);
        }

        private async Task CompleteLinkAccount(long chatId, string gameUsername, string tgUsername)
        {
            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var user = await db.Users.FirstOrDefaultAsync(u => u.Username == gameUsername);
            if (user == null)
            {
                await SendMessage(chatId, $"❌ *User not found*\n\nNo account with username \"{EscapeMarkdown(gameUsername)}\" exists. Please register on the website first: https://codeduelarena.onrender.com");
                return;
            }

            var settings = await db.UserSettings.FirstOrDefaultAsync(s => s.Username == gameUsername);
            if (settings == null)
            {
                settings = new Models.UserSettings { Username = gameUsername };
                db.UserSettings.Add(settings);
            }
            settings.TelegramChatId = chatId.ToString();
            await db.SaveChangesAsync();

            await SendMessage(chatId, $"✅ *Account Linked!*\n\nYour Telegram is now connected to \"{EscapeMarkdown(gameUsername)}\".\n\nYou will receive notifications and can use /stats to check your progress.");
        }

        // ============ РАССЫЛКИ ============

        public async Task SendTournamentAnnouncement(string title, string description, DateTime date)
        {
            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            
            var subscribers = await db.UserSettings
                .Where(s => s.NotifyTournaments && !string.IsNullOrEmpty(s.TelegramChatId))
                .Select(s => s.TelegramChatId)
                .ToListAsync();

            var text = $"🏆 *TOURNAMENT ANNOUNCEMENT*\n\n" +
                       $"*{EscapeMarkdown(title)}*\n" +
                       $"{EscapeMarkdown(description)}\n\n" +
                       $"📅 *Date:* {date:dd.MM.yyyy HH:mm} UTC\n\n" +
                       $"_Be ready to duel!_";

            foreach (var chatId in subscribers)
            {
                await SendMessage(chatId, text);
            }
        }

        public async Task SendTechMaintenanceAlert(string message, DateTime startTime, DateTime endTime)
        {
            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            
            var subscribers = await db.UserSettings
                .Where(s => s.NotifyTechUpdates && !string.IsNullOrEmpty(s.TelegramChatId))
                .Select(s => s.TelegramChatId)
                .ToListAsync();

            var text = $"🔧 *TECHNICAL MAINTENANCE*\n\n" +
                       $"{EscapeMarkdown(message)}\n\n" +
                       $"📅 *Start:* {startTime:dd.MM.yyyy HH:mm} UTC\n" +
                       $"📅 *End:* {endTime:dd.MM.yyyy HH:mm} UTC\n\n" +
                       $"_The site may be unavailable during this period._";

            foreach (var chatId in subscribers)
            {
                await SendMessage(chatId, text);
            }
        }

        public async Task SendPersonalNotification(string username, string message)
        {
            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            
            var settings = await db.UserSettings.FirstOrDefaultAsync(s => s.Username == username);
            if (settings == null || string.IsNullOrEmpty(settings.TelegramChatId)) return;

            await SendMessage(settings.TelegramChatId, EscapeMarkdown(message));
        }

        // ============ ВСПОМОГАТЕЛЬНЫЕ МЕТОДЫ ============

        private async Task SendMessage(object chatId, string text)
        {
            var payload = new { chat_id = chatId.ToString(), text, parse_mode = "Markdown" };
            var json = JsonConvert.SerializeObject(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            await _http.PostAsync("sendMessage", content);
        }

        private async Task SendMessageWithKeyboard(object chatId, string text, object replyMarkup)
        {
            var payload = new { chat_id = chatId.ToString(), text, parse_mode = "Markdown", reply_markup = replyMarkup };
            var json = JsonConvert.SerializeObject(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            await _http.PostAsync("sendMessage", content);
        }

        private object CreateButton(string text, string callbackData)
        {
            return new { text, callback_data = callbackData };
        }

        private static string EscapeMarkdown(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            char[] specialChars = { '_', '*', '[', ']', '(', ')', '~', '`', '>', '#', '+', '-', '=', '|', '{', '}', '.', '!' };
            foreach (var c in specialChars)
                text = text.Replace(c.ToString(), "\\" + c);
            return text;
        }

        // Обработка Callback Query (кнопки)
        public async Task<string> HandleCallback(JObject callback)
        {
            var callbackData = callback["callback_query"]?["data"]?.Value<string>() ?? "";
            var chatId = callback["callback_query"]?["message"]?["chat"]?["id"]?.Value<long>() ?? 0;
            var username = callback["callback_query"]?["from"]?["username"]?.Value<string>() ?? "User";
            var callbackId = callback["callback_query"]?["id"]?.Value<string>() ?? "";

            // Подтверждаем callback
            await AnswerCallback(callbackId);

            switch (callbackData)
            {
                case "menu_stats": await ShowStats(chatId, username); break;
                case "menu_support": await StartSupport(chatId, username); break;
                case "menu_tournaments": await ShowTournaments(chatId); break;
                case "menu_tech": await ShowTechInfo(chatId); break;
                case "menu_link": await LinkAccount(chatId, username); break;
                case "menu_main": await ShowMainMenu(chatId, username); break;
                case "menu_help": await ShowMainMenu(chatId, username); break;
                case "sub_tournaments": await SubscribeTo(chatId, "tournaments"); break;
                case "sub_tech": await SubscribeTo(chatId, "tech"); break;
            }

            return "Callback processed";
        }

        private async Task AnswerCallback(string callbackId)
        {
            var payload = new { callback_query_id = callbackId };
            var json = JsonConvert.SerializeObject(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            await _http.PostAsync("answerCallbackQuery", content);
        }

        private async Task SubscribeTo(long chatId, string type)
        {
            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var settings = await db.UserSettings.FirstOrDefaultAsync(s => s.TelegramChatId == chatId.ToString());
            if (settings == null)
            {
                await SendMessage(chatId, "⚠️ Please link your account first using /link command.");
                return;
            }

            if (type == "tournaments")
            {
                settings.NotifyTournaments = true;
                await SendMessage(chatId, "🔔 *Subscribed to tournament alerts!*\n\nYou will receive notifications about upcoming tournaments and events.");
            }
            else if (type == "tech")
            {
                settings.NotifyTechUpdates = true;
                await SendMessage(chatId, "🔔 *Subscribed to technical alerts!*\n\nYou will receive notifications about maintenance and technical updates.");
            }

            await db.SaveChangesAsync();
        }
    }
}