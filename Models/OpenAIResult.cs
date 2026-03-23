namespace AIResumeAssistant.Models;

public class OpenAIResult
{
    public bool Success { get; set; }
    public string? Content { get; set; }
    public string? Error { get; set; }

    public static OpenAIResult Ok(string content) => new() { Success = true, Content = content };
    public static OpenAIResult Fail(string error) => new() { Success = false, Error = error };
}
