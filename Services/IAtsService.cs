using AIResumeAssistant.Models.Dto;

namespace AIResumeAssistant.Services;

public interface IAtsService
{
    Task<AtsScoreResult> ScoreResumeAsync(int resumeId, string resumeText, string? jobDescription = null);
}
