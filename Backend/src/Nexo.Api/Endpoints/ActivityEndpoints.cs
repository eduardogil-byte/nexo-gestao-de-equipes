using Microsoft.EntityFrameworkCore;
using Nexo.Api.Data;
using Nexo.Api.Domain;
using Nexo.Api.Infrastructure;

namespace Nexo.Api.Endpoints;

public static class ActivityEndpoints
{
    public sealed record ActivityInput(DateOnly ActivityDate, Guid? ProjectId, Guid? TaskId,
        ActivityCategory Category, string? Description, int MinutesSpent, string? PartialResult, bool NoActivity);
    public sealed record ActivityEdit(DateOnly ActivityDate, ActivityCategory Category,
        string? Description, int MinutesSpent, string? PartialResult, bool NoActivity);

    public static void MapActivityEndpoints(this WebApplication app)
    {
        var g = app.MapGroup("/api/activities").RequireAuthorization().WithTags("Registros diários");
        g.MapGet("", List);
        g.MapPost("", Create);
        g.MapPut("/{id:guid}", Edit);
        g.MapGet("/missing-days", MissingDays);
    }

    private static async Task<IResult> Create(ActivityInput req, NexoDbContext db, AccessService access, DomainEvents events)
    {
        var validation = Validate(req.ActivityDate, req.Category, req.Description, req.MinutesSpent, req.PartialResult, req.NoActivity);
        if (validation is not null) return EndpointHelpers.Error(validation);
        if (req.NoActivity && (req.ProjectId != null || req.TaskId != null))
            return EndpointHelpers.Error("Registro 'Sem atividades' deve ser geral, sem projeto ou tarefa.");
        ResearchProject? project = null;
        if (req.ProjectId != null)
        {
            project = await db.Projects.FirstOrDefaultAsync(x => x.Id == req.ProjectId);
            if (project is null) return Results.NotFound();
            if (!await access.IsMember(project.Id)) return EndpointHelpers.Error("Apenas integrantes podem registrar trabalho do projeto.", 403);
            if (!EndpointHelpers.IsEditable(project)) return EndpointHelpers.Error("Projeto encerrado.", 409);
        }
        if (req.TaskId != null)
        {
            var task = await db.Tasks.AsNoTracking().FirstOrDefaultAsync(x => x.Id == req.TaskId);
            if (task is null || project is null || task.ProjectId != project.Id)
                return EndpointHelpers.Error("Tarefa não pertence ao projeto selecionado.");
        }
        if (req.NoActivity)
        {
            if (await db.Activities.AnyAsync(x => x.UserId == access.UserId && x.ActivityDate == req.ActivityDate))
                return EndpointHelpers.Error("Já existe registro nessa data. Corrija o registro existente.", 409);
        }
        else if (await db.Activities.AnyAsync(x => x.UserId == access.UserId && x.ActivityDate == req.ActivityDate && x.NoActivity))
            return EndpointHelpers.Error("Dia marcado como 'Sem atividades'. Edite esse registro para substituí-lo.", 409);
        var a = new ActivityEntry
        {
            UserId = access.UserId, ActivityDate = req.ActivityDate, ProjectId = req.ProjectId,
            TaskId = req.TaskId, Category = req.Category, Description = req.Description?.Trim() ?? "",
            MinutesSpent = req.MinutesSpent, PartialResult = req.PartialResult?.Trim(), NoActivity = req.NoActivity
        };
        db.Activities.Add(a);
        events.Audit("Activity", a.Id, "Created", project?.Id, new { a.ActivityDate, a.Category, a.NoActivity });
        if (project is not null) await events.BeginOnMemberWork(project, "ActivityRegistered");
        await db.SaveChangesAsync();
        return Results.Created($"/api/activities/{a.Id}", new { a.Id, a.ActivityDate, a.CreatedAtUtc,
            isRetroactive = req.ActivityDate < DateOnly.FromDateTime(DateTime.UtcNow) });
    }

    private static async Task<IResult> Edit(Guid id, ActivityEdit req, NexoDbContext db, AccessService access, DomainEvents events)
    {
        var a = await db.Activities.FirstOrDefaultAsync(x => x.Id == id);
        if (a is null) return Results.NotFound();
        if (a.UserId != access.UserId && !access.IsAdmin) return EndpointHelpers.Error("Sem permissão.", 403);
        var validation = Validate(req.ActivityDate, req.Category, req.Description, req.MinutesSpent, req.PartialResult, req.NoActivity);
        if (validation is not null) return EndpointHelpers.Error(validation);
        if (req.NoActivity && (a.ProjectId != null || a.TaskId != null))
            return EndpointHelpers.Error("Registros associados a projeto/tarefa não podem virar 'Sem atividades'.");
        if (req.NoActivity && await db.Activities.AnyAsync(x => x.UserId == a.UserId && x.ActivityDate == req.ActivityDate && x.Id != id))
            return EndpointHelpers.Error("Já existe atividade nessa data.", 409);
        if (!req.NoActivity && await db.Activities.AnyAsync(x => x.UserId == a.UserId && x.ActivityDate == req.ActivityDate && x.NoActivity && x.Id != id))
            return EndpointHelpers.Error("Dia marcado como 'Sem atividades'. Corrija o registro do dia.", 409);
        var previous = new { a.ActivityDate, a.Category, a.Description, a.MinutesSpent, a.PartialResult, a.NoActivity };
        a.ActivityDate = req.ActivityDate; a.Category = req.Category; a.Description = req.Description?.Trim() ?? "";
        a.MinutesSpent = req.MinutesSpent; a.PartialResult = req.PartialResult?.Trim();
        a.NoActivity = req.NoActivity; a.UpdatedAtUtc = DateTime.UtcNow;
        events.Audit("Activity", id, "Edited", a.ProjectId, new { previous, current = req });
        if (a.ProjectId is Guid projectId)
        {
            var p = await db.Projects.FirstOrDefaultAsync(x => x.Id == projectId);
            if (p is not null) await events.BeginOnMemberWork(p, "ActivityEdited");
        }
        await db.SaveChangesAsync();
        return Results.NoContent();
    }

