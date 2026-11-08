using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Nexo.Api.Data;
using Nexo.Api.Domain;
using Nexo.Api.Infrastructure;

namespace Nexo.Api.Endpoints;

public static partial class TaskEndpoints
{
    public sealed record CreateTask(string Title, string Description, Priority Priority,
        DateOnly? DueDate, List<Guid>? AssigneeIds);
    public sealed record EditTask(string Title, string Description, Priority Priority, DateOnly? DueDate);
    public sealed record ChangeTaskState(WorkState Status);
    public sealed record CompleteTask(bool ForceAll = false);
    public sealed record ProposeAssignee(Guid UserId);
    public sealed record CommentInput(string Text);

    public static void MapTaskEndpoints(this WebApplication app)
    {
        var p = app.MapGroup("/api/projects/{projectId:guid}/tasks").RequireAuthorization().WithTags("Tarefas");
        p.MapGet("", ListTasks);
        p.MapPost("", NewTask);
        var t = app.MapGroup("/api/tasks").RequireAuthorization().WithTags("Tarefas");
        t.MapGet("/mine", Mine);
        t.MapGet("/{id:guid}", Details);
        t.MapPut("/{id:guid}", Edit);
        t.MapPost("/{id:guid}/state", SetState);
        t.MapPost("/{id:guid}/complete", Complete);
        t.MapPost("/{id:guid}/reopen", Reopen);
        t.MapPost("/{id:guid}/assignees", AddAssignee);
        t.MapPost("/{id:guid}/accept", Accept);
        t.MapPost("/{id:guid}/decline", Decline);
        t.MapDelete("/{id:guid}/assignees/{userId:guid}", RemoveAssignee);
        t.MapGet("/{id:guid}/comments", Comments);
        t.MapPost("/{id:guid}/comments", Comment);
    }

    private static async Task<IResult> ListTasks(Guid projectId, NexoDbContext db, AccessService access)
    {
        var p = await db.Projects.AsNoTracking().FirstOrDefaultAsync(x => x.Id == projectId);
        if (p is null) return Results.NotFound();
        if (!await access.CanRead(p)) return EndpointHelpers.Error("Sem acesso.", 403);
        var q = await db.Tasks.AsNoTracking().Where(x => x.ProjectId == projectId)
            .OrderBy(x => x.Status).ThenBy(x => x.DueDate).Take(500)
            .Select(x => new { x.Id, x.ProjectId, x.Title, x.Description, x.Priority, x.Status, x.DueDate,
                x.NeedsRedistribution, x.CreatedById, x.CreatedAtUtc, x.UpdatedAtUtc }).ToListAsync();
        return Results.Ok(q);
    }

    private static async Task<IResult> NewTask(Guid projectId, CreateTask req, NexoDbContext db, AccessService access, DomainEvents events)
    {
        var p = await db.Projects.FirstOrDefaultAsync(x => x.Id == projectId);
        if (p is null) return Results.NotFound();
        if (!await access.CanWork(p)) return EndpointHelpers.Error("Somente integrantes podem criar tarefas.", 403);
        if (!EndpointHelpers.IsEditable(p)) return EndpointHelpers.Error("Projeto encerrado.", 409);
        if (!Valid(req.Title, req.Description, req.Priority)) return EndpointHelpers.Error("Título, descrição ou prioridade inválidos.");
        var canAssign = await access.CanLead(p);
        var recipients = req.AssigneeIds?.Distinct().ToList() ?? new List<Guid>();
        if (recipients.Count == 0 && await access.IsMember(p.Id)) recipients.Add(access.UserId);
        if (recipients.Count == 0 || recipients.Count > 25) return EndpointHelpers.Error("Informe de 1 a 25 responsáveis.");
        foreach (var userId in recipients)
            if (!await access.IsActiveProjectMember(projectId, userId))
                return EndpointHelpers.Error("Todos os responsáveis precisam ser integrantes ativos.");
        var task = new ProjectTask
        {
            ProjectId = projectId, Title = req.Title.Trim(), Description = req.Description?.Trim() ?? "",
            Priority = req.Priority, DueDate = req.DueDate, CreatedById = access.UserId
        };
        db.Tasks.Add(task);
        foreach (var userId in recipients)
        {
            var accepted = canAssign || userId == access.UserId;
            db.TaskAssignees.Add(new TaskAssignee
            {
                TaskId = task.Id, UserId = userId, ProposedById = access.UserId, Accepted = accepted
            });
            if (userId != access.UserId) events.Notify(userId,
                accepted ? "TaskAssigned" : "TaskAcceptanceRequested",
                accepted ? $"Nova tarefa: {task.Title}" : $"Solicitação de tarefa: {task.Title}", projectId, task.Id);
        }
        events.Audit("Task", task.Id, "Created", projectId, new { task.Title, recipients });
        await events.BeginOnMemberWork(p, "TaskCreated");
        await db.SaveChangesAsync();
        return Results.Created($"/api/tasks/{task.Id}", new { task.Id, task.ProjectId, task.Status });
    }

