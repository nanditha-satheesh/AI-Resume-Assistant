using System.Security.Claims;
using AIResumeAssistant.Data;
using AIResumeAssistant.Models;
using AIResumeAssistant.Models.Domain;
using AIResumeAssistant.Models.Dto;
using AIResumeAssistant.PromptBuilder;
using AIResumeAssistant.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace AIResumeAssistant.Controllers;

[Authorize]
public class ResumeController : Controller
{
    private readonly IPdfParserService _pdfParser;
    private readonly IOpenAIService _openAIService;
    private readonly IAtsService _atsService;
    private readonly IFileStorageService _fileStorage;
    private readonly AppDbContext _db;
    private readonly ILogger<ResumeController> _logger;

    public ResumeController(
        IPdfParserService pdfParser,
        IOpenAIService openAIService,
        IAtsService atsService,
        IFileStorageService fileStorage,
        AppDbContext db,
        ILogger<ResumeController> logger)
    {
        _pdfParser = pdfParser;
        _openAIService = openAIService;
        _atsService = atsService;
        _fileStorage = fileStorage;
        _db = db;
        _logger = logger;
    }

    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    /// <summary>
    /// Main page for the Resume Assistant.
    /// </summary>
    public async Task<IActionResult> Index()
    {
        var resume = await GetActiveResumeAsync();
        var chatHistory = new List<ChatMessage>();

        if (resume is not null)
        {
            var session = await _db.ChatSessions
                .Where(s => s.ResumeId == resume.Id)
                .OrderByDescending(s => s.CreatedAt)
                .FirstOrDefaultAsync();

            if (session is not null)
            {
                chatHistory = await _db.ChatMessages
                    .Where(m => m.SessionId == session.Id)
                    .OrderBy(m => m.Timestamp)
                    .Select(m => new ChatMessage
                    {
                        Role = m.Role,
                        Content = m.Content,
                        Timestamp = m.Timestamp
                    })
                    .ToListAsync();
            }
        }

        ViewBag.ChatHistory = chatHistory;
        ViewBag.HasResume = resume is not null;
        ViewBag.PromptModes = ResumePromptBuilder.GetAvailableModes();

        return View();
    }

    /// <summary>
    /// Upload a PDF resume, extract text, and store it in the database.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("upload")]
    public async Task<IActionResult> UploadResume(IFormFile file)
    {
        if (file == null || file.Length == 0)
        {
            return Json(new AiResponse { Success = false, Error = "Please select a PDF file to upload." });
        }

        if (!file.ContentType.Equals("application/pdf", StringComparison.OrdinalIgnoreCase)
            && !Path.GetExtension(file.FileName).Equals(".pdf", StringComparison.OrdinalIgnoreCase))
        {
            return Json(new AiResponse { Success = false, Error = "Only PDF files are supported." });
        }

        if (file.Length > 5 * 1024 * 1024) // 5 MB limit
        {
            return Json(new AiResponse { Success = false, Error = "File size must be less than 5 MB." });
        }

        try
        {
            using var stream = file.OpenReadStream();
            var extractedText = await _pdfParser.ExtractTextAsync(stream);

            if (string.IsNullOrWhiteSpace(extractedText))
            {
                return Json(new AiResponse { Success = false, Error = "Could not extract text from the PDF. The file may be image-based or empty." });
            }

            // Deactivate previous resumes for this user
            var previousResumes = await _db.Resumes
                .Where(r => r.UserId == UserId && r.IsActive)
                .ToListAsync();

            foreach (var prev in previousResumes)
            {
                prev.IsActive = false;
            }

            // Persist the original PDF file
            using var saveStream = file.OpenReadStream();
            var filePath = await _fileStorage.SaveFileAsync(saveStream, file.FileName);

            // Create new resume record
            var resume = new Resume
            {
                UserId = UserId,
                FileName = file.FileName,
                FilePath = filePath,
                ExtractedText = extractedText,
                IsActive = true
            };

            _db.Resumes.Add(resume);
            await _db.SaveChangesAsync();

            // Create initial version
            _db.ResumeVersions.Add(new ResumeVersion
            {
                ResumeId = resume.Id,
                VersionNumber = 1,
                ExtractedText = extractedText,
                ChangeNotes = "Initial upload"
            });

            // Create a chat session for this resume
            _db.ChatSessions.Add(new ChatSession
            {
                ResumeId = resume.Id,
                PromptMode = "Default"
            });

            await _db.SaveChangesAsync();

            _logger.LogInformation("Resume uploaded by user {UserId}. Extracted {Length} characters.", UserId, extractedText.Length);

            return Json(new AiResponse
            {
                Success = true,
                Message = $"Resume uploaded successfully! Extracted {extractedText.Length} characters. You can now ask questions about your resume."
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing uploaded resume");
            return Json(new AiResponse { Success = false, Error = "An error occurred while processing your resume. Please try again." });
        }
    }

    /// <summary>
    /// Send a user question along with resume context to OpenAI and return the response.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("ai")]
    public async Task<IActionResult> AskAI([FromBody] AskAiRequest request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.Question))
        {
            return Json(new AiResponse { Success = false, Error = "Please enter a question." });
        }

        var resume = await GetActiveResumeAsync();

        if (resume is null)
        {
            return Json(new AiResponse { Success = false, Error = "Please upload your resume first." });
        }

        try
        {
            // Get or create chat session
            var session = await _db.ChatSessions
                .Where(s => s.ResumeId == resume.Id)
                .OrderByDescending(s => s.CreatedAt)
                .FirstOrDefaultAsync();

            if (session is null)
            {
                session = new ChatSession { ResumeId = resume.Id, PromptMode = "Default" };
                _db.ChatSessions.Add(session);
                await _db.SaveChangesAsync();
            }

            // Store user message
            _db.ChatMessages.Add(new ChatMessageEntity
            {
                SessionId = session.Id,
                Role = "user",
                Content = request.Question
            });
            await _db.SaveChangesAsync();

            // Validate prompt mode against allowed values
            var allowedModes = ResumePromptBuilder.GetAvailableModes().Keys;
            var promptMode = allowedModes.Contains(request.PromptMode ?? "Default") ? request.PromptMode! : "Default";

            // Build prompts
            var systemPrompt = ResumePromptBuilder.BuildSystemPrompt(promptMode);
            var userPrompt = ResumePromptBuilder.BuildUserPrompt(resume.ExtractedText, request.Question);

            // Call OpenAI
            var aiResult = await _openAIService.GetChatCompletionAsync(systemPrompt, userPrompt);

            if (!aiResult.Success)
            {
                return Json(new AiResponse { Success = false, Error = aiResult.Error });
            }

            // Store AI response
            _db.ChatMessages.Add(new ChatMessageEntity
            {
                SessionId = session.Id,
                Role = "assistant",
                Content = aiResult.Content!
            });
            await _db.SaveChangesAsync();

            return Json(new AiResponse { Success = true, Message = aiResult.Content });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing AI request");
            return Json(new AiResponse { Success = false, Error = "An unexpected error occurred. Please try again." });
        }
    }