    private static async Task<IResult> List(DateOnly? from, DateOnly? to, Guid? userId, Guid? projectId,
        NexoDbContext db, AccessService access)
    {
        var start = from ?? DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-6));
        var end = to ?? DateOnly.FromDateTime(DateTime.UtcNow);
        if (end < start || end.DayNumber - start.DayNumber > 365) return EndpointHelpers.Error("Intervalo inválido (máximo 365 dias).");
        if (userId != null && userId != access.UserId && !access.IsAdmin)
        {
            var p = projectId == null ? null : await db.Projects.AsNoTracking().FirstOrDefaultAsync(x => x.Id == projectId);
            if (p is null || p.LeaderId != access.UserId || !await access.IsActiveProjectMember(p.Id, userId.Value))
                return EndpointHelpers.Error("Somente coordenação ou líder do projeto pode consultar atividades de outro integrante.", 403);
        }
        if (projectId != null)
        {
            var p = await db.Projects.AsNoTracking().FirstOrDefaultAsync(x => x.Id == projectId);
            if (p is null) return Results.NotFound();
            if (!await access.CanRead(p)) return EndpointHelpers.Error("Sem acesso ao projeto.", 403);
        }
        var targetId = userId ?? access.UserId;
        var q = db.Activities.AsNoTracking().Where(x => x.ActivityDate >= start && x.ActivityDate <= end);
        if (projectId != null) q = q.Where(x => x.ProjectId == projectId);
        if (!access.IsAdmin && (projectId == null || !await IsLeader(db, projectId.Value, access.UserId)))
            q = q.Where(x => x.UserId == targetId);
        else if (userId != null) q = q.Where(x => x.UserId == userId);
        var data = await q.OrderByDescending(x => x.ActivityDate).ThenByDescending(x => x.CreatedAtUtc)
            .Take(500).ToListAsync();
        return Results.Ok(data.Select(x => new
        {
            x.Id, x.UserId, x.ProjectId, x.TaskId, x.ActivityDate, x.Category, x.Description,
            x.MinutesSpent, x.PartialResult, x.NoActivity, x.CreatedAtUtc, x.UpdatedAtUtc,
            isRetroactive = DateOnly.FromDateTime(x.CreatedAtUtc) > x.ActivityDate
        }));
    }

    private static async Task<IResult> MissingDays(DateOnly? from, DateOnly? to, Guid? userId,
        NexoDbContext db, AccessService access)
    {
        var target = userId ?? access.UserId;
        if (target != access.UserId && !access.IsAdmin) return EndpointHelpers.Error("Sem permissão.", 403);
        var end = to ?? DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1));
        var start = from ?? end.AddDays(-13);
        if (end < start || end.DayNumber - start.DayNumber > 90) return EndpointHelpers.Error("Intervalo inválido (máximo 90 dias).");
        var weekdays = await db.WorkSchedules.AsNoTracking().Where(x => x.UserId == target)
            .Select(x => x.Weekday).Distinct().ToListAsync();
        var recorded = await db.Activities.AsNoTracking().Where(x => x.UserId == target && x.ActivityDate >= start && x.ActivityDate <= end)
            .Select(x => x.ActivityDate).Distinct().ToListAsync();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var missing = Enumerable.Range(0, end.DayNumber - start.DayNumber + 1)
            .Select(offset => start.AddDays(offset))
            .Where(day => day < today && weekdays.Contains(day.DayOfWeek) && !recorded.Contains(day)).ToList();
        return Results.Ok(new { userId = target, from = start, to = end, missing,
            note = "Não considera feriados, férias nem ausências justificadas; não substitui controle de ponto." });
    }

    private static Task<bool> IsLeader(NexoDbContext db, Guid p, Guid user) =>
        db.Projects.AnyAsync(x => x.Id == p && x.LeaderId == user);

    private static string? Validate(DateOnly date, ActivityCategory category, string? desc, int minutes, string? result, bool noActivity)
    {
        if (date == default || date > DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1))) return "Data inválida ou futura.";
        if (!Enum.IsDefined(category) || minutes < 0 || minutes > 1440 || result?.Length > 2000) return "Categoria, tempo ou resultado inválido.";
        if (!noActivity && !EndpointHelpers.IsValid(desc, 4000, 3)) return "Descrição obrigatória (3 a 4000 caracteres).";
        if (noActivity && (minutes != 0 || (desc?.Length ?? 0) > 4000)) return "Registro sem atividades não pode conter tempo positivo.";
        return null;
    }
}