    private static async Task<IResult> Mine(NexoDbContext db, AccessService access)
    {
        var userId = access.UserId;
        var query = from a in db.TaskAssignees.AsNoTracking()
                    join t in db.Tasks.AsNoTracking() on a.TaskId equals t.Id
                    join p in db.Projects.AsNoTracking() on t.ProjectId equals p.Id
                    join m in db.ProjectMembers.AsNoTracking() on new { ProjectId = p.Id, UserId = userId }
                        equals new { m.ProjectId, m.UserId }
                    where a.UserId == userId && a.IsActive && m.IsActive
                    select new { t.Id, t.ProjectId, projectName = p.Name, t.Title, t.Status, t.Priority,
                        t.DueDate, a.Accepted, a.CompletedAtUtc, t.NeedsRedistribution };
        return Results.Ok(await query.OrderBy(x => x.DueDate).Take(300).ToListAsync());
    }

    private static async Task<IResult> Details(Guid id, NexoDbContext db, AccessService access)
    {
        var t = await db.Tasks.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (t is null) return Results.NotFound();
        var p = await db.Projects.AsNoTracking().FirstAsync(x => x.Id == t.ProjectId);
        if (!await access.CanRead(p)) return EndpointHelpers.Error("Sem acesso.", 403);
        var members = await db.TaskAssignees.AsNoTracking().Where(a => a.TaskId == id && a.IsActive)
            .Select(a => new { a.UserId, a.Accepted, a.CompletedAtUtc, a.ProposedById }).ToListAsync();
        return Results.Ok(new { t.Id, t.ProjectId, projectName = p.Name, t.Title, t.Description,
            t.Status, t.Priority, t.DueDate, t.NeedsRedistribution, t.CreatedById, t.CreatedAtUtc,
            t.UpdatedAtUtc, t.CompletedAtUtc, assignees = members });
    }

    private static async Task<IResult> Edit(Guid id, EditTask req, NexoDbContext db, AccessService access, DomainEvents events)
    {
        var result = await LoadEditable(id, db, access);
        if (result.Error is not null) return result.Error;
        var (task, project) = (result.Task!, result.Project!);
        if (!await access.CanManageTask(task, project)) return EndpointHelpers.Error("Sem permissão para editar.", 403);
        if (!Valid(req.Title, req.Description, req.Priority)) return EndpointHelpers.Error("Título, descrição ou prioridade inválidos.");
        var before = new { task.Title, task.Description, task.Priority, task.DueDate };
        task.Title = req.Title.Trim(); task.Description = req.Description?.Trim() ?? "";
        task.Priority = req.Priority; task.DueDate = req.DueDate; task.UpdatedAtUtc = DateTime.UtcNow;
        events.Audit("Task", task.Id, "Edited", task.ProjectId, new { before, after = req });
        await events.BeginOnMemberWork(project, "TaskEdited");
        await db.SaveChangesAsync();
        return Results.NoContent();
    }

