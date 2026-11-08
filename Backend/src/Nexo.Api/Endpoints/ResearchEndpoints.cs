using Microsoft.EntityFrameworkCore;
using Nexo.Api.Data;
using Nexo.Api.Domain;
using Nexo.Api.Infrastructure;

namespace Nexo.Api.Endpoints;

public static class ResearchEndpoints
{
    public sealed record MilestoneInput(string Title, string? Description, DateOnly? DueDate);
    public sealed record MilestoneStatus(WorkState Status);
    public sealed record DeliverableInput(string Title, string Kind, string? Description, string? ExternalUrl);
    public sealed record ObstacleInput(string Description, Guid? TaskId);
    public sealed record ResolveObstacle(string Resolution);
    public sealed record ContributionInput(Guid? ProjectId, string? CollaborationCode, DateOnly ActivityDate,
        string Description, int MinutesSpent);
    public sealed record ReviewContribution(ContributionState Status);

    public static void MapResearchEndpoints(this WebApplication app)
    {
        var p = app.MapGroup("/api/projects/{projectId:guid}").RequireAuthorization().WithTags("Pesquisa e entregas");
        p.MapGet("/milestones", ListMilestones);
        p.MapPost("/milestones", CreateMilestone);
        p.MapGet("/deliverables", ListDeliverables);
        p.MapPost("/deliverables", CreateDeliverable);
        p.MapGet("/obstacles", ListObstacles);
        p.MapPost("/obstacles", CreateObstacle);
        p.MapGet("/contributions", ListContributions);
        var m = app.MapGroup("/api/milestones").RequireAuthorization().WithTags("Pesquisa e entregas");
        m.MapPut("/{id:guid}/status", UpdateMilestone);
        var d = app.MapGroup("/api/deliverables").RequireAuthorization().WithTags("Pesquisa e entregas");
        d.MapPost("/{id:guid}/complete", CompleteDeliverable);
        var o = app.MapGroup("/api/obstacles").RequireAuthorization().WithTags("Obstáculos");
        o.MapPost("/{id:guid}/resolve", MarkObstacleResolved);
        var c = app.MapGroup("/api/contributions").RequireAuthorization().WithTags("Colaborações externas");
        c.MapPost("", SubmitContribution);
        c.MapGet("/mine", MyContributions);
        c.MapPost("/{id:guid}/review", Review);
    }

    private static async Task<(ResearchProject? Project, IResult? Error)> GetProject(Guid id, NexoDbContext db, AccessService access, bool writable = false)
    {
        var p = await db.Projects.FirstOrDefaultAsync(x => x.Id == id);
        if (p is null) return (null, Results.NotFound());
        if (!await (writable ? access.CanWork(p) : access.CanRead(p)))
            return (null, EndpointHelpers.Error("Sem acesso ao projeto.", 403));
        if (writable && !EndpointHelpers.IsEditable(p)) return (null, EndpointHelpers.Error("Projeto encerrado.", 409));
        return (p, null);
    }

    private static async Task<IResult> ListMilestones(Guid projectId, NexoDbContext db, AccessService access)
    {
        var (p, err) = await GetProject(projectId, db, access);
        if (err is not null) return err;
        return Results.Ok(await db.Milestones.AsNoTracking().Where(x => x.ProjectId == p!.Id)
            .OrderBy(x => x.CreatedAtUtc).ToListAsync());
    }

    private static async Task<IResult> CreateMilestone(Guid projectId, MilestoneInput req, NexoDbContext db,
        AccessService access, DomainEvents events)
    {
        var (p, err) = await GetProject(projectId, db, access, true);
        if (err is not null) return err;
        if (!await access.CanLead(p!)) return EndpointHelpers.Error("Somente líder/coordenação.", 403);
        if (!EndpointHelpers.IsValid(req.Title, 200, 3) || req.Description?.Length > 2000)
            return EndpointHelpers.Error("Dados inválidos.");
        var milestone = new ProjectMilestone
        { ProjectId = p!.Id, Title = req.Title.Trim(), Description = req.Description?.Trim() ?? "", DueDate = req.DueDate };
        db.Milestones.Add(milestone);
        events.Audit("Milestone", milestone.Id, "Created", p.Id);
        await events.BeginOnMemberWork(p, "MilestoneCreated");
        await db.SaveChangesAsync();
        return Results.Created($"/api/projects/{projectId}/milestones", milestone);
    }

    private static async Task<IResult> UpdateMilestone(Guid id, MilestoneStatus req, NexoDbContext db,
        AccessService access, DomainEvents events)
    {
        var milestone = await db.Milestones.FirstOrDefaultAsync(x => x.Id == id);
        if (milestone is null) return Results.NotFound();
        var (p, err) = await GetProject(milestone.ProjectId, db, access, true);
        if (err is not null) return err;
        if (!await access.CanLead(p!)) return EndpointHelpers.Error("Somente líder/coordenação.", 403);
        if (!Enum.IsDefined(req.Status)) return EndpointHelpers.Error("Estado inválido.");
        var before = milestone.Status; milestone.Status = req.Status;
        events.Audit("Milestone", id, "StatusChanged", p!.Id, new { before, after = req.Status });
        await events.BeginOnMemberWork(p, "MilestoneUpdated");
        await db.SaveChangesAsync();
        return Results.NoContent();
    }

