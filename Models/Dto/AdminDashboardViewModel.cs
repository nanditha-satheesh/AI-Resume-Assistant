namespace AIResumeAssistant.Models.Dto;

public class AdminDashboardViewModel
{
    // Summary counts
    public int TotalUsers { get; set; }
    public int TotalResumes { get; set; }
    public int TotalChatSessions { get; set; }
    public int TotalMessages { get; set; }
    public int TotalAtsScans { get; set; }
    public double AverageAtsScore { get; set; }

    // Time-based
    public int ResumesToday { get; set; }
    public int MessagesToday { get; set; }
    public int NewUsersToday { get; set; }

    // Recent activity
    public List<RecentUpload> RecentUploads { get; set; } = [];
    public List<UserSummary> TopUsers { get; set; } = [];
}

public class RecentUpload
{
    public string UserEmail { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public DateTime UploadedAt { get; set; }
    public int TextLength { get; set; }
}

public class UserSummary
{
    public string UserId { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public DateTime JoinedAt { get; set; }
    public int ResumeCount { get; set; }
    public int MessageCount { get; set; }
}