    private static async Task<IResult> SetState(Guid id, ChangeTaskState req, NexoDbContext db, AccessService access, DomainEvents events)
    {
        var r = await LoadEditable(id, db, access);
        if (r.Error is not null) return r.Error;
        var (task, project) = (r.Task!, r.Project!);
        if (!await access.CanManageTask(task, project)) return EndpointHelpers.Error("Sem permissão.", 403);
        if (!Enum.IsDefined(req.Status) || req.Status == WorkState.Completed)
            return EndpointHelpers.Error("Use o endpoint /complete para concluir tarefas.");
        if (task.Status == WorkState.Completed) return EndpointHelpers.Error("Somente líder/coordenação pode reabrir tarefa concluída.", 409);
        if (req.Status == WorkState.Cancelled && !await access.CanLead(project))
            return EndpointHelpers.Error("Somente líder/coordenação pode cancelar tarefas.", 403);
        var before = task.Status;
        task.Status = req.Status; task.UpdatedAtUtc = DateTime.UtcNow;
        events.Audit("Task", id, "StatusChanged", task.ProjectId, new { before, after = req.Status });
        await events.BeginOnMemberWork(project, "TaskStatusChanged");
        await db.SaveChangesAsync();
        return Results.NoContent();
    }

    private static async Task<IResult> Complete(Guid id, CompleteTask req, NexoDbContext db, AccessService access, DomainEvents events)
    {
        var r = await LoadEditable(id, db, access);
        if (r.Error is not null) return r.Error;
        var (task, project) = (r.Task!, r.Project!);
        if (task.Status == WorkState.Completed) return EndpointHelpers.Error("Tarefa já concluída.", 409);
        if (task.Status == WorkState.Cancelled) return EndpointHelpers.Error("Tarefa cancelada.", 409);
        var assignees = await db.TaskAssignees.Where(x => x.TaskId == id && x.IsActive).ToListAsync();
        var current = assignees.FirstOrDefault(x => x.UserId == access.UserId && x.Accepted);
        if (current is null && !access.IsAdmin) return EndpointHelpers.Error("Apenas responsáveis aceitos podem concluir.", 403);
        if (current is not null) current.CompletedAtUtc = DateTime.UtcNow;
        if (req.ForceAll || assignees.Count > 0 && assignees.All(x => x.Accepted && x.CompletedAtUtc != null))
        {
            task.Status = WorkState.Completed; task.CompletedAtUtc = DateTime.UtcNow;
            events.Audit("Task", id, "Completed", task.ProjectId,
                new { req.ForceAll, completedBy = access.UserId,
                    individualCompletions = assignees.Where(a => a.CompletedAtUtc != null).Select(a => a.UserId) });
            await events.NotifyLeader(project, "TaskCompleted", $"Tarefa concluída: {task.Title}", id);
        }
        else
        {
            task.Status = WorkState.InProgress;
            events.Audit("Task", id, "ParticipationCompleted", task.ProjectId, new { userId = access.UserId });
        }
        task.UpdatedAtUtc = DateTime.UtcNow;
        await events.BeginOnMemberWork(project, "TaskProgress");
        await db.SaveChangesAsync();
        return Results.Ok(new { task.Id, task.Status, task.CompletedAtUtc });
    }

    private static async Task<IResult> Reopen(Guid id, NexoDbContext db, AccessService access, DomainEvents events)
    {
        var r = await LoadEditable(id, db, access);
        if (r.Error is not null) return r.Error;
        var (task, project) = (r.Task!, r.Project!);
        if (!await access.CanLead(project)) return EndpointHelpers.Error("Somente líder ou coordenação pode reabrir.", 403);
        if (task.Status != WorkState.Completed) return EndpointHelpers.Error("Apenas tarefa concluída pode ser reaberta.");
        var completed = await db.TaskAssignees.Where(x => x.TaskId == id && x.IsActive).ToListAsync();
        events.Audit("Task", id, "Reopened", project.Id, new
        {
            previousCompletedAt = task.CompletedAtUtc,
            individual = completed.Select(x => new { x.UserId, x.CompletedAtUtc })
        });
        foreach (var x in completed) x.CompletedAtUtc = null;
        task.Status = WorkState.InProgress; task.CompletedAtUtc = null; task.UpdatedAtUtc = DateTime.UtcNow;
        await events.BeginOnMemberWork(project, "TaskReopened");
        await db.SaveChangesAsync();
        return Results.NoContent();
    }

