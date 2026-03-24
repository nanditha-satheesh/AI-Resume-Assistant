using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using AIResumeAssistant.Models;

namespace AIResumeAssistant.Services;

public class GroqService : IOpenAIService
{
    private readonly HttpClient _httpClient;
    private readonly string _model;
    private readonly ILogger<GroqService> _logger;

    public GroqService(HttpClient httpClient, IConfiguration configuration, ILogger<GroqService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;

        _model = configuration["Groq:Model"] ?? "llama-3.3-70b-versatile";

        var apiKey = configuration["Groq:ApiKey"]
            ?? throw new InvalidOperationException("Groq:ApiKey is not configured.");

        _httpClient.BaseAddress = new Uri("https://api.groq.com/");
        _httpClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);
    }

    public async Task<OpenAIResult> GetChatCompletionAsync(string systemPrompt, string userMessage)
    {
        var requestBody = new
        {
            model = _model,
            messages = new[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = userMessage }
            },
            max_tokens = 2048,
            temperature = 0.7
        };

        var json = JsonSerializer.Serialize(requestBody);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        try
        {
            var response = await _httpClient.PostAsync("openai/v1/chat/completions", content);
            var responseJson = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Groq API returned {StatusCode}: {Body}", response.StatusCode, responseJson);

                var errorMessage = $"Groq API error ({(int)response.StatusCode})";
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
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString();

            return string.IsNullOrWhiteSpace(message)
                ? OpenAIResult.Fail("Groq returned an empty response.")
                : OpenAIResult.Ok(message.Trim());
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error calling Groq API");
            return OpenAIResult.Fail($"Network error: {ex.Message}");
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogError(ex, "Groq API request timed out");
            return OpenAIResult.Fail("Request timed out. Please try again.");
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Error parsing Groq API response");
            return OpenAIResult.Fail("Failed to parse AI response.");
        }
    }

    public async IAsyncEnumerable<string> StreamChatCompletionAsync(
        string systemPrompt, string userMessage,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var requestBody = new
        {
            model = _model,
            messages = new[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = userMessage }
            },
            max_tokens = 2048,
            temperature = 0.7,
            stream = true
        };

        var json = JsonSerializer.Serialize(requestBody);
        var request = new HttpRequestMessage(HttpMethod.Post, "openai/v1/chat/completions")
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
            _logger.LogError(ex, "Error initiating Groq streaming request");
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
            if (data == "[DONE]")
                break;

            string? text = null;
            try
            {
                using var doc = JsonDocument.Parse(data);
                var delta = doc.RootElement
                    .GetProperty("choices")[0]
                    .GetProperty("delta");

                if (delta.TryGetProperty("content", out var contentProp))
                {
                    text = contentProp.GetString();
                }
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Failed to parse streaming chunk");
            }

            if (!string.IsNullOrEmpty(text))
                yield return text;
        }
    }
}
