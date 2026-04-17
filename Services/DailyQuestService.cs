using CodeDuelArena.Data;
using CodeDuelArena.Models;
using Microsoft.EntityFrameworkCore;

namespace CodeDuelArena.Services
{
    public class DailyQuestService
    {
        private readonly AppDbContext _db;
        
        public DailyQuestService(AppDbContext db)
        {
            _db = db;
        }
        
        public async Task InitializeDailyQuests()
        {
            var today = DateTime.UtcNow.Date;
            var exists = await _db.DailyQuests.AnyAsync(q => q.Date == today);
            if (!exists)
            {
                var quests = new List<DailyQuest>
                {
                    new DailyQuest { Title = "Победитель", Description = "Выиграй 2 дуэли", RewardPoints = 100, Condition = "win_duel", RequiredCount = 2, Date = today },
                    new DailyQuest { Title = "Квестер", Description = "Пройди 3 квеста", RewardPoints = 150, Condition = "complete_quest", RequiredCount = 3, Date = today },
                    new DailyQuest { Title = "Говорун", Description = "Напиши 5 сообщений в чат", RewardPoints = 50, Condition = "send_message", RequiredCount = 5, Date = today }
                };
                await _db.DailyQuests.AddRangeAsync(quests);
                await _db.SaveChangesAsync();
            }
        }
        
        public async Task TrackProgress(string username, string action)
        {
            var today = DateTime.UtcNow.Date;
            var quests = await _db.DailyQuests.Where(q => q.Date == today && q.Condition == action).ToListAsync();
            
            foreach (var quest in quests)
            {
                var progress = await _db.UserDailyProgress
                    .FirstOrDefaultAsync(p => p.Username == username && p.DailyQuestId == quest.Id && p.Date == today);
                
                if (progress == null)
                {
                    progress = new UserDailyProgress
                    {
                        Username = username,
                        DailyQuestId = quest.Id,
                        CurrentCount = 1,
                        Completed = false,
                        Date = today
                    };
                    _db.UserDailyProgress.Add(progress);
                }
                else if (!progress.Completed)
                {
                    progress.CurrentCount++;
                    if (progress.CurrentCount >= quest.RequiredCount)
                    {
                        progress.Completed = true;
                        var user = await _db.Users.FirstOrDefaultAsync(u => u.Username == username);
                        if (user != null)
                        {
                            user.Score += quest.RewardPoints;
                        }
                    }
                }
                await _db.SaveChangesAsync();
            }
        }
    }
}