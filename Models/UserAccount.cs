using System;
using System.Collections.Generic;

namespace CodeDuelArena.Models
{
    public class UserAccount
    {
        public string Username { get; set; } = "";
        public string PasswordHash { get; set; } = "";
        public string Email { get; set; } = "";
        public int Score { get; set; } = 0;
        public int Wins { get; set; } = 0;
        public int Losses { get; set; } = 0;
        public List<string> CompletedQuests { get; set; } = new List<string>();
        public DateTime RegisteredAt { get; set; } = DateTime.Now;
        public DateTime LastLogin { get; set; } = DateTime.Now;
    }
}