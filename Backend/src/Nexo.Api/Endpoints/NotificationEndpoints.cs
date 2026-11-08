using Microsoft.EntityFrameworkCore;
using Nexo.Api.Data;
using Nexo.Api.Infrastructure;

namespace Nexo.Api.Endpoints;

public static class NotificationEndpoints
{
    public static void MapNotificationEndpoints(this WebApplication app)
    {
        var g = app.MapGroup("/api/notifications").RequireAuthorization().WithTags("Notificações");
        g.MapGet("", List);
        g.MapPost("/{id:guid}/read", Read);
        g.MapPost("/read-all", ReadAll);
    }

    private static async Task<IResult> List(int? limit, NexoDbContext db, AccessService access) =>
        Results.Ok(await db.Notifications.AsNoTracking().Where(n => n.UserId == access.UserId)
            .OrderByDescending(n => n.CreatedAtUtc).Take(Math.Clamp(limit ?? 50, 1, 100))
            .Select(n => new { n.Id, n.Kind, n.Message, n.ProjectId, n.TaskId, n.CreatedAtUtc, n.ReadAtUtc }).ToListAsync());

    private static async Task<IResult> Read(Guid id, NexoDbContext db, AccessService access)
    {
        var n = await db.Notifications.FirstOrDefaultAsync(x => x.Id == id && x.UserId == access.UserId);
        if (n is null) return Results.NotFound();
        n.ReadAtUtc ??= DateTime.UtcNow;
        await db.SaveChangesAsync();
        return Results.NoContent();
    }

    private static async Task<IResult> ReadAll(NexoDbContext db, AccessService access)
    {
        var unread = await db.Notifications.Where(x => x.UserId == access.UserId && x.ReadAtUtc == null).ToListAsync();
        foreach (var n in unread) n.ReadAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return Results.Ok(new { markedAsRead = unread.Count });
    }
}
