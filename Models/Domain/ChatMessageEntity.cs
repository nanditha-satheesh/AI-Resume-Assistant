namespace AIResumeAssistant.Models.Domain;

public class ChatMessageEntity
{
    public int Id { get; set; }
    public int SessionId { get; set; }
    public string Role { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    public ChatSession Session { get; set; } = null!;
}
