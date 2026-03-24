using AIResumeAssistant.Data;
using AIResumeAssistant.Models.Domain;
using AIResumeAssistant.Models.Dto;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AIResumeAssistant.Controllers;

[Authorize(Roles = "Admin")]
public class AdminController : Controller
{
    private readonly AppDbContext _db;
    private readonly UserManager<AppUser> _userManager;

    public AdminController(AppDbContext db, UserManager<AppUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    public async Task<IActionResult> Index()
    {
        var today = DateTime.UtcNow.Date;

        var totalUsers = await _userManager.Users.CountAsync();
        var totalResumes = await _db.Resumes.CountAsync();
        var totalSessions = await _db.ChatSessions.CountAsync();
        var totalMessages = await _db.ChatMessages.CountAsync();
        var totalAtsScans = await _db.AtsScores.CountAsync();

        var averageAts = totalAtsScans > 0
            ? await _db.AtsScores.AverageAsync(a => (double)a.OverallScore)
            : 0;

        var resumesToday = await _db.Resumes.CountAsync(r => r.UploadedAt >= today);
        var messagesToday = await _db.ChatMessages.CountAsync(m => m.Timestamp >= today);
        var newUsersToday = await _userManager.Users.CountAsync(u => u.CreatedAt >= today);

        // Recent 10 uploads with user info
        var recentUploads = await _db.Resumes
            .OrderByDescending(r => r.UploadedAt)
            .Take(10)
            .Select(r => new RecentUpload
            {
                UserEmail = r.User.Email ?? "N/A",
                FileName = r.FileName,
                UploadedAt = r.UploadedAt,
                TextLength = r.ExtractedText.Length
            })
            .ToListAsync();

        // Top 10 users by message count
        var topUsers = await _userManager.Users
            .OrderByDescending(u => u.Resumes.SelectMany(r => r.ChatSessions).SelectMany(s => s.Messages).Count())
            .Take(10)
            .Select(u => new UserSummary
            {
                UserId = u.Id,
                Email = u.Email ?? "N/A",
                FullName = u.FullName,
                JoinedAt = u.CreatedAt,
                ResumeCount = u.Resumes.Count,
                MessageCount = u.Resumes.SelectMany(r => r.ChatSessions).SelectMany(s => s.Messages).Count()
            })
            .ToListAsync();

        var model = new AdminDashboardViewModel
        {
            TotalUsers = totalUsers,
            TotalResumes = totalResumes,
            TotalChatSessions = totalSessions,
            TotalMessages = totalMessages,
            TotalAtsScans = totalAtsScans,
            AverageAtsScore = Math.Round(averageAts, 1),
            ResumesToday = resumesToday,
            MessagesToday = messagesToday,
            NewUsersToday = newUsersToday,
            RecentUploads = recentUploads,
            TopUsers = topUsers
        };

        return View(model);
    }

    /// <summary>
    /// Promotes a user to the Admin role.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PromoteToAdmin(string userId)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user is null)
        {
            TempData["Error"] = "User not found.";
            return RedirectToAction(nameof(Index));
        }

        if (!await _userManager.IsInRoleAsync(user, "Admin"))
        {
            await _userManager.AddToRoleAsync(user, "Admin");
            TempData["Success"] = $"{user.Email} has been promoted to Admin.";
        }
        else
        {
            TempData["Info"] = $"{user.Email} is already an Admin.";
        }

        return RedirectToAction(nameof(Index));
    }
}
