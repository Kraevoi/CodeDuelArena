using Microsoft.EntityFrameworkCore;
using CodeDuelArena.Models;

namespace CodeDuelArena.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }
        
        public DbSet<UserDb> Users { get; set; }
        public DbSet<Complaint> Complaints { get; set; }
        public DbSet<ActivityLog> ActivityLogs { get; set; }
        public DbSet<DailyQuest> DailyQuests { get; set; }
        public DbSet<UserDailyProgress> UserDailyProgress { get; set; }
        public DbSet<Achievement> Achievements { get; set; }
        public DbSet<UserAchievement> UserAchievements { get; set; }
        public DbSet<PrivateMessage> PrivateMessages { get; set; }
        public DbSet<UserSettings> UserSettings { get; set; }
        public DbSet<UserLeague> UserLeagues { get; set; }
        public DbSet<DuelTask> DuelTasks { get; set; }
        
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<UserDb>().HasIndex(u => u.Username).IsUnique();
            modelBuilder.Entity<PrivateMessage>().HasIndex(p => p.ToUser);
            modelBuilder.Entity<PrivateMessage>().HasIndex(p => p.FromUser);
            modelBuilder.Entity<UserSettings>().HasIndex(u => u.Username).IsUnique();
            modelBuilder.Entity<UserLeague>().HasIndex(u => u.Username).IsUnique();
            
            modelBuilder.Entity<Achievement>().HasData(
                new Achievement { Id = 1, Name = "Первая победа", Description = "Выиграть первую дуэль", Icon = "🏆", RequiredValue = 1, Condition = "win_duel", RewardPoints = 50 },
                new Achievement { Id = 2, Name = "Воин", Description = "Выиграть 10 дуэлей", Icon = "⚔️", RequiredValue = 10, Condition = "win_duel", RewardPoints = 200 },
                new Achievement { Id = 3, Name = "Мастер кода", Description = "Пройди 5 квестов", Icon = "🧙", RequiredValue = 5, Condition = "complete_quest", RewardPoints = 150 },
                new Achievement { Id = 4, Name = "Легенда", Description = "Набрать 1000 очков", Icon = "👑", RequiredValue = 1000, Condition = "score", RewardPoints = 500 }
            );
        }
    }
}