    private static async Task<IResult> ListDeliverables(Guid projectId, NexoDbContext db, AccessService access)
    {
        var (p, err) = await GetProject(projectId, db, access);
        if (err is not null) return err;
        return Results.Ok(await db.Deliverables.AsNoTracking().Where(x => x.ProjectId == p!.Id)
            .OrderByDescending(x => x.CreatedAtUtc).ToListAsync());
    }

    private static async Task<IResult> CreateDeliverable(Guid projectId, DeliverableInput req,
        NexoDbContext db, AccessService access, DomainEvents events)
    {
        var (p, err) = await GetProject(projectId, db, access, true);
        if (err is not null) return err;
        if (!EndpointHelpers.IsValid(req.Title, 200, 3) || !EndpointHelpers.IsValid(req.Kind, 80) ||
            req.Description?.Length > 3000 || !EndpointHelpers.IsHttpLink(req.ExternalUrl))
            return EndpointHelpers.Error("Título, tipo ou link inválido.");
        var deliverable = new ProjectDeliverable
        { ProjectId = p!.Id, Title = req.Title.Trim(), Kind = req.Kind.Trim(), Description = req.Description?.Trim(),
          ExternalUrl = req.ExternalUrl?.Trim() };
        db.Deliverables.Add(deliverable);
        events.Audit("Deliverable", deliverable.Id, "Created", p.Id);
        await events.BeginOnMemberWork(p, "DeliverableCreated");
        await db.SaveChangesAsync();
        return Results.Created($"/api/projects/{projectId}/deliverables", deliverable);
    }

    private static async Task<IResult> CompleteDeliverable(Guid id, NexoDbContext db, AccessService access, DomainEvents events)
    {
        var delivery = await db.Deliverables.FirstOrDefaultAsync(x => x.Id == id);
        if (delivery is null) return Results.NotFound();
        var (p, err) = await GetProject(delivery.ProjectId, db, access, true);
        if (err is not null) return err;
        delivery.Status = WorkState.Completed; delivery.CompletedAtUtc = DateTime.UtcNow;
        events.Audit("Deliverable", id, "Completed", p!.Id);
        await events.BeginOnMemberWork(p, "DeliverableCompleted");
        await db.SaveChangesAsync();
        return Results.NoContent();
    }

    private static async Task<IResult> ListObstacles(Guid projectId, NexoDbContext db, AccessService access)
    {
        var (p, err) = await GetProject(projectId, db, access);
        if (err is not null) return err;
        if (!await access.CanWork(p!)) return EndpointHelpers.Error("Apenas integrantes podem ver obstáculos.", 403);
        return Results.Ok(await db.Obstacles.AsNoTracking().Where(x => x.ProjectId == p!.Id)
            .OrderByDescending(x => x.ReportedAtUtc).ToListAsync());
    }

    private static async Task<IResult> CreateObstacle(Guid projectId, ObstacleInput req, NexoDbContext db,
        AccessService access, DomainEvents events)
    {
        var (p, err) = await GetProject(projectId, db, access, true);
        if (err is not null) return err;
        if (!EndpointHelpers.IsValid(req.Description, 4000, 5)) return EndpointHelpers.Error("Descreva o obstáculo.");
        if (req.TaskId != null && !await db.Tasks.AnyAsync(t => t.Id == req.TaskId && t.ProjectId == projectId))
            return EndpointHelpers.Error("Tarefa não pertence ao projeto.");
        var obstacle = new ProjectObstacle
        { ProjectId = projectId, TaskId = req.TaskId, ReportedById = access.UserId, Description = req.Description.Trim() };
        db.Obstacles.Add(obstacle);
        events.Audit("Obstacle", obstacle.Id, "Reported", projectId);
        await events.NotifyLeader(p!, "ObstacleReported", "Novo obstáculo registrado no projeto.", req.TaskId);
        await events.BeginOnMemberWork(p!, "ObstacleReported");
        await db.SaveChangesAsync();
        return Results.Created($"/api/projects/{projectId}/obstacles", new { obstacle.Id, obstacle.Status });
    }

