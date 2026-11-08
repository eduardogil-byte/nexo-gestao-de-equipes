using Microsoft.AspNetCore.Identity;

namespace Nexo.Api.Domain;

public enum ApprovalState { Pending, Approved, Rejected }
public enum ProjectState { Proposed, InProgress, Paused, Completed, Cancelled }
public enum ProjectVisibility { Private, PublicInsideNucleus }
public enum WorkState { NotStarted, InProgress, Completed, Blocked, Cancelled }
public enum Priority { Low, Normal, High, Urgent }
public enum ActivityCategory { Development, Research, Testing, Documentation, Meeting, Support, Other }
public enum ContributionState { Pending, Approved, Rejected }
public enum ObstacleState { Open, Resolved }
public enum Shift { Morning, Afternoon, Evening }

public sealed class AppUser : IdentityUser<Guid>
{
    public string FullName { get; set; } = "";
    public string? Specialty { get; set; }
    public ApprovalState Approval { get; set; } = ApprovalState.Pending;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class WorkSchedule
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public DayOfWeek Weekday { get; set; }
    public Shift Shift { get; set; }
}

public sealed class ResearchProject
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string Objectives { get; set; } = "";
    public string Justification { get; set; } = "";
    public string ResearchArea { get; set; } = "";
    public decimal? Budget { get; set; }
    public string ExpectedResults { get; set; } = "";
    public DateOnly? PlannedStart { get; set; }
    public DateOnly? PlannedEnd { get; set; }
    public Guid LeaderId { get; set; }
    public ProjectState Status { get; set; } = ProjectState.Proposed;
    public ProjectVisibility Visibility { get; set; } = ProjectVisibility.Private;
    public string CollaborationCode { get; set; } = Guid.NewGuid().ToString("N");
    public int? LeaderProgressPercent { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? StartedAtUtc { get; set; }
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}

public sealed class ProjectMember
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProjectId { get; set; }
    public Guid UserId { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime JoinedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? LeftAtUtc { get; set; }
}

public sealed class ProjectTask
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProjectId { get; set; }
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public Priority Priority { get; set; } = Priority.Normal;
    public WorkState Status { get; set; } = WorkState.NotStarted;
    public DateOnly? DueDate { get; set; }
    public Guid CreatedById { get; set; }
    public bool NeedsRedistribution { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAtUtc { get; set; }
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}

public sealed class TaskAssignee
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TaskId { get; set; }
    public Guid UserId { get; set; }
    public bool Accepted { get; set; }
    public bool IsActive { get; set; } = true;
    public Guid ProposedById { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class TaskComment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TaskId { get; set; }
    public Guid UserId { get; set; }
    public string Text { get; set; } = "";
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class ActivityEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public Guid? ProjectId { get; set; }
    public Guid? TaskId { get; set; }
    public DateOnly ActivityDate { get; set; }
    public ActivityCategory Category { get; set; }
    public string Description { get; set; } = "";
    public int MinutesSpent { get; set; }
    public string? PartialResult { get; set; }
    public bool NoActivity { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class ProjectMilestone
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProjectId { get; set; }
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public WorkState Status { get; set; } = WorkState.NotStarted;
    public DateOnly? DueDate { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class ProjectDeliverable
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProjectId { get; set; }
    public string Title { get; set; } = "";
    public string Kind { get; set; } = "Other";
    public string? Description { get; set; }
    public string? ExternalUrl { get; set; }
    public WorkState Status { get; set; } = WorkState.NotStarted;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAtUtc { get; set; }
}

public sealed class ProjectObstacle
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProjectId { get; set; }
    public Guid? TaskId { get; set; }
    public Guid ReportedById { get; set; }
    public string Description { get; set; } = "";
    public ObstacleState Status { get; set; } = ObstacleState.Open;
    public string? Resolution { get; set; }
    public DateTime ReportedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ResolvedAtUtc { get; set; }
}

public sealed class ProjectContribution
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProjectId { get; set; }
    public Guid UserId { get; set; }
    public DateOnly ActivityDate { get; set; }
    public string Description { get; set; } = "";
    public int MinutesSpent { get; set; }
    public ContributionState Status { get; set; } = ContributionState.Pending;
    public Guid? ReviewedById { get; set; }
    public DateTime? ReviewedAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class StoredFile
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProjectId { get; set; }
    public Guid? TaskId { get; set; }
    public Guid UploadedById { get; set; }
    public string OriginalName { get; set; } = "";
    public string StoredName { get; set; } = "";
    public string ContentType { get; set; } = "application/octet-stream";
    public long Length { get; set; }
    public DateTime UploadedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class ExternalDocument
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProjectId { get; set; }
    public Guid? TaskId { get; set; }
    public Guid AddedById { get; set; }
    public string Title { get; set; } = "";
    public string Url { get; set; } = "";
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class UserNotification
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public string Kind { get; set; } = "";
    public string Message { get; set; } = "";
    public Guid? ProjectId { get; set; }
    public Guid? TaskId { get; set; }
    public string? DeduplicationKey { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ReadAtUtc { get; set; }
}

public sealed class AuditEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ActorId { get; set; }
    public Guid? ProjectId { get; set; }
    public string EntityType { get; set; } = "";
    public Guid EntityId { get; set; }
    public string Action { get; set; } = "";
    public string? Details { get; set; }
    public DateTime AtUtc { get; set; } = DateTime.UtcNow;
}
