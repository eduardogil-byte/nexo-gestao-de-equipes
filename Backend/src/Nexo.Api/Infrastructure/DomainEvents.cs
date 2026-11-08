using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Nexo.Api.Data;
using Nexo.Api.Domain;

namespace Nexo.Api.Infrastructure;

public sealed class DomainEvents(NexoDbContext db, AccessService access)
{
    public void Audit(string entity, Guid entityId, string action, Guid? projectId = null, object? details = null)
    {
        db.AuditEvents.Add(new AuditEvent
        {
            ActorId = access.UserId, EntityType = entity, EntityId = entityId,
            Action = action, ProjectId = projectId,
            Details = details is null ? null : JsonSerializer.Serialize(details)
        });
    }

    public void Notify(Guid userId, string kind, string message, Guid? projectId = null, Guid? taskId = null,
        string? deduplicationKey = null)
    {
        db.Notifications.Add(new UserNotification
        {
            UserId = userId, Kind = kind, Message = message.Length > 500 ? message[..500] : message,
            ProjectId = projectId, TaskId = taskId, DeduplicationKey = deduplicationKey
        });
    }

    public async Task BeginOnMemberWork(ResearchProject project, string action)
    {
        if (project.Status != ProjectState.Proposed || !await access.IsMember(project.Id)) return;
        project.Status = ProjectState.InProgress;
        project.StartedAtUtc = DateTime.UtcNow;
        project.UpdatedAtUtc = DateTime.UtcNow;
        Audit("Project", project.Id, "AutomaticallyStarted", project.Id, new { cause = action });
    }

    public async Task NotifyLeader(ResearchProject project, string kind, string message, Guid? taskId = null)
    {
        if (project.LeaderId != access.UserId)
            Notify(project.LeaderId, kind, message, project.Id, taskId);
        await Task.CompletedTask;
    }
}
