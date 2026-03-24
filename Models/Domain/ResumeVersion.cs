namespace AIResumeAssistant.Models.Domain;

public class ResumeVersion
{
    public int Id { get; set; }
    public int ResumeId { get; set; }
    public int VersionNumber { get; set; }
    public string ExtractedText { get; set; } = string.Empty;
    public int? AtsScore { get; set; }
    public string? ChangeNotes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Resume Resume { get; set; } = null!;
}
