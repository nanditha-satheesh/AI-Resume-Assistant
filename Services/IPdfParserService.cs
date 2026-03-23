namespace AIResumeAssistant.Services;

public interface IPdfParserService
{
    Task<string> ExtractTextAsync(Stream pdfStream);
}
