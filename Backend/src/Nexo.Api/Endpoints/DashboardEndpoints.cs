using Microsoft.EntityFrameworkCore;
using Nexo.Api.Data;
using Nexo.Api.Domain;
using Nexo.Api.Infrastructure;

namespace Nexo.Api.Endpoints;

public static class DashboardEndpoints
{
    public static void MapDashboardEndpoints(this WebApplication app)
    {
        var g = app.MapGroup("/api/dashboard").RequireAuthorization().WithTags("Dashboard");
        g.MapGet("", Home);
        g.MapGet("/projects/{id:guid}", ProjectSummary);
        g.MapGet("/recent", Recent);
        g.MapGet("/activity-summary", ActivitySummary);
    }

    private static async Task<IResult> Home(NexoDbContext db, AccessService access)
    {
        var userId = access.UserId;
        var projects = access.IsAdmin ? db.Projects.AsNoTracking() :
            db.Projects.AsNoTracking().Where(p => db.ProjectMembers.Any(m =>
                m.ProjectId == p.Id && m.UserId == userId && m.IsActive));
        var totalProjects = await projects.CountAsync();
        var activeProjects = await projects.CountAsync(x => x.Status == ProjectState.InProgress);
        var tasks = access.IsAdmin ? db.Tasks.AsNoTracking() :
            db.Tasks.AsNoTracking().Where(t => db.TaskAssignees.Any(a =>
                a.TaskId == t.Id && a.UserId == userId && a.Accepted && a.IsActive) &&
                db.ProjectMembers.Any(m => m.ProjectId == t.ProjectId && m.UserId == userId && m.IsActive));
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var tasksOpen = await tasks.CountAsync(t => t.Status != WorkState.Completed && t.Status != WorkState.Cancelled);
        var completed = await tasks.CountAsync(t => t.Status == WorkState.Completed);
        var overdue = await tasks.CountAsync(t => t.DueDate != null && t.DueDate < today &&
            t.Status != WorkState.Completed && t.Status != WorkState.Cancelled);
        var oneWeekAgo = today.AddDays(-6);
        var logs = db.Activities.AsNoTracking().Where(a => a.ActivityDate >= oneWeekAgo && a.ActivityDate <= today);
        if (!access.IsAdmin) logs = logs.Where(a => a.UserId == userId);
        var hours = (await logs.SumAsync(x => (int?)x.MinutesSpent) ?? 0) / 60m;
        var obstacles = access.IsAdmin ? db.Obstacles.AsNoTracking() :
            db.Obstacles.AsNoTracking().Where(o => db.ProjectMembers.Any(m =>
                m.ProjectId == o.ProjectId && m.UserId == userId && m.IsActive));
        var openObstacles = await obstacles.CountAsync(x => x.Status == ObstacleState.Open);
        var upcoming = await (from t in tasks
            join p in db.Projects.AsNoTracking() on t.ProjectId equals p.Id
            where t.Status != WorkState.Completed && t.Status != WorkState.Cancelled
            orderby t.DueDate, t.Priority descending
            select new { t.Id, t.Title, t.Status, t.Priority, t.DueDate, t.ProjectId, projectName = p.Name })
            .Take(10).ToListAsync();
        var recentQuery = db.Activities.AsNoTracking();
        if (!access.IsAdmin) recentQuery = recentQuery.Where(a => a.UserId == userId);
        var recentActivities = await recentQuery
            .OrderByDescending(a => a.CreatedAtUtc).Take(8)
            .Select(a => new { a.Id, a.ActivityDate, a.CreatedAtUtc, a.ProjectId, a.Description, a.NoActivity })
            .ToListAsync();
        var unread = await db.Notifications.CountAsync(n => n.UserId == userId && n.ReadAtUtc == null);
        return Results.Ok(new { totalProjects, activeProjects, tasksOpen, completedTasks = completed,
            overdueTasks = overdue, openObstacles, hoursLast7Days = hours,
            unreadNotifications = unread, upcomingTasks = upcoming, recentActivities,
            note = "Indicadores informativos; horas registradas não equivalem a controle de ponto." });
    }

