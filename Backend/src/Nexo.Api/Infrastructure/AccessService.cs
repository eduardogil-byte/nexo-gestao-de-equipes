using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Nexo.Api.Data;
using Nexo.Api.Domain;

namespace Nexo.Api.Infrastructure;

public sealed class AccessService(IHttpContextAccessor http, NexoDbContext db)
{
    public Guid UserId => Guid.TryParse(http.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : Guid.Empty;
    public bool IsAdmin => http.HttpContext?.User.IsInRole("Admin") == true;
    public Task<bool> IsMember(Guid projectId) => db.ProjectMembers.AnyAsync(x => x.ProjectId == projectId && x.UserId == UserId && x.IsActive);
    public Task<bool> CanRead(ResearchProject project) =>
        IsAdmin || project.Visibility == ProjectVisibility.PublicInsideNucleus
            ? Task.FromResult(true) : IsMember(project.Id);
    public async Task<bool> CanWork(ResearchProject project) => IsAdmin || await IsMember(project.Id);
    public Task<bool> CanLead(ResearchProject project) => Task.FromResult(IsAdmin || project.LeaderId == UserId);
    public async Task<bool> CanManageTask(ProjectTask task, ResearchProject project) =>
        IsAdmin || project.LeaderId == UserId ||
        await db.TaskAssignees.AnyAsync(a => a.TaskId == task.Id && a.UserId == UserId && a.Accepted);
    public async Task<bool> IsActiveProjectMember(Guid projectId, Guid userId) =>
        await db.ProjectMembers.AnyAsync(m => m.ProjectId == projectId && m.UserId == userId && m.IsActive);
}
