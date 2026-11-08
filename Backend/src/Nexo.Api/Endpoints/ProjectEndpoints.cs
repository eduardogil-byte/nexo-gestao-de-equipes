using Microsoft.EntityFrameworkCore;
using Nexo.Api.Data;
using Nexo.Api.Domain;
using Nexo.Api.Infrastructure;

namespace Nexo.Api.Endpoints;

public static class ProjectEndpoints
{
    public sealed record SaveProject(string Name, string Description, string Objectives, string Justification,
        string ResearchArea, decimal? Budget, string ExpectedResults, DateOnly? PlannedStart,
        DateOnly? PlannedEnd, ProjectVisibility Visibility, Guid? LeaderId);
    public sealed record ChangeStatus(ProjectState Status);
    public sealed record AssignLeader(Guid UserId);
    public sealed record ProgressInput(int Percent);
    public sealed record AddMember(Guid UserId);

    public static void MapProjectEndpoints(this WebApplication app)
    {
        var g = app.MapGroup("/api/projects").RequireAuthorization().WithTags("Projetos e equipes");
        g.MapGet("", List);
        g.MapGet("/{id:guid}", Details);
        g.MapPost("", Create).RequireAuthorization("AdminOnly");
        g.MapPut("/{id:guid}", Update);
        g.MapPost("/{id:guid}/status", Status).RequireAuthorization("AdminOnly");
        g.MapPost("/{id:guid}/leader", Leader).RequireAuthorization("AdminOnly");
        g.MapPut("/{id:guid}/progress", Progress);
        g.MapGet("/{id:guid}/members", Members);
        g.MapPost("/{id:guid}/members", AddProjectMember).RequireAuthorization("AdminOnly");
        g.MapDelete("/{id:guid}/members/{userId:guid}", RemoveProjectMember).RequireAuthorization("AdminOnly");
        g.MapGet("/{id:guid}/audit", Audit);
    }

    private static async Task<IResult> List(NexoDbContext db, AccessService access)
    {
        var q = db.Projects.AsNoTracking();
        if (!access.IsAdmin)
        {
            var userId = access.UserId;
            q = q.Where(p => p.Visibility == ProjectVisibility.PublicInsideNucleus ||
                db.ProjectMembers.Any(m => m.ProjectId == p.Id && m.UserId == userId && m.IsActive));
        }
        var result = await q.OrderByDescending(x => x.UpdatedAtUtc)
            .Select(x => new
            {
                x.Id, x.Name, x.Description, x.ResearchArea, x.Status, x.Visibility,
                x.LeaderId, x.LeaderProgressPercent, x.PlannedStart, x.PlannedEnd, x.UpdatedAtUtc
            }).ToListAsync();
        return Results.Ok(result);
    }

    private static async Task<IResult> Details(Guid id, NexoDbContext db, AccessService access)
    {
        var p = await db.Projects.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (p is null) return Results.NotFound();
        if (!await access.CanRead(p)) return EndpointHelpers.Error("Sem acesso ao projeto.", 403);
        return Results.Ok(new
        {
            p.Id, p.Name, p.Description, p.Objectives, p.Justification, p.ResearchArea,
            p.Budget, p.ExpectedResults, p.PlannedStart, p.PlannedEnd, p.LeaderId,
            p.Status, p.Visibility, p.LeaderProgressPercent, p.CreatedAtUtc, p.UpdatedAtUtc,
            p.StartedAtUtc,
            CollaborationCode = access.IsAdmin || p.LeaderId == access.UserId ? p.CollaborationCode : null
        });
    }

    private static async Task<IResult> Create(SaveProject req, NexoDbContext db, AccessService access, DomainEvents events)
    {
        var error = Validate(req);
        if (error is not null) return EndpointHelpers.Error(error);
        if (req.LeaderId is null || !await db.Users.AnyAsync(u => u.Id == req.LeaderId && u.Approval == ApprovalState.Approved))
            return EndpointHelpers.Error("Informe um líder com conta aprovada.");
        var p = new ResearchProject();
        Fill(p, req);
        p.LeaderId = req.LeaderId.Value;
        db.Projects.Add(p);
        db.ProjectMembers.Add(new ProjectMember { ProjectId = p.Id, UserId = p.LeaderId });
        events.Audit("Project", p.Id, "Created", p.Id, new { p.Name, p.LeaderId });
        if (p.LeaderId != access.UserId) events.Notify(p.LeaderId, "ProjectLeader", $"Você foi designado líder de {p.Name}.", p.Id);
        await db.SaveChangesAsync();
        return Results.Created($"/api/projects/{p.Id}", new { p.Id, p.Name, p.Status });
    }