    private static async Task<IResult> AddAssignee(Guid id, ProposeAssignee req, NexoDbContext db, AccessService access, DomainEvents events)
    {
        var r = await LoadEditable(id, db, access);
        if (r.Error is not null) return r.Error;
        var (task, project) = (r.Task!, r.Project!);
        if (!await access.CanWork(project)) return EndpointHelpers.Error("Somente integrante da equipe.", 403);
        if (task.Status == WorkState.Completed) return EndpointHelpers.Error("Reabra a tarefa antes de alterar responsáveis.", 409);
        if (!await access.IsActiveProjectMember(project.Id, req.UserId))
            return EndpointHelpers.Error("Responsável não está na equipe.");
        var assignment = await db.TaskAssignees.FirstOrDefaultAsync(x => x.TaskId == id && x.UserId == req.UserId);
        if (assignment?.IsActive == true) return EndpointHelpers.Error("Responsável já cadastrado.", 409);
        var direct = await access.CanLead(project) || req.UserId == access.UserId;
        if (assignment is null) db.TaskAssignees.Add(new TaskAssignee
        {
            TaskId = id, UserId = req.UserId, ProposedById = access.UserId, Accepted = direct
        });
        else { assignment.IsActive = true; assignment.Accepted = direct; assignment.CompletedAtUtc = null;
            assignment.ProposedById = access.UserId; }
        task.NeedsRedistribution = false;
        events.Audit("Task", id, "AssigneeAdded", project.Id, new { req.UserId, direct });
        if (req.UserId != access.UserId) events.Notify(req.UserId,
            direct ? "TaskAssigned" : "TaskAcceptanceRequested",
            direct ? $"Nova atribuição: {task.Title}" : $"Aceite uma atribuição proposta: {task.Title}", project.Id, id);
        await events.BeginOnMemberWork(project, "TaskAssigneeAdded");
        await db.SaveChangesAsync();
        return Results.NoContent();
    }

    private static async Task<IResult> Accept(Guid id, NexoDbContext db, AccessService access, DomainEvents events)
    {
        var t = await db.Tasks.FirstOrDefaultAsync(x => x.Id == id);
        if (t is null) return Results.NotFound();
        var p = await db.Projects.FirstAsync(x => x.Id == t.ProjectId);
        if (!EndpointHelpers.IsEditable(p) || !await access.IsMember(p.Id)) return EndpointHelpers.Error("Projeto indisponível.", 403);
        var a = await db.TaskAssignees.FirstOrDefaultAsync(x => x.TaskId == id && x.UserId == access.UserId && x.IsActive);
        if (a is null || a.Accepted) return EndpointHelpers.Error("Nenhum aceite pendente.", 409);
        a.Accepted = true;
        events.Audit("Task", id, "AssignmentAccepted", p.Id);
        await events.BeginOnMemberWork(p, "TaskAccepted");
        await db.SaveChangesAsync();
        return Results.NoContent();
    }

    private static async Task<IResult> Decline(Guid id, NexoDbContext db, AccessService access, DomainEvents events)
    {
        var a = await db.TaskAssignees.FirstOrDefaultAsync(x => x.TaskId == id && x.UserId == access.UserId && x.IsActive && !x.Accepted);
        if (a is null) return EndpointHelpers.Error("Nenhuma proposta pendente.", 409);
        var t = await db.Tasks.FirstAsync(x => x.Id == id);
        var p = await db.Projects.FirstAsync(x => x.Id == t.ProjectId);
        a.IsActive = false;
        if (!await db.TaskAssignees.AnyAsync(x => x.TaskId == id && x.IsActive && x.Id != a.Id)) t.NeedsRedistribution = true;
        events.Audit("Task", id, "AssignmentDeclined", p.Id);
        events.Notify(p.LeaderId, "AssignmentDeclined", $"Atribuição recusada: {t.Title}", p.Id, id);
        await events.BeginOnMemberWork(p, "TaskAssignmentDeclined");
        await db.SaveChangesAsync();
        return Results.NoContent();
    }

