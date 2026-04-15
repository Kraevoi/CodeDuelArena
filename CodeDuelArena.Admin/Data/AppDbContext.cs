using Microsoft.EntityFrameworkCore;
using CodeDuelArena.Admin.Models;

namespace CodeDuelArena.Admin.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }
        
        public DbSet<UserDb> Users { get; set; }
        public DbSet<Complaint> Complaints { get; set; }
        public DbSet<ActivityLog> ActivityLogs { get; set; }
        public DbSet<DuelTask> DuelTasks { get; set; }
    }
}