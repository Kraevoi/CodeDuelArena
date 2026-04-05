using System;

namespace CodeDuelArena.Models
{
    public class DuelTask
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string TestCode { get; set; } = string.Empty;
        public string ExpectedOutput { get; set; } = string.Empty;
        public int Difficulty { get; set; } = 1; // 1-5
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}