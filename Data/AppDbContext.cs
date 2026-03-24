using AIResumeAssistant.Models.Domain;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace AIResumeAssistant.Data;

public class AppDbContext : IdentityDbContext<AppUser>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Resume> Resumes => Set<Resume>();
    public DbSet<ResumeVersion> ResumeVersions => Set<ResumeVersion>();
    public DbSet<ChatSession> ChatSessions => Set<ChatSession>();
    public DbSet<ChatMessageEntity> ChatMessages => Set<ChatMessageEntity>();
    public DbSet<AtsScore> AtsScores => Set<AtsScore>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Resume>(e =>
        {
            e.HasOne(r => r.User)
                .WithMany(u => u.Resumes)
                .HasForeignKey(r => r.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasIndex(r => new { r.UserId, r.IsActive });
        });

        builder.Entity<ResumeVersion>(e =>
        {
            e.HasOne(v => v.Resume)
                .WithMany(r => r.Versions)
                .HasForeignKey(v => v.ResumeId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasIndex(v => new { v.ResumeId, v.VersionNumber }).IsUnique();
        });

        builder.Entity<ChatSession>(e =>
        {
            e.HasOne(s => s.Resume)
                .WithMany(r => r.ChatSessions)
                .HasForeignKey(s => s.ResumeId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<ChatMessageEntity>(e =>
        {
            e.HasOne(m => m.Session)
                .WithMany(s => s.Messages)
                .HasForeignKey(m => m.SessionId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasIndex(m => m.SessionId);
        });

        builder.Entity<AtsScore>(e =>
        {
            e.HasOne(a => a.Resume)
                .WithMany(r => r.AtsScores)
                .HasForeignKey(a => a.ResumeId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
