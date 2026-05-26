namespace CodeDuelArena.Models
{
    public class UserSettings
    {
        public int Id { get; set; }
        public string Username { get; set; } = string.Empty;
        public string Theme { get; set; } = "dark";
        public string AvatarUrl { get; set; } = string.Empty;
        public byte[]? AvatarData { get; set; } // Само фото в базе
        public string AvatarContentType { get; set; } = string.Empty; // image/png или image/jpeg
        public string CustomCss { get; set; } = string.Empty;
        public bool NotificationsEnabled { get; set; } = true;
        public string TelegramChatId { get; set; } = string.Empty;
        public bool NotifyTournaments { get; set; } = false;
        public bool NotifyTechUpdates { get; set; } = false;
    }
}