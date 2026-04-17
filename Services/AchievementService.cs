using CodeDuelArena.Data;
using CodeDuelArena.Models;
using Microsoft.EntityFrameworkCore;

namespace CodeDuelArena.Services
{
    public class AchievementService
    {
        private readonly AppDbContext _db;
        
        public AchievementService(AppDbContext db)
        {
            _db = db;
        }
        
        public async Task CheckAchievements(string username)
        {
            var user = await _db.Users.FirstOrDefaultAsync(u => u.Username == username);
            if (user == null) return;
            
            var achievements = await _db.Achievements.ToListAsync();
            var unlocked = await _db.UserAchievements.Where(a => a.Username == username).Select(a => a.AchievementId).ToListAsync();
            
            foreach (var ach in achievements)
            {
                if (unlocked.Contains(ach.Id)) continue;
                
                bool earned = ach.Condition switch
                {
                    "win_duel" => user.Wins >= ach.RequiredValue,
                    "score" => user.Score >= ach.RequiredValue,
                    _ => false
                };
                
                if (earned)
                {
                    _db.UserAchievements.Add(new UserAchievement
                    {
                        Username = username,
                        AchievementId = ach.Id,
                        UnlockedAt = DateTime.UtcNow
                    });
                    user.Score += ach.RewardPoints;
                    await _db.SaveChangesAsync();
                }
            }
        }
    }
}