using System.IdentityModel.Tokens.Jwt;
using System.Net.Mail;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Nexo.Api.Data;
using Nexo.Api.Domain;
using Nexo.Api.Infrastructure;

namespace Nexo.Api.Endpoints;

public static class AuthEndpoints
{
    public sealed record RegisterRequest(string FullName, string Email, string Password, string? Specialty);
    public sealed record LoginRequest(string Email, string Password);
    public sealed record ProfileRequest(string FullName, string? Specialty);
    public sealed record ScheduleItem(DayOfWeek Weekday, Shift Shift);
    public sealed record ScheduleRequest(List<ScheduleItem> Items);

    public static void MapAuthEndpoints(this WebApplication app)
    {
        var auth = app.MapGroup("/api/auth").WithTags("Autenticação");
        auth.MapPost("/register", Register).AllowAnonymous().RequireRateLimiting("auth");
        auth.MapPost("/login", Login).AllowAnonymous().RequireRateLimiting("auth");
        auth.MapGet("/me", Me).RequireAuthorization();
        auth.MapPut("/me", UpdateMe).RequireAuthorization();
        auth.MapGet("/me/schedule", GetSchedule).RequireAuthorization();
        auth.MapPut("/me/schedule", UpdateSchedule).RequireAuthorization();

        var users = app.MapGroup("/api/users").RequireAuthorization("AdminOnly").WithTags("Usuários");
        users.MapGet("", AllUsers);
        users.MapPost("/{id:guid}/approve", Approve);
        users.MapPost("/{id:guid}/reject", Reject);
        users.MapPut("/{id:guid}", AdminEdit);
        users.MapGet("/{id:guid}/schedule", AdminGetSchedule);
        users.MapPut("/{id:guid}/schedule", AdminSetSchedule);
    }

    private static async Task<IResult> Register(RegisterRequest input, UserManager<AppUser> mgr)
    {
        if (!EndpointHelpers.IsValid(input.FullName, 150, 3) || !EmailIsValid(input.Email) ||
            string.IsNullOrWhiteSpace(input.Password) || input.Specialty?.Length > 120)
            return EndpointHelpers.Error("Nome, e-mail ou especialidade inválidos.");
        var user = new AppUser
        {
            UserName = input.Email.Trim().ToLowerInvariant(),
            Email = input.Email.Trim().ToLowerInvariant(),
            FullName = input.FullName.Trim(),
            Specialty = input.Specialty?.Trim(), Approval = ApprovalState.Pending
        };
        var result = await mgr.CreateAsync(user, input.Password);
        return result.Succeeded
            ? Results.Created($"/api/auth/me", new { user.Id, message = "Cadastro recebido. Aguarde aprovação da coordenação." })
            : EndpointHelpers.Error(string.Join("; ", result.Errors.Select(x => x.Description)));
    }

