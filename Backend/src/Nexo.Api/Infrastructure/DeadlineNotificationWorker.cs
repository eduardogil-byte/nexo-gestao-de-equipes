using Microsoft.EntityFrameworkCore;
using Nexo.Api.Data;
using Nexo.Api.Domain;

namespace Nexo.Api.Infrastructure;

public sealed class DeadlineNotificationWorker(IServiceScopeFactory scopes, ILogger<DeadlineNotificationWorker> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Atrasos são avaliados diariamente; falhas não derrubam a API.
        using var timer = new PeriodicTimer(TimeSpan.FromHours(6));
        do
        {
            try { await CheckDeadlines(stoppingToken); }
            catch (Exception ex) when (ex is not OperationCanceledException)
            { logger.LogError(ex, "Falha ao verificar prazos."); }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task CheckDeadlines(CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NexoDbContext>();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var soon = today.AddDays(1);
        var tasks = await db.Tasks.AsNoTracking()
            .Where(t => t.DueDate != null && t.DueDate <= soon &&
                        t.Status != WorkState.Completed && t.Status != WorkState.Cancelled)
            .Take(2000).ToListAsync(ct);
        foreach (var task in tasks)
        {
            var project = await db.Projects.AsNoTracking().FirstOrDefaultAsync(p => p.Id == task.ProjectId, ct);
            if (project is null || project.Status is ProjectState.Cancelled or ProjectState.Completed) continue;
            var ids = await db.TaskAssignees.AsNoTracking().Where(a => a.TaskId == task.Id && a.Accepted && a.IsActive)
                .Select(a => a.UserId).ToListAsync(ct);
            ids.Add(project.LeaderId);
            foreach (var userId in ids.Distinct())
            {
                var kind = task.DueDate < today ? "TaskOverdue" : "TaskDueSoon";
                var key = $"{kind}:{task.Id:N}:{userId:N}:{today:yyyyMMdd}";
                if (await db.Notifications.AnyAsync(n => n.DeduplicationKey == key, ct)) continue;
                db.Notifications.Add(new UserNotification
                {
                    UserId = userId, Kind = kind, ProjectId = task.ProjectId, TaskId = task.Id,
                    Message = task.DueDate < today ? $"Tarefa atrasada: {task.Title}" : $"Prazo próximo: {task.Title}",
                    DeduplicationKey = key
                });
            }
        }
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException ex)
        {
            // Em múltiplas instâncias, o índice único evita a notificação duplicada.
            logger.LogWarning(ex, "Uma verificação de prazos simultânea gerou duplicidade evitada pelo banco.");
        }
    }
}
