using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Nexo.Api.Domain;

namespace Nexo.Api.Data;

public sealed class NexoDbContext(DbContextOptions<NexoDbContext> options) : IdentityDbContext<AppUser, IdentityRole<Guid>, Guid>(options)
{
    public DbSet<WorkSchedule> WorkSchedules => Set<WorkSchedule>();
    public DbSet<ResearchProject> Projects => Set<ResearchProject>();
    public DbSet<ProjectMember> ProjectMembers => Set<ProjectMember>();
    public DbSet<ProjectTask> Tasks => Set<ProjectTask>();
    public DbSet<TaskAssignee> TaskAssignees => Set<TaskAssignee>();
    public DbSet<TaskComment> TaskComments => Set<TaskComment>();
    public DbSet<ActivityEntry> Activities => Set<ActivityEntry>();
    public DbSet<ProjectMilestone> Milestones => Set<ProjectMilestone>();
    public DbSet<ProjectDeliverable> Deliverables => Set<ProjectDeliverable>();
    public DbSet<ProjectObstacle> Obstacles => Set<ProjectObstacle>();
    public DbSet<ProjectContribution> Contributions => Set<ProjectContribution>();
    public DbSet<StoredFile> Files => Set<StoredFile>();
    public DbSet<ExternalDocument> ExternalDocuments => Set<ExternalDocument>();
    public DbSet<UserNotification> Notifications => Set<UserNotification>();
    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();

    protected override void OnModelCreating(ModelBuilder m)
    {
        base.OnModelCreating(m);
        m.Entity<AppUser>(e =>
        {
            e.Property(x => x.FullName).HasMaxLength(150).IsRequired();
            e.Property(x => x.Specialty).HasMaxLength(120);
            e.HasIndex(x => x.Approval);
        });
        m.Entity<WorkSchedule>(e =>
        {
            e.HasIndex(x => new { x.UserId, x.Weekday, x.Shift }).IsUnique();
            e.HasOne<AppUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        });
        m.Entity<ResearchProject>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(180).IsRequired();
            e.Property(x => x.Description).HasMaxLength(4000);
            e.Property(x => x.Objectives).HasMaxLength(4000);
            e.Property(x => x.Justification).HasMaxLength(4000);
            e.Property(x => x.ResearchArea).HasMaxLength(150);
            e.Property(x => x.ExpectedResults).HasMaxLength(4000);
            e.Property(x => x.Budget).HasPrecision(18, 2);
            e.Property(x => x.CollaborationCode).HasMaxLength(32);
            e.HasIndex(x => x.CollaborationCode).IsUnique();
            e.HasOne<AppUser>().WithMany().HasForeignKey(x => x.LeaderId).OnDelete(DeleteBehavior.Restrict);
            e.Property(x => x.RowVersion).IsRowVersion();
        });
        m.Entity<ProjectMember>(e =>
        {
            e.HasIndex(x => new { x.ProjectId, x.UserId }).IsUnique();
            e.HasOne<ResearchProject>().WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<AppUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        });
        m.Entity<ProjectTask>(e =>
        {
            e.Property(x => x.Title).HasMaxLength(200).IsRequired();
            e.Property(x => x.Description).HasMaxLength(5000);
            e.HasIndex(x => new { x.ProjectId, x.Status });
            e.HasOne<ResearchProject>().WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<AppUser>().WithMany().HasForeignKey(x => x.CreatedById).OnDelete(DeleteBehavior.Restrict);
            e.Property(x => x.RowVersion).IsRowVersion();
        });
        m.Entity<TaskAssignee>(e =>
        {
            e.HasIndex(x => new { x.TaskId, x.UserId }).IsUnique();
            e.HasOne<ProjectTask>().WithMany().HasForeignKey(x => x.TaskId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<AppUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        });
        m.Entity<TaskComment>(e =>
        {
            e.Property(x => x.Text).HasMaxLength(4000).IsRequired();
            e.HasOne<ProjectTask>().WithMany().HasForeignKey(x => x.TaskId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<AppUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        });
        m.Entity<ActivityEntry>(e =>
        {
            e.HasIndex(x => new { x.UserId, x.ActivityDate });
            e.HasIndex(x => new { x.ProjectId, x.ActivityDate });
            e.Property(x => x.Description).HasMaxLength(4000);
            e.Property(x => x.PartialResult).HasMaxLength(2000);
            e.HasOne<AppUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<ResearchProject>().WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<ProjectTask>().WithMany().HasForeignKey(x => x.TaskId).OnDelete(DeleteBehavior.Restrict);
        });
        m.Entity<ProjectMilestone>(e =>
        {
            e.Property(x => x.Title).HasMaxLength(200).IsRequired();
            e.Property(x => x.Description).HasMaxLength(2000);
            e.HasOne<ResearchProject>().WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Restrict);
        });
        m.Entity<ProjectDeliverable>(e =>
        {
            e.Property(x => x.Title).HasMaxLength(200).IsRequired();
            e.Property(x => x.Kind).HasMaxLength(80);
            e.Property(x => x.Description).HasMaxLength(3000);
            e.Property(x => x.ExternalUrl).HasMaxLength(2048);
            e.HasOne<ResearchProject>().WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Restrict);
        });
        m.Entity<ProjectObstacle>(e =>
        {
            e.Property(x => x.Description).HasMaxLength(4000).IsRequired();
            e.Property(x => x.Resolution).HasMaxLength(4000);
            e.HasOne<ResearchProject>().WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<ProjectTask>().WithMany().HasForeignKey(x => x.TaskId).OnDelete(DeleteBehavior.Restrict);
        });
        m.Entity<ProjectContribution>(e =>
        {
            e.Property(x => x.Description).HasMaxLength(3000).IsRequired();
            e.HasOne<ResearchProject>().WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<AppUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        });
        m.Entity<StoredFile>(e =>
        {
            e.Property(x => x.OriginalName).HasMaxLength(250).IsRequired();
            e.Property(x => x.StoredName).HasMaxLength(80).IsRequired();
            e.Property(x => x.ContentType).HasMaxLength(150);
            e.HasOne<ResearchProject>().WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Restrict);
        });
        m.Entity<ExternalDocument>(e =>
        {
            e.Property(x => x.Title).HasMaxLength(200).IsRequired();
            e.Property(x => x.Url).HasMaxLength(2048).IsRequired();
            e.HasOne<ResearchProject>().WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Restrict);
        });
        m.Entity<UserNotification>(e =>
        {
            e.Property(x => x.Kind).HasMaxLength(80);
            e.Property(x => x.Message).HasMaxLength(500);
            e.Property(x => x.DeduplicationKey).HasMaxLength(200);
            e.HasIndex(x => x.DeduplicationKey).IsUnique().HasFilter("[DeduplicationKey] IS NOT NULL");
            e.HasIndex(x => new { x.UserId, x.CreatedAtUtc });
            e.HasOne<AppUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        });
        m.Entity<AuditEvent>(e =>
        {
            e.Property(x => x.EntityType).HasMaxLength(80);
            e.Property(x => x.Action).HasMaxLength(80);
            e.Property(x => x.Details).HasMaxLength(6000);
            e.HasIndex(x => new { x.ProjectId, x.AtUtc });
            e.HasOne<AppUser>().WithMany().HasForeignKey(x => x.ActorId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