    /// <summary>
    /// Returns the chat history for the current session.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetChatHistory()
    {
        var resume = await GetActiveResumeAsync();
        if (resume is null)
            return Json(Array.Empty<ChatMessage>());

        var session = await _db.ChatSessions
            .Where(s => s.ResumeId == resume.Id)
            .OrderByDescending(s => s.CreatedAt)
            .FirstOrDefaultAsync();

        if (session is null)
            return Json(Array.Empty<ChatMessage>());

        var messages = await _db.ChatMessages
            .Where(m => m.SessionId == session.Id)
            .OrderBy(m => m.Timestamp)
            .Select(m => new ChatMessage
            {
                Role = m.Role,
                Content = m.Content,
                Timestamp = m.Timestamp
            })
            .ToListAsync();

        return Json(messages);
    }

    /// <summary>
    /// Clears the current session (deactivates resume and deletes chat history).
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ClearSession()
    {
        var resume = await GetActiveResumeAsync();
        if (resume is not null)
        {
            resume.IsActive = false;
            await _db.SaveChangesAsync();
        }

        return Json(new AiResponse { Success = true, Message = "Session cleared." });
    }

    /// <summary>
    /// Downloads the last AI response as a text file (e.g., cover letter).
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> DownloadLastResponse()
    {
        var resume = await GetActiveResumeAsync();
        if (resume is null)
            return NotFound("No AI response available to download.");

        var session = await _db.ChatSessions
            .Where(s => s.ResumeId == resume.Id)
            .OrderByDescending(s => s.CreatedAt)
            .FirstOrDefaultAsync();

        if (session is null)
            return NotFound("No AI response available to download.");

        var lastAssistant = await _db.ChatMessages
            .Where(m => m.SessionId == session.Id && m.Role == "assistant")
            .OrderByDescending(m => m.Timestamp)
            .FirstOrDefaultAsync();

        if (lastAssistant is null)
        {
            return NotFound("No AI response available to download.");
        }

        var bytes = System.Text.Encoding.UTF8.GetBytes(lastAssistant.Content);
        return File(bytes, "text/plain", "ai-response.txt");
    }

    /// <summary>
    /// Downloads the original uploaded PDF for the active resume.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> DownloadResume()
    {
        var resume = await GetActiveResumeAsync();
        if (resume is null || string.IsNullOrEmpty(resume.FilePath))
            return NotFound("No resume file available to download.");

        var stream = await _fileStorage.GetFileAsync(resume.FilePath);
        if (stream is null)
            return NotFound("Resume file not found on server.");

        return File(stream, "application/pdf", resume.FileName);
    }

    /// <summary>
    /// Scores the active resume using ATS analysis, optionally against a job description.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("ai")]
    public async Task<IActionResult> ScoreResume([FromBody] AtsRequest request)
    {
        var resume = await GetActiveResumeAsync();

        if (resume is null)
        {
            return Json(new AtsScoreResult { Success = false, Error = "Please upload your resume first." });
        }

        var result = await _atsService.ScoreResumeAsync(resume.Id, resume.ExtractedText, request?.JobDescription);
        return Json(result);
    }

    /// <summary>
    /// Gets the currently active resume for the authenticated user.
    /// </summary>
    private async Task<Resume?> GetActiveResumeAsync()
    {
        return await _db.Resumes
            .Where(r => r.UserId == UserId && r.IsActive)
            .OrderByDescending(r => r.UploadedAt)
            .FirstOrDefaultAsync();
    }
}