    private static async Task<IResult> Update(Guid id, SaveProject req, NexoDbContext db, AccessService access, DomainEvents events)
    {
        var p = await db.Projects.FirstOrDefaultAsync(x => x.Id == id);
        if (p is null) return Results.NotFound();
        if (!await access.CanLead(p)) return EndpointHelpers.Error("Apenas líder ou coordenação pode editar.", 403);
        if (!EndpointHelpers.IsEditable(p)) return EndpointHelpers.Error("Reabra o projeto antes de editar.", 409);
        var error = Validate(req);
        if (error is not null) return EndpointHelpers.Error(error);
        if (req.LeaderId != null && req.LeaderId != p.LeaderId)
            return EndpointHelpers.Error("Troque o líder pelo endpoint específico, disponível à coordenação.");
        var before = new { p.Name, p.Description, p.Objectives, p.Justification, p.ResearchArea,
            p.Budget, p.ExpectedResults, p.PlannedStart, p.PlannedEnd, p.Visibility };
        Fill(p, req);
        p.UpdatedAtUtc = DateTime.UtcNow;
        events.Audit("Project", id, "Edited", id, new { before, after = req });
        await events.BeginOnMemberWork(p, "ProjectEdited");
        await db.SaveChangesAsync();
        return Results.NoContent();
    }

    private static async Task<IResult> Status(Guid id, ChangeStatus req, NexoDbContext db, DomainEvents events)
    {
        var p = await db.Projects.FirstOrDefaultAsync(x => x.Id == id);
        if (p is null) return Results.NotFound();
        if (!Enum.IsDefined(req.Status)) return EndpointHelpers.Error("Estado inválido.");
        var previous = p.Status;
        p.Status = req.Status; p.UpdatedAtUtc = DateTime.UtcNow;
        if (req.Status == ProjectState.InProgress && p.StartedAtUtc is null) p.StartedAtUtc = DateTime.UtcNow;
        events.Audit("Project", id, "StatusChanged", id, new { previous, current = req.Status });
        await db.SaveChangesAsync();
        return Results.NoContent();
    }

    private static async Task<IResult> Leader(Guid id, AssignLeader req, NexoDbContext db, DomainEvents events)
    {
        var p = await db.Projects.FirstOrDefaultAsync(x => x.Id == id);
        if (p is null) return Results.NotFound();
        if (!await db.ProjectMembers.AnyAsync(m => m.ProjectId == id && m.UserId == req.UserId && m.IsActive))
            return EndpointHelpers.Error("Novo líder precisa ser integrante ativo do projeto.");
        var before = p.LeaderId;
        p.LeaderId = req.UserId; p.UpdatedAtUtc = DateTime.UtcNow;
        events.Audit("Project", id, "LeaderChanged", id, new { before, after = req.UserId });
        events.Notify(req.UserId, "ProjectLeader", $"Você é o novo líder de {p.Name}.", id);
        await db.SaveChangesAsync();
        return Results.NoContent();
    }

    private static async Task<IResult> Progress(Guid id, ProgressInput req, NexoDbContext db, AccessService access, DomainEvents events)
    {
        var p = await db.Projects.FirstOrDefaultAsync(x => x.Id == id);
        if (p is null) return Results.NotFound();
        if (!await access.CanLead(p)) return EndpointHelpers.Error("Somente líder ou coordenação.", 403);
        if (!EndpointHelpers.IsEditable(p)) return EndpointHelpers.Error("Projeto encerrado.", 409);
        if (req.Percent is < 0 or > 100) return EndpointHelpers.Error("Progresso deve estar entre 0 e 100.");
        var before = p.LeaderProgressPercent;
        p.LeaderProgressPercent = req.Percent; p.UpdatedAtUtc = DateTime.UtcNow;
        events.Audit("Project", id, "ProgressUpdated", id, new { before, after = req.Percent });
        await events.BeginOnMemberWork(p, "ProgressUpdated");
        await db.SaveChangesAsync();
        return Results.NoContent();
    }

    private static async Task<IResult> Members(Guid id, NexoDbContext db, AccessService access)
    {
        var p = await db.Projects.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (p is null) return Results.NotFound();
        if (!await access.CanWork(p)) return EndpointHelpers.Error("Lista de integrantes restrita à equipe e coordenação.", 403);
        var q = from m in db.ProjectMembers.AsNoTracking()
                join u in db.Users.AsNoTracking() on m.UserId equals u.Id
                where m.ProjectId == id && m.IsActive
                select new { u.Id, u.FullName, u.Specialty, isLeader = u.Id == p.LeaderId, m.JoinedAtUtc };
        return Results.Ok(await q.ToListAsync());
    }

