namespace CodeDuelArena.Models
{
    public class QuestModel
    {
        public int Id { get; set; }
        public string Title { get; set; } = "";
        public string Type { get; set; } = "";
        public string Description { get; set; } = "";
        public string LegacyCode { get; set; } = "";
        public string SolutionCode { get; set; } = "";
        public int Points { get; set; } = 100;
    }
}