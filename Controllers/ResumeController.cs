using AIResumeAssistant.Models;
using AIResumeAssistant.PromptBuilder;
using AIResumeAssistant.Services;
using Microsoft.AspNetCore.Mvc;

namespace AIResumeAssistant.Controllers;

public class ResumeController : Controller
{
    private readonly IPdfParserService _pdfParser;
    private readonly IOpenAIService _openAIService;
    private readonly IResumeSessionService _sessionService;
    private readonly ILogger<ResumeController> _logger;

    public ResumeController(
        IPdfParserService pdfParser,
        IOpenAIService openAIService,
        IResumeSessionService sessionService,
        ILogger<ResumeController> logger)
    {
        _pdfParser = pdfParser;
        _openAIService = openAIService;
        _sessionService = sessionService;
        _logger = logger;
    }

    /// <summary>
    /// Main page for the Resume Assistant.
    /// </summary>
    public IActionResult Index()
    {
        var sessionId = GetOrCreateSessionId();
        var chatHistory = _sessionService.GetChatHistory(sessionId);
        var hasResume = _sessionService.GetResumeText(sessionId) != null;

        ViewBag.ChatHistory = chatHistory;
        ViewBag.HasResume = hasResume;
        ViewBag.PromptModes = ResumePromptBuilder.GetAvailableModes();

        return View();
    }

    /// <summary>
    /// Upload a PDF resume, extract text, and store it in the session.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
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

            var sessionId = GetOrCreateSessionId();
            _sessionService.ClearSession(sessionId);
            _sessionService.StoreResumeText(sessionId, extractedText);

            _logger.LogInformation("Resume uploaded successfully. Extracted {Length} characters.", extractedText.Length);

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
    public async Task<IActionResult> AskAI([FromBody] AskAiRequest request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.Question))
        {
            return Json(new AiResponse { Success = false, Error = "Please enter a question." });
        }

        var sessionId = GetOrCreateSessionId();
        var resumeText = _sessionService.GetResumeText(sessionId);

        if (string.IsNullOrWhiteSpace(resumeText))
        {
            return Json(new AiResponse { Success = false, Error = "Please upload your resume first." });
        }

        try
        {
            // Store user message in chat history
            _sessionService.AddChatMessage(sessionId, new ChatMessage
            {
                Role = "user",
                Content = request.Question
            });

            // Validate prompt mode against allowed values
            var allowedModes = ResumePromptBuilder.GetAvailableModes().Keys;
            var promptMode = allowedModes.Contains(request.PromptMode ?? "Default") ? request.PromptMode! : "Default";

            // Build prompts
            var systemPrompt = ResumePromptBuilder.BuildSystemPrompt(promptMode);
            var userPrompt = ResumePromptBuilder.BuildUserPrompt(resumeText, request.Question);

            // Call OpenAI
            var aiResult = await _openAIService.GetChatCompletionAsync(systemPrompt, userPrompt);

            if (!aiResult.Success)
            {
                return Json(new AiResponse { Success = false, Error = aiResult.Error });
            }

            // Store AI response in chat history
            _sessionService.AddChatMessage(sessionId, new ChatMessage
            {
                Role = "assistant",
                Content = aiResult.Content!
            });

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
    public IActionResult GetChatHistory()
    {
        var sessionId = GetOrCreateSessionId();
        var history = _sessionService.GetChatHistory(sessionId);
        return Json(history);
    }

    /// <summary>
    /// Clears the current session (resume + chat history).
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult ClearSession()
    {
        var sessionId = GetOrCreateSessionId();
        _sessionService.ClearSession(sessionId);
        return Json(new AiResponse { Success = true, Message = "Session cleared." });
    }

    /// <summary>
    /// Downloads the last AI response as a text file (e.g., cover letter).
    /// </summary>
    [HttpGet]
    public IActionResult DownloadLastResponse()
    {
        var sessionId = GetOrCreateSessionId();
        var history = _sessionService.GetChatHistory(sessionId);
        var lastAssistant = history.FindLast(m => m.Role == "assistant");

        if (lastAssistant == null)
        {
            return NotFound("No AI response available to download.");
        }

        var bytes = System.Text.Encoding.UTF8.GetBytes(lastAssistant.Content);
        return File(bytes, "text/plain", "ai-response.txt");
    }

    private string GetOrCreateSessionId()
    {
        const string sessionCookieName = "ResumeSessionId";

        if (Request.Cookies.TryGetValue(sessionCookieName, out var sessionId) && !string.IsNullOrEmpty(sessionId))
        {
            return sessionId;
        }

        sessionId = Guid.NewGuid().ToString();
        Response.Cookies.Append(sessionCookieName, sessionId, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Expires = DateTimeOffset.UtcNow.AddHours(24)
        });

        return sessionId;
    }
}
