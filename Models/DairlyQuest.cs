using System;

namespace CodeDuelArena.Models
{
    public class DailyQuest
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int RewardPoints { get; set; }
        public string Condition { get; set; } = string.Empty;
        public int RequiredCount { get; set; }
        public DateTime Date { get; set; }
    }
}