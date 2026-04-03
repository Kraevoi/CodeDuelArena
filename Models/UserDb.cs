using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace CodeDuelArena.Models
{
    public class UserDb
    {
        [Key]
        public int Id { get; set; }
        
        [Required, MaxLength(50)]
        public string Username { get; set; } = string.Empty;
        
        [Required, MaxLength(255)]
        public string PasswordHash { get; set; } = string.Empty;
        
        [MaxLength(100)]
        public string Email { get; set; } = string.Empty;
        
        public int Score { get; set; } = 0;
        public int Wins { get; set; } = 0;
        public int Losses { get; set; } = 0;
        
        public string CompletedQuestsJson { get; set; } = "[]";
        
        public DateTime RegisteredAt { get; set; } = DateTime.Now;
        public DateTime LastLogin { get; set; } = DateTime.Now;
        
        public List<string> CompletedQuests
        {
            get => string.IsNullOrEmpty(CompletedQuestsJson) ? new List<string>() : JsonSerializer.Deserialize<List<string>>(CompletedQuestsJson) ?? new List<string>();
            set => CompletedQuestsJson = JsonSerializer.Serialize(value);
        }
    }
}