using System.Collections.Concurrent;
using AIResumeAssistant.Models;

namespace AIResumeAssistant.Services;

public class ResumeSessionService : IResumeSessionService
{
    private readonly ConcurrentDictionary<string, string> _resumeTexts = new();
    private readonly ConcurrentDictionary<string, List<ChatMessage>> _chatHistories = new();

    public void StoreResumeText(string sessionId, string text)
    {
        _resumeTexts[sessionId] = text;
    }

    public string? GetResumeText(string sessionId)
    {
        _resumeTexts.TryGetValue(sessionId, out var text);
        return text;
    }

    public void AddChatMessage(string sessionId, ChatMessage message)
    {
        var history = _chatHistories.GetOrAdd(sessionId, _ => new List<ChatMessage>());
        lock (history)
        {
            history.Add(message);
        }
    }

    public List<ChatMessage> GetChatHistory(string sessionId)
    {
        if (_chatHistories.TryGetValue(sessionId, out var history))
        {
            lock (history)
            {
                return new List<ChatMessage>(history);
            }
        }
        return new List<ChatMessage>();
    }

    public void ClearSession(string sessionId)
    {
        _resumeTexts.TryRemove(sessionId, out _);
        _chatHistories.TryRemove(sessionId, out _);
    }
}
