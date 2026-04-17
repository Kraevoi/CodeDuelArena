namespace CodeDuelArena.Models
{
    public class Achievement
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Icon { get; set; } = string.Empty;
        public int RequiredValue { get; set; }
        public string Condition { get; set; } = string.Empty;
        public int RewardPoints { get; set; }
    }
}