namespace CodeDuelArena.Models
{
    public class UserSettings
    {
        public int Id { get; set; }
        public string Username { get; set; } = string.Empty;
        public string Theme { get; set; } = "dark";
        public string AvatarUrl { get; set; } = string.Empty;
        public string CustomCss { get; set; } = string.Empty;
        public bool NotificationsEnabled { get; set; } = true;
    }
}