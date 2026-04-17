using System;

namespace CodeDuelArena.Models
{
    public class PrivateMessage
    {
        public int Id { get; set; }
        public string FromUser { get; set; } = string.Empty;
        public string ToUser { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public DateTime SentAt { get; set; }
        public bool IsRead { get; set; }
    }
}