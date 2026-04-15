using System;
using System.ComponentModel.DataAnnotations;

namespace CodeDuelArena.Admin.Models
{
    public class UserDb
    {
        [Key]
        public int Id { get; set; }
        
        [Required]
        public string Username { get; set; } = string.Empty;
        
        [Required]
        public string PasswordHash { get; set; } = string.Empty;
        
        public string Email { get; set; } = string.Empty;
        
        public int Score { get; set; } = 0;
        public int Wins { get; set; } = 0;
        public int Losses { get; set; } = 0;
        
        public DateTime RegisteredAt { get; set; } = DateTime.UtcNow;
        public DateTime LastLogin { get; set; } = DateTime.UtcNow;
    }
}