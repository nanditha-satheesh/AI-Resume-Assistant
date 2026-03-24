using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using AIResumeAssistant.Models;

namespace AIResumeAssistant.Services;

public class GeminiService : IOpenAIService
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly string _model;
    private readonly ILogger<GeminiService> _logger;

    public GeminiService(HttpClient httpClient, IConfiguration configuration, ILogger<GeminiService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;

        _apiKey = configuration["Gemini:ApiKey"]
            ?? throw new InvalidOperationException("Gemini:ApiKey is not configured in appsettings.json.");
        _model = configuration["Gemini:Model"] ?? "gemini-2.0-flash";

        _httpClient.BaseAddress = new Uri("https://generativelanguage.googleapis.com/");
    }

    public async Task<OpenAIResult> GetChatCompletionAsync(string systemPrompt, string userMessage)
    {
        var requestBody = new
        {
            systemInstruction = new
            {
                parts = new[] { new { text = systemPrompt } }
            },
            contents = new[]
            {
                new
                {
                    role = "user",
                    parts = new[] { new { text = userMessage } }
                }
            },
            generationConfig = new
            {
                temperature = 0.7,
                maxOutputTokens = 2048
            }
        };

        var json = JsonSerializer.Serialize(requestBody);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var url = $"v1beta/models/{_model}:generateContent?key={_apiKey}";

        try
        {
            var response = await _httpClient.PostAsync(url, content);
            var responseJson = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Gemini API returned {StatusCode}: {Body}", response.StatusCode, responseJson);

                var errorMessage = $"Gemini API error ({(int)response.StatusCode})";
                try
                {
                    using var errorDoc = JsonDocument.Parse(responseJson);
                    if (errorDoc.RootElement.TryGetProperty("error", out var errorObj)
                        && errorObj.TryGetProperty("message", out var msgProp))
                    {
                        errorMessage = msgProp.GetString() ?? errorMessage;
                    }
                }
                catch { /* use default error message */ }

                return OpenAIResult.Fail(errorMessage);
            }

            using var doc = JsonDocument.Parse(responseJson);

            var message = doc.RootElement
                .GetProperty("candidates")[0]
                .GetProperty("content")
                .GetProperty("parts")[0]
                .GetProperty("text")
                .GetString();

            return string.IsNullOrWhiteSpace(message)
                ? OpenAIResult.Fail("Gemini returned an empty response.")
                : OpenAIResult.Ok(message.Trim());
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error calling Gemini API");
            return OpenAIResult.Fail($"Network error: {ex.Message}");
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogError(ex, "Gemini API request timed out");
            return OpenAIResult.Fail("Request timed out. Please try again.");
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Error parsing Gemini API response");
            return OpenAIResult.Fail("Failed to parse AI response.");
        }
    }

    public async IAsyncEnumerable<string> StreamChatCompletionAsync(
        string systemPrompt, string userMessage,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        // Gemini uses streamGenerateContent for streaming
        var requestBody = new
        {
            systemInstruction = new
            {
                parts = new[] { new { text = systemPrompt } }
            },
            contents = new[]
            {
                new
                {
                    role = "user",
                    parts = new[] { new { text = userMessage } }
                }
            },
            generationConfig = new
            {
                temperature = 0.7,
                maxOutputTokens = 2048
            }
        };

        var json = JsonSerializer.Serialize(requestBody);
        var url = $"v1beta/models/{_model}:streamGenerateContent?alt=sse&key={_apiKey}";
        var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
            response.EnsureSuccessStatusCode();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initiating Gemini streaming request");
            yield break;
        }

        using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var reader = new StreamReader(stream);

        while (!reader.EndOfStream && !ct.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(ct);
            if (line is null || !line.StartsWith("data: "))
                continue;

            var data = line["data: ".Length..];

            string? text = null;
            try
            {
                using var doc = JsonDocument.Parse(data);
                var candidates = doc.RootElement.GetProperty("candidates");
                text = candidates[0]
                    .GetProperty("content")
                    .GetProperty("parts")[0]
                    .GetProperty("text")
                    .GetString();
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Failed to parse Gemini streaming chunk");
            }

            if (!string.IsNullOrEmpty(text))
                yield return text;
        }
    }
}
