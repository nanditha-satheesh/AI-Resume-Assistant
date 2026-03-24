namespace AIResumeAssistant.Models.Domain;

public class ChatSession
{
    public int Id { get; set; }
    public int ResumeId { get; set; }
    public string PromptMode { get; set; } = "Default";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Resume Resume { get; set; } = null!;
    public ICollection<ChatMessageEntity> Messages { get; set; } = [];
}