    private static async Task<IResult> AddProjectMember(Guid id, AddMember req, NexoDbContext db, DomainEvents events)
    {
        var p = await db.Projects.FirstOrDefaultAsync(x => x.Id == id);
        if (p is null) return Results.NotFound();
        if (!EndpointHelpers.IsEditable(p)) return EndpointHelpers.Error("Projeto encerrado.", 409);
        if (!await db.Users.AnyAsync(u => u.Id == req.UserId && u.Approval == ApprovalState.Approved))
            return EndpointHelpers.Error("Usuário não encontrado ou não aprovado.");
        var member = await db.ProjectMembers.FirstOrDefaultAsync(x => x.ProjectId == id && x.UserId == req.UserId);
        if (member?.IsActive == true) return EndpointHelpers.Error("Usuário já está na equipe.", 409);
        if (member is null) db.ProjectMembers.Add(new ProjectMember { ProjectId = id, UserId = req.UserId });
        else { member.IsActive = true; member.JoinedAtUtc = DateTime.UtcNow; member.LeftAtUtc = null; }
        events.Audit("ProjectMember", req.UserId, "AddedToProject", id);
        events.Notify(req.UserId, "ProjectJoined", $"Você foi incluído no projeto {p.Name}.", id);
        await db.SaveChangesAsync();
        return Results.NoContent();
    }

    private static async Task<IResult> RemoveProjectMember(Guid id, Guid userId, NexoDbContext db, DomainEvents events)
    {
        var p = await db.Projects.FirstOrDefaultAsync(x => x.Id == id);
        if (p is null) return Results.NotFound();
        if (p.LeaderId == userId) return EndpointHelpers.Error("Defina outro líder antes de remover o líder atual.");
        var m = await db.ProjectMembers.FirstOrDefaultAsync(x => x.ProjectId == id && x.UserId == userId && x.IsActive);
        if (m is null) return Results.NotFound();
        m.IsActive = false; m.LeftAtUtc = DateTime.UtcNow;
        var assignments = await (from a in db.TaskAssignees
            join t in db.Tasks on a.TaskId equals t.Id
            where t.ProjectId == id && a.UserId == userId && a.IsActive && t.Status != WorkState.Completed
                && t.Status != WorkState.Cancelled
            select new { a, t }).ToListAsync();
        foreach (var entry in assignments)
        {
            entry.a.IsActive = false;
            entry.t.NeedsRedistribution = true;
        }
        events.Audit("ProjectMember", userId, "RemovedFromProject", id,
            new { tasksAwaitingRedistribution = assignments.Count });
        await db.SaveChangesAsync();
        return Results.NoContent();
    }

    private static async Task<IResult> Audit(Guid id, NexoDbContext db, AccessService access)
    {
        var p = await db.Projects.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (p is null) return Results.NotFound();
        if (!access.IsAdmin && p.LeaderId != access.UserId) return EndpointHelpers.Error("Apenas líder ou coordenação pode consultar auditoria detalhada.", 403);
        return Results.Ok(await db.AuditEvents.AsNoTracking().Where(x => x.ProjectId == id)
            .OrderByDescending(x => x.AtUtc).Take(200).ToListAsync());
    }

    private static string? Validate(SaveProject req)
    {
        if (!EndpointHelpers.IsValid(req.Name, 180, 3) || req.Description?.Length > 4000 ||
            req.Objectives?.Length > 4000 || req.Justification?.Length > 4000 ||
            req.ResearchArea?.Length > 150 || req.ExpectedResults?.Length > 4000)
            return "Nome ou textos do projeto inválidos.";
        if (req.Budget < 0 || req.PlannedStart > req.PlannedEnd || !Enum.IsDefined(req.Visibility))
            return "Orçamento, datas ou visibilidade inválidos.";
        return null;
    }

    private static void Fill(ResearchProject p, SaveProject req)
    {
        p.Name = req.Name.Trim(); p.Description = req.Description?.Trim() ?? "";
        p.Objectives = req.Objectives?.Trim() ?? "";
        p.Justification = req.Justification?.Trim() ?? "";
        p.ResearchArea = req.ResearchArea?.Trim() ?? "";
        p.ExpectedResults = req.ExpectedResults?.Trim() ?? "";
        p.Budget = req.Budget; p.PlannedStart = req.PlannedStart; p.PlannedEnd = req.PlannedEnd;
        p.Visibility = req.Visibility;
    }
}
