using System.Collections.Concurrent;
using AIResumeAssistant.Models;

namespace AIResumeAssistant.Services;

public class ResumeSessionService : IResumeSessionService, IDisposable
{
    private readonly ConcurrentDictionary<string, string> _resumeTexts = new();
    private readonly ConcurrentDictionary<string, List<ChatMessage>> _chatHistories = new();
    private readonly ConcurrentDictionary<string, DateTime> _lastAccessed = new();
    private readonly Timer _cleanupTimer;
    private static readonly TimeSpan SessionLifetime = TimeSpan.FromHours(24);
    private static readonly TimeSpan CleanupInterval = TimeSpan.FromMinutes(30);

    public ResumeSessionService()
    {
        _cleanupTimer = new Timer(_ => RemoveExpiredSessions(), null, CleanupInterval, CleanupInterval);
    }

    public void StoreResumeText(string sessionId, string text)
    {
        _resumeTexts[sessionId] = text;
        _lastAccessed[sessionId] = DateTime.UtcNow;
    }

    public string? GetResumeText(string sessionId)
    {
        if (_resumeTexts.TryGetValue(sessionId, out var text))
        {
            _lastAccessed[sessionId] = DateTime.UtcNow;
            return text;
        }
        return null;
    }

    public void AddChatMessage(string sessionId, ChatMessage message)
    {
        var history = _chatHistories.GetOrAdd(sessionId, _ => new List<ChatMessage>());
        lock (history)
        {
            history.Add(message);
        }
        _lastAccessed[sessionId] = DateTime.UtcNow;
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
        _lastAccessed.TryRemove(sessionId, out _);
    }

    private void RemoveExpiredSessions()
    {
        var cutoff = DateTime.UtcNow - SessionLifetime;
        foreach (var entry in _lastAccessed)
        {
            if (entry.Value < cutoff)
            {
                ClearSession(entry.Key);
            }
        }
    }

    public void Dispose()
    {
        _cleanupTimer.Dispose();
    }
}
