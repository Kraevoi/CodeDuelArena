using System;

namespace CodeDuelArena.Models
{
    public class UserDailyProgress
    {
        public int Id { get; set; }
        public string Username { get; set; } = string.Empty;
        public int DailyQuestId { get; set; }
        public int CurrentCount { get; set; }
        public bool Completed { get; set; }
        public DateTime Date { get; set; }
    }
}