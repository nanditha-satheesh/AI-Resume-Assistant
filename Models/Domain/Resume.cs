namespace AIResumeAssistant.Models.Domain;

public class Resume
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string? FilePath { get; set; }
    public string ExtractedText { get; set; } = string.Empty;
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    public bool IsActive { get; set; } = true;

    public AppUser User { get; set; } = null!;
    public ICollection<ResumeVersion> Versions { get; set; } = [];
    public ICollection<ChatSession> ChatSessions { get; set; } = [];
    public ICollection<AtsScore> AtsScores { get; set; } = [];
}
