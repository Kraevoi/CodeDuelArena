using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using CodeDuelArena.Data;
using Microsoft.EntityFrameworkCore;

namespace CodeDuelArena.Services
{
    public class TelegramBotService : BackgroundService
    {
        private readonly IServiceProvider _services;
        private readonly ITelegramBotClient _bot;
        
        public TelegramBotService(IServiceProvider services)
        {
            _services = services;
            _bot = new TelegramBotClient("8579896503:AAEZkYkgCLxvW7UP_sFPAESQ6ZeUQomlBB8");
        }
        
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await Task.Delay(10000);
            _bot.StartReceiving(UpdateHandler, ErrorHandler, cancellationToken: stoppingToken);
            await Task.Delay(-1, stoppingToken);
        }
        
        private async Task UpdateHandler(ITelegramBotClient bot, Update update, CancellationToken ct)
        {
            if (update.Message?.Text is not { } text) return;
            
            using var scope = _services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            
            var chatId = update.Message.Chat.Id;
            var messageText = text.Trim();
            
            if (messageText == "/start")
            {
                await bot.SendTextMessageAsync(chatId, "👋 Добро пожаловать в CodeDuel Bot!\n\n/top - Топ игроков\n/stats - Статистика\n/profile @username - Профиль\n/complaints - Жалобы\n/daily - Ежедневные задания");
            }
            else if (messageText == "/top")
            {
                var topUsers = await db.Users.OrderByDescending(u => u.Score).Take(10).ToListAsync();
                var msg = "🏆 ТОП ИГРОКОВ:\n\n";
                for (int i = 0; i < topUsers.Count; i++)
                {
                    msg += $"{i+1}. {topUsers[i].Username} — {topUsers[i].Score}⭐\n";
                }
                await bot.SendTextMessageAsync(chatId, msg);
            }
            else if (messageText == "/stats")
            {
                var totalUsers = await db.Users.CountAsync();
                var totalDuels = await db.ActivityLogs.CountAsync(l => l.Action == "Победил в дуэли");
                await bot.SendTextMessageAsync(chatId, $"📊 СТАТИСТИКА:\n\n👥 Пользователей: {totalUsers}\n⚔️ Дуэлей: {totalDuels}");
            }
            else if (messageText == "/complaints")
            {
                var newComplaints = await db.Complaints.CountAsync(c => !c.IsRead);
                await bot.SendTextMessageAsync(chatId, $"📢 НОВЫХ ЖАЛОБ: {newComplaints}");
            }
            else if (messageText == "/daily")
            {
                var today = DateTime.UtcNow.Date;
                var dailyQuests = await db.DailyQuests.Where(q => q.Date == today).ToListAsync();
                var dailyMsg = "📅 ЕЖЕДНЕВНЫЕ ЗАДАНИЯ:\n\n";
                foreach (var q in dailyQuests)
                {
                    dailyMsg += $"• {q.Title}: {q.Description} +{q.RewardPoints}⭐\n";
                }
                await bot.SendTextMessageAsync(chatId, dailyMsg);
            }
            else if (messageText.StartsWith("/profile"))
            {
                var parts = messageText.Split(' ');
                if (parts.Length > 1)
                {
                    var username = parts[1].TrimStart('@');
                    var user = await db.Users.FirstOrDefaultAsync(u => u.Username == username);
                    if (user != null)
                    {
                        var league = await db.UserLeagues.FirstOrDefaultAsync(l => l.Username == username);
                        await bot.SendTextMessageAsync(chatId, $"👤 {user.Username}\n⭐ {user.Score}\n🏆 {user.Wins} побед\n🏅 Лига: {league?.League ?? "Bronze"}");
                    }
                    else
                    {
                        await bot.SendTextMessageAsync(chatId, "❌ Пользователь не найден");
                    }
                }
            }
        }
        
        private Task ErrorHandler(ITelegramBotClient bot, Exception exception, CancellationToken ct)
        {
            Console.WriteLine($"Telegram bot error: {exception.Message}");
            return Task.CompletedTask;
        }
    }
}