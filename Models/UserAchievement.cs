using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace CodeDuelArena.Models
{
    public class UserAchievement
    {
        public int Id { get; set; }
        public string Username { get; set; } = string.Empty;
        public int AchievementId { get; set; }
        public DateTime UnlockedAt { get; set; }
        
        [ForeignKey("AchievementId")]
        public virtual Achievement? Achievement { get; set; }
    }
}