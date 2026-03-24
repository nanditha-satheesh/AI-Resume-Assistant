using AIResumeAssistant.Models;

namespace AIResumeAssistant.Services;

public interface IOpenAIService
{
    Task<OpenAIResult> GetChatCompletionAsync(string systemPrompt, string userMessage);
    IAsyncEnumerable<string> StreamChatCompletionAsync(string systemPrompt, string userMessage, CancellationToken ct = default);
}
