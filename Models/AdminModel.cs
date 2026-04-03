namespace CodeDuelArena.Models
{
    public class AdminLoginModel
    {
        public string Password { get; set; } = string.Empty;
        public bool RememberMe { get; set; } = false;
    }
}