    private static async Task<IResult> MarkObstacleResolved(Guid id, ResolveObstacle req, NexoDbContext db,
        AccessService access, DomainEvents events)
    {
        var obstacle = await db.Obstacles.FirstOrDefaultAsync(x => x.Id == id);
        if (obstacle is null) return Results.NotFound();
        var (p, err) = await GetProject(obstacle.ProjectId, db, access, true);
        if (err is not null) return err;
        if (obstacle.ReportedById != access.UserId && !await access.CanLead(p!))
            return EndpointHelpers.Error("Apenas autor, líder ou coordenação pode resolver.", 403);
        if (!EndpointHelpers.IsValid(req.Resolution, 4000, 3)) return EndpointHelpers.Error("Descreva a solução.");
        obstacle.Status = ObstacleState.Resolved; obstacle.Resolution = req.Resolution.Trim();
        obstacle.ResolvedAtUtc = DateTime.UtcNow;
        events.Audit("Obstacle", id, "Resolved", p!.Id, new { req.Resolution });
        await events.BeginOnMemberWork(p, "ObstacleResolved");
        await db.SaveChangesAsync();
        return Results.NoContent();
    }

    private static async Task<IResult> SubmitContribution(ContributionInput req, NexoDbContext db,
        AccessService access, DomainEvents events)
    {
        if (!EndpointHelpers.IsValid(req.Description, 3000, 5) || req.MinutesSpent < 0 || req.MinutesSpent > 1440 ||
            req.ActivityDate == default || req.ActivityDate > DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)))
            return EndpointHelpers.Error("Descrição, data ou duração inválida.");
        ResearchProject? p = null;
        if (!string.IsNullOrWhiteSpace(req.CollaborationCode))
            p = await db.Projects.FirstOrDefaultAsync(x => x.CollaborationCode == req.CollaborationCode.Trim());
        else if (req.ProjectId.HasValue)
            p = await db.Projects.FirstOrDefaultAsync(x => x.Id == req.ProjectId && x.Visibility == ProjectVisibility.PublicInsideNucleus);
        if (p is null) return EndpointHelpers.Error("Projeto não encontrado ou código de colaboração inválido.", 404);
        if (!EndpointHelpers.IsEditable(p)) return EndpointHelpers.Error("Projeto encerrado.", 409);
        if (await access.IsMember(p.Id)) return EndpointHelpers.Error("Você faz parte da equipe; registre esta atividade normalmente.");
        var contribution = new ProjectContribution
        { ProjectId = p.Id, UserId = access.UserId, Description = req.Description.Trim(),
          ActivityDate = req.ActivityDate, MinutesSpent = req.MinutesSpent };
        db.Contributions.Add(contribution);
        events.Audit("Contribution", contribution.Id, "Submitted", p.Id);
        events.Notify(p.LeaderId, "ContributionPending", "Uma contribuição externa aguarda confirmação.", p.Id);
        await db.SaveChangesAsync();
        return Results.Created($"/api/contributions/mine", new { contribution.Id, contribution.Status });
    }

    private static async Task<IResult> MyContributions(NexoDbContext db, AccessService access) =>
        Results.Ok(await db.Contributions.AsNoTracking().Where(x => x.UserId == access.UserId)
            .OrderByDescending(x => x.CreatedAtUtc).Take(200).ToListAsync());

    private static async Task<IResult> ListContributions(Guid projectId, NexoDbContext db, AccessService access)
    {
        var p = await db.Projects.AsNoTracking().FirstOrDefaultAsync(x => x.Id == projectId);
        if (p is null) return Results.NotFound();
        if (!await access.CanLead(p)) return EndpointHelpers.Error("Somente líder/coordenação.", 403);
        return Results.Ok(await db.Contributions.AsNoTracking().Where(x => x.ProjectId == projectId)
            .OrderByDescending(x => x.CreatedAtUtc).Take(300).ToListAsync());
    }

    private static async Task<IResult> Review(Guid id, ReviewContribution req, NexoDbContext db,
        AccessService access, DomainEvents events)
    {
        var c = await db.Contributions.FirstOrDefaultAsync(x => x.Id == id);
        if (c is null) return Results.NotFound();
        var p = await db.Projects.FirstAsync(x => x.Id == c.ProjectId);
        if (!await access.CanLead(p)) return EndpointHelpers.Error("Somente líder/coordenação.", 403);
        if (c.Status != ContributionState.Pending) return EndpointHelpers.Error("Colaboração já avaliada.", 409);
        if (req.Status is not (ContributionState.Approved or ContributionState.Rejected))
            return EndpointHelpers.Error("Informe Approved ou Rejected.");
        c.Status = req.Status; c.ReviewedById = access.UserId; c.ReviewedAtUtc = DateTime.UtcNow;
        events.Audit("Contribution", id, "Reviewed", p.Id, new { req.Status });
        events.Notify(c.UserId, "ContributionReviewed", req.Status == ContributionState.Approved
            ? "Sua contribuição foi confirmada pelo líder." : "Sua contribuição não foi confirmada.", p.Id);
        await events.BeginOnMemberWork(p, "ContributionReviewed");
        await db.SaveChangesAsync();
        return Results.NoContent();
    }
}
