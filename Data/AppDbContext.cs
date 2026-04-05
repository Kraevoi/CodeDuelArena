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
        
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<UserDb>()
                .HasIndex(u => u.Username)
                .IsUnique();
        }
    }
}