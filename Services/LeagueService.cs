using CodeDuelArena.Data;
using CodeDuelArena.Models;
using Microsoft.EntityFrameworkCore;

namespace CodeDuelArena.Services
{
    public class LeagueService
    {
        private readonly AppDbContext _db;
        
        public LeagueService(AppDbContext db)
        {
            _db = db;
        }
        
        public async Task UpdateLeague(string username)
        {
            var user = await _db.Users.FirstOrDefaultAsync(u => u.Username == username);
            if (user == null) return;
            
            var league = await _db.UserLeagues.FirstOrDefaultAsync(l => l.Username == username);
            if (league == null)
            {
                league = new UserLeague { Username = username };
                _db.UserLeagues.Add(league);
            }
            
            string newLeague = user.Score switch
            {
                < 200 => "Bronze",
                < 500 => "Silver",
                < 1000 => "Gold",
                < 2000 => "Platinum",
                _ => "Diamond"
            };
            
            if (league.League != newLeague)
            {
                league.League = newLeague;
                league.UpdatedAt = DateTime.UtcNow;
                await _db.SaveChangesAsync();
            }
        }
        
        public string GetLeagueIcon(string league)
        {
            return league switch
            {
                "Bronze" => "🪙",
                "Silver" => "⚪",
                "Gold" => "🥇",
                "Platinum" => "💎",
                "Diamond" => "👑",
                _ => "🪙"
            };
        }
    }
}