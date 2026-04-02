using System.Collections.Generic;

namespace CodeDuelArena.Models
{
    public class UserModel
    {
        public string ConnectionId { get; set; } = "";
        public string Username { get; set; } = "";
        public int Score { get; set; } = 0;
        public int Wins { get; set; } = 0;
        public int Losses { get; set; } = 0;
        public int CurrentDuelId { get; set; } = -1;
        public bool IsInQueue { get; set; } = false;
        public List<string> CompletedQuests { get; set; } = new List<string>();
    }
}