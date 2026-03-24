namespace AIResumeAssistant.Models.Dto;

public class AtsScoreResult
{
    public bool Success { get; set; } = true;
    public string? Error { get; set; }
    public int OverallScore { get; set; }
    public int FormatScore { get; set; }
    public int KeywordScore { get; set; }
    public int ImpactScore { get; set; }
    public int? MatchPercentage { get; set; }
    public List<string> MissingSkills { get; set; } = [];
    public List<string> Suggestions { get; set; } = [];
    public List<string> StrongPoints { get; set; } = [];
}
