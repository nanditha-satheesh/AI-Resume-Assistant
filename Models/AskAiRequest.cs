namespace AIResumeAssistant.Models;

public class AskAiRequest
{
    public string Question { get; set; } = string.Empty;
    public string PromptMode { get; set; } = "Default";
}