    private static async Task<IResult> RemoveAssignee(Guid id, Guid userId, NexoDbContext db, AccessService access, DomainEvents events)
    {
        var r = await LoadEditable(id, db, access);
        if (r.Error is not null) return r.Error;
        var (task, project) = (r.Task!, r.Project!);
        if (!await access.CanLead(project)) return EndpointHelpers.Error("Somente líder/coordenação pode remover responsáveis.", 403);
        var a = await db.TaskAssignees.FirstOrDefaultAsync(x => x.TaskId == id && x.UserId == userId && x.IsActive);
        if (a is null) return Results.NotFound();
        a.IsActive = false;
        if (!await db.TaskAssignees.AnyAsync(x => x.TaskId == id && x.IsActive && x.Id != a.Id)) task.NeedsRedistribution = true;
        events.Audit("Task", id, "AssigneeRemoved", project.Id, new { userId });
        await events.BeginOnMemberWork(project, "TaskAssigneeRemoved");
        await db.SaveChangesAsync();
        return Results.NoContent();
    }

    private static async Task<IResult> Comments(Guid id, NexoDbContext db, AccessService access)
    {
        var task = await db.Tasks.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (task is null) return Results.NotFound();
        var project = await db.Projects.AsNoTracking().FirstAsync(x => x.Id == task.ProjectId);
        if (!await access.CanWork(project)) return EndpointHelpers.Error("Comentários restritos à equipe.", 403);
        var comments = await db.TaskComments.AsNoTracking().Where(x => x.TaskId == id)
            .OrderByDescending(x => x.CreatedAtUtc).Take(100).ToListAsync();
        return Results.Ok(comments);
    }

    private static async Task<IResult> Comment(Guid id, CommentInput req, NexoDbContext db, AccessService access, DomainEvents events)
    {
        var task = await db.Tasks.FirstOrDefaultAsync(x => x.Id == id);
        if (task is null) return Results.NotFound();
        var project = await db.Projects.FirstAsync(x => x.Id == task.ProjectId);
        if (!await access.CanWork(project)) return EndpointHelpers.Error("Sem acesso.", 403);
        if (!EndpointHelpers.IsEditable(project)) return EndpointHelpers.Error("Projeto encerrado.", 409);
        if (!EndpointHelpers.IsValid(req.Text, 4000)) return EndpointHelpers.Error("Comentário inválido.");
        var comment = new TaskComment { TaskId = id, UserId = access.UserId, Text = req.Text.Trim() };
        db.TaskComments.Add(comment);
        events.Audit("TaskComment", comment.Id, "Added", project.Id, new { taskId = id });
        var emails = EmailMentions().Matches(req.Text).Cast<Match>().Select(m => m.Groups[1].Value).Distinct(StringComparer.OrdinalIgnoreCase);
        foreach (var email in emails)
        {
            var user = await db.Users.FirstOrDefaultAsync(x => x.NormalizedEmail == email.ToUpper());
            if (user is null || user.Id == access.UserId || !await access.IsActiveProjectMember(project.Id, user.Id)) continue;
            events.Notify(user.Id, "Mention", $"Você foi mencionado em uma tarefa: {task.Title}", project.Id, task.Id);
        }
        await events.BeginOnMemberWork(project, "CommentAdded");
        await db.SaveChangesAsync();
        return Results.Created($"/api/tasks/{id}/comments", new { comment.Id, comment.CreatedAtUtc });
    }

    [GeneratedRegex(@"@([\w.%+\-]+@[\w.\-]+\.[A-Za-z]{2,})", RegexOptions.Compiled)]
    private static partial Regex EmailMentions();

    private static bool Valid(string? title, string? description, Priority priority) =>
        EndpointHelpers.IsValid(title, 200, 3) && description?.Length <= 5000 && Enum.IsDefined(priority);

    private static async Task<(ProjectTask? Task, ResearchProject? Project, IResult? Error)> LoadEditable(
        Guid taskId, NexoDbContext db, AccessService access)
    {
        var t = await db.Tasks.FirstOrDefaultAsync(x => x.Id == taskId);
        if (t is null) return (null, null, Results.NotFound());
        var p = await db.Projects.FirstAsync(x => x.Id == t.ProjectId);
        if (!await access.CanWork(p)) return (null, null, EndpointHelpers.Error("Sem acesso.", 403));
        if (!EndpointHelpers.IsEditable(p)) return (null, null, EndpointHelpers.Error("Projeto encerrado.", 409));
        return (t, p, null);
    }
}
