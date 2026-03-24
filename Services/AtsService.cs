using System.Text.Json;
using AIResumeAssistant.Data;
using AIResumeAssistant.Models.Domain;
using AIResumeAssistant.Models.Dto;
using AIResumeAssistant.PromptBuilder;

namespace AIResumeAssistant.Services;

public class AtsService : IAtsService
{
    private readonly IOpenAIService _aiService;
    private readonly AppDbContext _db;
    private readonly ILogger<AtsService> _logger;

    public AtsService(IOpenAIService aiService, AppDbContext db, ILogger<AtsService> logger)
    {
        _aiService = aiService;
        _db = db;
        _logger = logger;
    }

    public async Task<AtsScoreResult> ScoreResumeAsync(int resumeId, string resumeText, string? jobDescription = null)
    {
        var systemPrompt = "You are an ATS (Applicant Tracking System) scoring engine. " +
            "Analyze resumes and return ONLY valid JSON with no markdown formatting, no code blocks, no explanation.";
        var userPrompt = ResumePromptBuilder.BuildAtsScorePrompt(resumeText, jobDescription);

        var result = await _aiService.GetChatCompletionAsync(systemPrompt, userPrompt);

        if (!result.Success)
        {
            return new AtsScoreResult { Success = false, Error = result.Error };
        }

        try
        {
            // Clean potential markdown code block wrapper
            var content = result.Content!.Trim();
            if (content.StartsWith("```"))
            {
                var firstNewLine = content.IndexOf('\n');
                if (firstNewLine >= 0)
                    content = content[(firstNewLine + 1)..];
                if (content.EndsWith("```"))
                    content = content[..^3].Trim();
            }

            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var score = JsonSerializer.Deserialize<AtsScoreResult>(content, options)!;
            score.Success = true;

            // Persist to database
            _db.AtsScores.Add(new AtsScore
            {
                ResumeId = resumeId,
                OverallScore = score.OverallScore,
                FormatScore = score.FormatScore,
                KeywordScore = score.KeywordScore,
                ImpactScore = score.ImpactScore,
                JobDescriptionUsed = jobDescription,
                MissingSkills = JsonSerializer.Serialize(score.MissingSkills),
                ScoredAt = DateTime.UtcNow
            });
            await _db.SaveChangesAsync();

            _logger.LogInformation("ATS score for resume {ResumeId}: {Score}", resumeId, score.OverallScore);

            return score;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse ATS score response: {Content}", result.Content);
            return new AtsScoreResult { Success = false, Error = "Failed to parse ATS score. Please try again." };
        }
    }
}