    private static async Task<IResult> ProjectSummary(Guid id, NexoDbContext db, AccessService access)
    {
        var p = await db.Projects.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (p is null) return Results.NotFound();
        if (!await access.CanRead(p)) return EndpointHelpers.Error("Sem acesso.", 403);
        var total = await db.Tasks.CountAsync(t => t.ProjectId == id && t.Status != WorkState.Cancelled);
        var completed = await db.Tasks.CountAsync(t => t.ProjectId == id && t.Status == WorkState.Completed);
        var totalMilestones = await db.Milestones.CountAsync(m => m.ProjectId == id && m.Status != WorkState.Cancelled);
        var finishedMilestones = await db.Milestones.CountAsync(m => m.ProjectId == id && m.Status == WorkState.Completed);
        var deliveries = await db.Deliverables.CountAsync(d => d.ProjectId == id);
        var finishedDeliveries = await db.Deliverables.CountAsync(d => d.ProjectId == id && d.Status == WorkState.Completed);
        var isPrivateParticipant = await access.CanWork(p);
        var obstacles = isPrivateParticipant ? await db.Obstacles.CountAsync(o => o.ProjectId == id && o.Status == ObstacleState.Open) : (int?)null;
        return Results.Ok(new
        {
            p.Id, p.Name, p.Status, p.LeaderId, p.Visibility,
            tasks = new { total, completed, percent = total == 0 ? (decimal?)null : Math.Round(100m * completed / total, 1) },
            milestones = new { total = totalMilestones, completed = finishedMilestones,
                percent = totalMilestones == 0 ? (decimal?)null : Math.Round(100m * finishedMilestones / totalMilestones, 1) },
            leaderProgressPercent = p.LeaderProgressPercent,
            deliverables = new { total = deliveries, completed = finishedDeliveries },
            openObstacles = obstacles,
            note = "Progresso das tarefas, etapas e avaliação do líder são exibidos separadamente."
        });
    }

    private static async Task<IResult> Recent(Guid? projectId, int? limit, NexoDbContext db, AccessService access)
    {
        ResearchProject? p = null;
        if (projectId is not null)
        {
            p = await db.Projects.AsNoTracking().FirstOrDefaultAsync(x => x.Id == projectId);
            if (p is null) return Results.NotFound();
            if (!await access.CanRead(p)) return EndpointHelpers.Error("Sem acesso.", 403);
        }
        // Os detalhes de auditoria são restritos a líder/coordenação. Integrantes consultam suas ações.
        var q = db.AuditEvents.AsNoTracking().AsQueryable();
        if (projectId is not null) q = q.Where(e => e.ProjectId == projectId);
        if (!access.IsAdmin && (p is null || p.LeaderId != access.UserId))
        {
            var userId = access.UserId;
            q = q.Where(e => e.ActorId == userId);
        }
        var max = Math.Clamp(limit ?? 30, 1, 100);
        return Results.Ok(await q.OrderByDescending(e => e.AtUtc).Take(max)
            .Select(e => new { e.Id, e.ProjectId, e.EntityType, e.EntityId, e.Action, e.ActorId, e.AtUtc })
            .ToListAsync());
    }

    private static async Task<IResult> ActivitySummary(DateOnly? from, DateOnly? to, Guid? projectId,
        NexoDbContext db, AccessService access)
    {
        var end = to ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var start = from ?? end.AddDays(-6);
        if (end < start || end.DayNumber - start.DayNumber > 90) return EndpointHelpers.Error("Intervalo inválido (máximo 90 dias).");
        ResearchProject? p = null;
        if (projectId.HasValue)
        {
            p = await db.Projects.AsNoTracking().FirstOrDefaultAsync(x => x.Id == projectId);
            if (p is null) return Results.NotFound();
            if (!await access.CanRead(p)) return EndpointHelpers.Error("Sem acesso.", 403);
        }
        var q = db.Activities.AsNoTracking().Where(a => a.ActivityDate >= start && a.ActivityDate <= end);
        if (projectId.HasValue) q = q.Where(a => a.ProjectId == projectId);
        if (!access.IsAdmin && (p is null || p.LeaderId != access.UserId))
        {
            var userId = access.UserId;
            q = q.Where(a => a.UserId == userId);
        }
        var items = await q.OrderByDescending(x => x.ActivityDate)
            .Select(a => new { a.ActivityDate, a.ProjectId, a.Category, a.NoActivity, a.MinutesSpent }).Take(5000).ToListAsync();
        var byDay = items.GroupBy(a => a.ActivityDate).OrderBy(g => g.Key)
            .Select(g => new { date = g.Key, entries = g.Count(x => !x.NoActivity), minutes = g.Sum(x => x.MinutesSpent),
                withoutActivity = g.Count(x => x.NoActivity) }).ToArray();
        var byCategory = items.Where(a => !a.NoActivity).GroupBy(a => a.Category)
            .Select(g => new { category = g.Key, entries = g.Count(), minutes = g.Sum(x => x.MinutesSpent) }).ToArray();
        var byProject = items.Where(a => a.ProjectId != null && !a.NoActivity).GroupBy(a => a.ProjectId)
            .Select(g => new { projectId = g.Key, entries = g.Count(), minutes = g.Sum(x => x.MinutesSpent) }).ToArray();
        return Results.Ok(new { from = start, to = end, byDay, byCategory, byProject,
            truncated = items.Count == 5000, note = "Dados de registros declarados, não métricas automáticas de produtividade." });
    }
}
