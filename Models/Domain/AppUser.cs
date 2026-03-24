using Microsoft.AspNetCore.Identity;

namespace AIResumeAssistant.Models.Domain;

public class AppUser : IdentityUser
{
    public string FullName { get; set; } = string.Empty;
    public string? GoogleId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Resume> Resumes { get; set; } = [];
}