    private static async Task<IResult> Login(LoginRequest input, UserManager<AppUser> mgr, IConfiguration cfg)
    {
        if (string.IsNullOrWhiteSpace(input.Email) || string.IsNullOrEmpty(input.Password))
            return EndpointHelpers.Error("Credenciais inválidas.", 401);
        var user = await mgr.FindByEmailAsync(input.Email.Trim());
        if (user is null || await mgr.IsLockedOutAsync(user)) return EndpointHelpers.Error("Credenciais inválidas ou conta temporariamente bloqueada.", 401);
        if (!await mgr.CheckPasswordAsync(user, input.Password))
        {
            await mgr.AccessFailedAsync(user);
            return EndpointHelpers.Error("Credenciais inválidas.", 401);
        }
        if (user.Approval != ApprovalState.Approved)
            return EndpointHelpers.Error("Conta aguardando aprovação ou não autorizada.", 403);
        await mgr.ResetAccessFailedCountAsync(user);
        var hours = Math.Clamp(cfg.GetValue("Jwt:ExpirationHours", 8), 1, 24);
        var expires = DateTime.UtcNow.AddHours(hours);
        var roles = await mgr.GetRolesAsync(user);
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.FullName),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };
        claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(cfg["Jwt:Key"]!));
        var token = new JwtSecurityToken(cfg["Jwt:Issuer"], cfg["Jwt:Audience"], claims,
            expires: expires, signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));
        return Results.Ok(new
        {
            accessToken = new JwtSecurityTokenHandler().WriteToken(token),
            tokenType = "Bearer", expiresAtUtc = expires,
            user = new { user.Id, user.FullName, user.Email, user.Approval, roles }
        });
    }

    private static async Task<IResult> Me(UserManager<AppUser> mgr, AccessService access)
    {
        var user = await mgr.FindByIdAsync(access.UserId.ToString());
        if (user is null) return Results.NotFound();
        return Results.Ok(new { user.Id, user.FullName, user.Email, user.Specialty, user.Approval, roles = await mgr.GetRolesAsync(user) });
    }

    private static async Task<IResult> UpdateMe(ProfileRequest req, UserManager<AppUser> mgr, AccessService access)
    {
        if (!EndpointHelpers.IsValid(req.FullName, 150, 3) || req.Specialty?.Length > 120)
            return EndpointHelpers.Error("Dados inválidos.");
        var user = await mgr.FindByIdAsync(access.UserId.ToString());
        if (user is null) return Results.NotFound();
        user.FullName = req.FullName.Trim(); user.Specialty = req.Specialty?.Trim();
        var result = await mgr.UpdateAsync(user);
        return result.Succeeded ? Results.NoContent() : EndpointHelpers.Error("Não foi possível atualizar o perfil.");
    }

    private static async Task<IResult> AllUsers(NexoDbContext db)
    {
        var users = await db.Users.AsNoTracking().OrderBy(x => x.FullName)
            .Select(x => new { x.Id, x.FullName, x.Email, x.Specialty, x.Approval, x.CreatedAtUtc }).ToListAsync();
        return Results.Ok(users);
    }

    private static async Task<IResult> Approve(Guid id, UserManager<AppUser> mgr, AccessService access, DomainEvents events, NexoDbContext db)
    {
        var user = await mgr.FindByIdAsync(id.ToString());
        if (user is null) return Results.NotFound();
        user.Approval = ApprovalState.Approved;
        var r = await mgr.UpdateAsync(user);
        if (!r.Succeeded) return EndpointHelpers.Error("Não foi possível aprovar.");
        if (!await mgr.IsInRoleAsync(user, "Admin") && !await mgr.IsInRoleAsync(user, "Member"))
            await mgr.AddToRoleAsync(user, "Member");
        events.Audit("User", id, "Approved");
        await db.SaveChangesAsync();
        return Results.NoContent();
    }

    private static async Task<IResult> Reject(Guid id, UserManager<AppUser> mgr, AccessService access, DomainEvents events, NexoDbContext db)
    {
        if (id == access.UserId) return EndpointHelpers.Error("Você não pode rejeitar sua própria conta.");
        var user = await mgr.FindByIdAsync(id.ToString());
        if (user is null) return Results.NotFound();
        if (await mgr.IsInRoleAsync(user, "Admin")) return EndpointHelpers.Error("Não é permitido rejeitar um administrador.", 403);
        user.Approval = ApprovalState.Rejected;
        var r = await mgr.UpdateAsync(user);
        if (!r.Succeeded) return EndpointHelpers.Error("Não foi possível rejeitar.");
        events.Audit("User", id, "Rejected");
        await db.SaveChangesAsync();
        return Results.NoContent();
    }

    private static async Task<IResult> AdminEdit(Guid id, ProfileRequest req, UserManager<AppUser> mgr, DomainEvents events, NexoDbContext db)
    {
        if (!EndpointHelpers.IsValid(req.FullName, 150, 3) || req.Specialty?.Length > 120)
            return EndpointHelpers.Error("Dados inválidos.");
        var user = await mgr.FindByIdAsync(id.ToString());
        if (user is null) return Results.NotFound();
        var previous = new { user.FullName, user.Specialty };
        user.FullName = req.FullName.Trim(); user.Specialty = req.Specialty?.Trim();
        var result = await mgr.UpdateAsync(user);
        if (!result.Succeeded) return EndpointHelpers.Error("Falha ao editar usuário.");
        events.Audit("User", id, "ProfileEdited", details: new { previous, current = req });
        await db.SaveChangesAsync();
        return Results.NoContent();
    }

    private static async Task<IResult> GetSchedule(AccessService access, NexoDbContext db) =>
        Results.Ok(await db.WorkSchedules.AsNoTracking().Where(x => x.UserId == access.UserId)
            .OrderBy(x => x.Weekday).ThenBy(x => x.Shift).Select(x => new { x.Weekday, x.Shift }).ToListAsync());

    private static async Task<IResult> UpdateSchedule(ScheduleRequest req, AccessService access, NexoDbContext db, DomainEvents events)
        => await SetSchedule(access.UserId, req, db, events);

    private static async Task<IResult> AdminGetSchedule(Guid id, NexoDbContext db) =>
        Results.Ok(await db.WorkSchedules.AsNoTracking().Where(x => x.UserId == id)
            .OrderBy(x => x.Weekday).ThenBy(x => x.Shift).Select(x => new { x.Weekday, x.Shift }).ToListAsync());

    private static async Task<IResult> AdminSetSchedule(Guid id, ScheduleRequest req, NexoDbContext db, DomainEvents events) =>
        await SetSchedule(id, req, db, events);

    private static async Task<IResult> SetSchedule(Guid userId, ScheduleRequest req, NexoDbContext db, DomainEvents events)
    {
        if (!await db.Users.AnyAsync(x => x.Id == userId)) return Results.NotFound();
        if (req.Items is null || req.Items.Count > 21 || req.Items.Any(x =>
            !Enum.IsDefined(x.Weekday) || !Enum.IsDefined(x.Shift)) ||
            req.Items.GroupBy(x => new { x.Weekday, x.Shift }).Any(g => g.Count() > 1))
            return EndpointHelpers.Error("Dias ou turnos inválidos/duplicados.");
        var old = await db.WorkSchedules.Where(x => x.UserId == userId).ToListAsync();
        var desired = req.Items.Select(x => (x.Weekday, x.Shift)).ToHashSet();
        var existing = old.Select(x => (x.Weekday, x.Shift)).ToHashSet();
        // Atualiza somente diferenças para evitar conflitos do índice único na mesma transação.
        db.WorkSchedules.RemoveRange(old.Where(x => !desired.Contains((x.Weekday, x.Shift))));
        db.WorkSchedules.AddRange(req.Items.Where(x => !existing.Contains((x.Weekday, x.Shift)))
            .Select(x => new WorkSchedule { UserId = userId, Weekday = x.Weekday, Shift = x.Shift }));
        events.Audit("User", userId, "ScheduleChanged", details: new { previous = old.Select(x => new { x.Weekday, x.Shift }), current = req.Items });
        await db.SaveChangesAsync();
        return Results.NoContent();
    }

    private static bool EmailIsValid(string? email)
    {
        if (string.IsNullOrWhiteSpace(email) || email.Length > 200) return false;
        try { return new MailAddress(email).Address.Equals(email.Trim(), StringComparison.OrdinalIgnoreCase); }
        catch { return false; }
    }
}
