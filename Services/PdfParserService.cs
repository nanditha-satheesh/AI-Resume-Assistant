using System.Text;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas.Parser;

namespace AIResumeAssistant.Services;

public class PdfParserService : IPdfParserService
{
    public Task<string> ExtractTextAsync(Stream pdfStream)
    {
        return Task.Run(() =>
        {
            using var pdfReader = new PdfReader(pdfStream);
            using var pdfDocument = new PdfDocument(pdfReader);

            var text = new StringBuilder();
            for (int i = 1; i <= pdfDocument.GetNumberOfPages(); i++)
            {
                var page = pdfDocument.GetPage(i);
                text.AppendLine(PdfTextExtractor.GetTextFromPage(page));
            }

            return text.ToString().Trim();
        });
    }
}
