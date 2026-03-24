namespace AIResumeAssistant.Services;

public class LocalFileStorageService : IFileStorageService
{
    private readonly string _basePath;
    private readonly ILogger<LocalFileStorageService> _logger;

    public LocalFileStorageService(IWebHostEnvironment env, ILogger<LocalFileStorageService> logger)
    {
        _logger = logger;
        _basePath = Path.Combine(env.ContentRootPath, "AppData", "uploads");
        Directory.CreateDirectory(_basePath);
    }

    public async Task<string> SaveFileAsync(Stream stream, string fileName, string subFolder = "resumes")
    {
        var folder = Path.Combine(_basePath, subFolder);
        Directory.CreateDirectory(folder);

        // Generate unique name to avoid collisions
        var uniqueName = $"{Guid.NewGuid():N}_{Path.GetFileName(fileName)}";
        var fullPath = Path.Combine(folder, uniqueName);

        await using var fileStream = new FileStream(fullPath, FileMode.Create, FileAccess.Write);
        await stream.CopyToAsync(fileStream);

        var relativePath = Path.Combine(subFolder, uniqueName);
        _logger.LogInformation("File saved: {RelativePath} ({Bytes} bytes)", relativePath, fileStream.Length);

        return relativePath;
    }

    public Task DeleteFileAsync(string relativePath)
    {
        var fullPath = Path.Combine(_basePath, relativePath);

        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
            _logger.LogInformation("File deleted: {RelativePath}", relativePath);
        }

        return Task.CompletedTask;
    }

    public Task<Stream?> GetFileAsync(string relativePath)
    {
        var fullPath = Path.Combine(_basePath, relativePath);

        if (!File.Exists(fullPath))
            return Task.FromResult<Stream?>(null);

        Stream stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read);
        return Task.FromResult<Stream?>(stream);
    }
}
