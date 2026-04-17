using System;

namespace CodeDuelArena.Models
{
    public class UserLeague
    {
        public int Id { get; set; }
        public string Username { get; set; } = string.Empty;
        public string League { get; set; } = "Bronze";
        public int LeaguePoints { get; set; } = 0;
        public DateTime UpdatedAt { get; set; }
    }
}