using System.Runtime.CompilerServices;
using System.Security.Claims;
using AIResumeAssistant.Data;
using AIResumeAssistant.Models.Domain;
using AIResumeAssistant.PromptBuilder;
using AIResumeAssistant.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace AIResumeAssistant.Hubs;

[Authorize]
public class ChatHub : Hub
{
    private readonly IOpenAIService _aiService;
    private readonly AppDbContext _db;
    private readonly ILogger<ChatHub> _logger;

    public ChatHub(IOpenAIService aiService, AppDbContext db, ILogger<ChatHub> logger)
    {
        _aiService = aiService;
        _db = db;
        _logger = logger;
    }

    private string UserId => Context.User!.FindFirstValue(ClaimTypes.NameIdentifier)!;

    /// <summary>
    /// Streams AI response chunks to the client in real-time (ChatGPT typing effect).
    /// </summary>
    public async IAsyncEnumerable<string> StreamResponse(
        string question, string promptMode,
        [EnumeratorCancellation] CancellationToken ct)
    {
        // Get active resume
        var resume = await _db.Resumes
            .Where(r => r.UserId == UserId && r.IsActive)
            .OrderByDescending(r => r.UploadedAt)
            .FirstOrDefaultAsync(ct);

        if (resume is null)
        {
            yield return "[ERROR]Please upload your resume first.";
            yield break;
        }

        // Get or create chat session
        var session = await _db.ChatSessions
            .Where(s => s.ResumeId == resume.Id)
            .OrderByDescending(s => s.CreatedAt)
            .FirstOrDefaultAsync(ct);

        if (session is null)
        {
            session = new ChatSession { ResumeId = resume.Id, PromptMode = promptMode };
            _db.ChatSessions.Add(session);
            await _db.SaveChangesAsync(ct);
        }

        // Save user message
        _db.ChatMessages.Add(new ChatMessageEntity
        {
            SessionId = session.Id,
            Role = "user",
            Content = question
        });
        await _db.SaveChangesAsync(ct);

        // Build prompts
        var allowedModes = ResumePromptBuilder.GetAvailableModes().Keys;
        var validMode = allowedModes.Contains(promptMode) ? promptMode : "Default";
        var systemPrompt = ResumePromptBuilder.BuildSystemPrompt(validMode);
        var userPrompt = ResumePromptBuilder.BuildUserPrompt(resume.ExtractedText, question);

        // Stream AI response
        var fullResponse = new System.Text.StringBuilder();

        await foreach (var chunk in _aiService.StreamChatCompletionAsync(systemPrompt, userPrompt, ct))
        {
            fullResponse.Append(chunk);
            yield return chunk;
        }

        // Save complete AI response to DB
        var responseText = fullResponse.ToString().Trim();
        if (!string.IsNullOrEmpty(responseText))
        {
            _db.ChatMessages.Add(new ChatMessageEntity
            {
                SessionId = session.Id,
                Role = "assistant",
                Content = responseText
            });
            await _db.SaveChangesAsync(CancellationToken.None);
        }

        _logger.LogInformation("Streamed AI response for user {UserId}, {Length} chars", UserId, responseText.Length);
    }
}
