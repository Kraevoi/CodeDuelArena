namespace CodeDuelArena.Models
{
    public class QuestModel
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty; // "LegacyFix" или "Hack"
        public string Description { get; set; } = string.Empty;
        public string LegacyCode { get; set; } = string.Empty;
        public string ExpectedOutput { get; set; } = string.Empty;
        public string SolutionCode { get; set; } = string.Empty;
        public int Points { get; set; } = 100;
    }
}