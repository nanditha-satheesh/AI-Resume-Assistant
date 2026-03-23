using AIResumeAssistant.Models;

namespace AIResumeAssistant.Services;

public interface IResumeSessionService
{
    void StoreResumeText(string sessionId, string text);
    string? GetResumeText(string sessionId);
    void AddChatMessage(string sessionId, ChatMessage message);
    List<ChatMessage> GetChatHistory(string sessionId);
    void ClearSession(string sessionId);
}
