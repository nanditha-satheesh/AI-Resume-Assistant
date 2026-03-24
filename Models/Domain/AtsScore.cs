namespace AIResumeAssistant.Models.Domain;

public class AtsScore
{
    public int Id { get; set; }
    public int ResumeId { get; set; }
    public int OverallScore { get; set; }
    public int FormatScore { get; set; }
    public int KeywordScore { get; set; }
    public int ImpactScore { get; set; }
    public string? JobDescriptionUsed { get; set; }
    public string? MissingSkills { get; set; }
    public DateTime ScoredAt { get; set; } = DateTime.UtcNow;

    public Resume Resume { get; set; } = null!;
}
