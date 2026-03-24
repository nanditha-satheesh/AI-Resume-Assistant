namespace AIResumeAssistant.Services;

public interface IFileStorageService
{
    /// <summary>
    /// Stores a file and returns the relative path it was saved to.
    /// </summary>
    Task<string> SaveFileAsync(Stream stream, string fileName, string subFolder = "resumes");

    /// <summary>
    /// Deletes a previously stored file by its relative path.
    /// </summary>
    Task DeleteFileAsync(string relativePath);

    /// <summary>
    /// Opens a read stream for the file at the given relative path.
    /// </summary>
    Task<Stream?> GetFileAsync(string relativePath);
}
