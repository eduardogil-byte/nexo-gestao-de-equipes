using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Nexo.Api.Data;
using Nexo.Api.Domain;

namespace Nexo.Api.Infrastructure;

public static class Setup
{
    public static void AddNexoInfrastructure(this WebApplicationBuilder builder)
    {
        var connection = builder.Configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Configure ConnectionStrings:DefaultConnection para o SQL Server.");
        var compatibility = builder.Configuration.GetValue("Database:CompatibilityLevel", 120);
        builder.Services.AddDbContext<NexoDbContext>(o => o.UseSqlServer(connection, sql =>
        {
            sql.UseCompatibilityLevel(compatibility);
            sql.EnableRetryOnFailure(3);
        }));
        builder.Services.AddIdentityCore<AppUser>(o =>
        {
            o.User.RequireUniqueEmail = true;
            o.Password.RequiredLength = 10;
            o.Password.RequireNonAlphanumeric = true;
            o.Password.RequireDigit = true;
            o.Password.RequireUppercase = true;
            o.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            o.Lockout.MaxFailedAccessAttempts = 5;
            o.Lockout.AllowedForNewUsers = true;
        })
        .AddRoles<IdentityRole<Guid>>()
        .AddEntityFrameworkStores<NexoDbContext>()
        .AddDefaultTokenProviders();

        var key = builder.Configuration["Jwt:Key"];
        if (string.IsNullOrWhiteSpace(key) || Encoding.UTF8.GetByteCount(key) < 32)
            throw new InvalidOperationException("Configure Jwt:Key (mínimo 32 bytes) via user-secrets ou variável de ambiente Jwt__Key. Não inclua o segredo no código.");
        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(o =>
        {
            o.MapInboundClaims = true;
            o.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true, ValidIssuer = builder.Configuration["Jwt:Issuer"],
                ValidateAudience = true, ValidAudience = builder.Configuration["Jwt:Audience"],
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
                ValidateLifetime = true, ClockSkew = TimeSpan.FromMinutes(1),
                RoleClaimType = ClaimTypes.Role, NameClaimType = ClaimTypes.NameIdentifier
            };
            o.Events = new JwtBearerEvents
            {
                OnTokenValidated = async c =>
                {
                    if (!Guid.TryParse(c.Principal?.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
                    {
                        c.Fail("Token inválido."); return;
                    }
                    var manager = c.HttpContext.RequestServices.GetRequiredService<UserManager<AppUser>>();
                    var user = await manager.FindByIdAsync(userId.ToString());
                    if (user is null || user.Approval != ApprovalState.Approved || user.LockoutEnd > DateTimeOffset.UtcNow)
                        c.Fail("Usuário inativo ou não aprovado.");
                }
            };
        });
        builder.Services.AddAuthorization(options => options.AddPolicy("AdminOnly", policy => policy.RequireRole("Admin")));
        var origins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();
        builder.Services.AddCors(o => o.AddPolicy("NexoFrontend", p =>
            p.WithOrigins(origins).AllowAnyMethod().AllowAnyHeader()));
        builder.Services.AddRateLimiter(o =>
        {
            o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            o.AddPolicy("auth", context => RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 10, Window = TimeSpan.FromMinutes(1), QueueLimit = 0,
                    AutoReplenishment = true
                }));
        });
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddScoped<AccessService>();
        builder.Services.AddScoped<DomainEvents>();
        builder.Services.AddHostedService<DeadlineNotificationWorker>();
        builder.Services.ConfigureHttpJsonOptions(o =>
            o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
            {
                Title = "Nexo - API", Version = "v1", Description = "Gestão de projetos e equipes do núcleo de pesquisa"
            });
            options.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Name = "Authorization", Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
                Scheme = "bearer", BearerFormat = "JWT", In = Microsoft.OpenApi.Models.ParameterLocation.Header
            });
            options.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
            {
                [new Microsoft.OpenApi.Models.OpenApiSecurityScheme
                {
                    Reference = new Microsoft.OpenApi.Models.OpenApiReference
                    { Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme, Id = "Bearer" }
                }] = Array.Empty<string>()
            });
        });
    }

    public static async Task InitializeDbForClassroom(this WebApplication app, string[] args)
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NexoDbContext>();
        if (app.Environment.IsDevelopment() && app.Configuration.GetValue<bool>("Database:InitializeForClassroom"))
        {
            await db.Database.EnsureCreatedAsync();
        }
        else if (!await db.Database.CanConnectAsync())
        {
            throw new InvalidOperationException("Banco indisponível. Configure a conexão e aplique o esquema de banco antes de executar.");
        }
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        foreach (var role in new[] { "Admin", "Member" })
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new IdentityRole<Guid>(role));

        var option = Array.IndexOf(args, "--bootstrap-admin");
        if (option < 0) return;
        if (option + 1 >= args.Length) throw new InvalidOperationException("Uso: dotnet run -- --bootstrap-admin email@exemplo.com");
        var manager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var existingAdmins = await manager.GetUsersInRoleAsync("Admin");
        if (existingAdmins.Count > 0) throw new InvalidOperationException("Já existe administrador. Bootstrap bloqueado.");
        var email = args[option + 1];
        var user = await manager.FindByEmailAsync(email)
            ?? throw new InvalidOperationException("Primeiro faça o cadastro via /api/auth/register.");
        user.Approval = ApprovalState.Approved;
        var updated = await manager.UpdateAsync(user);
        if (!updated.Succeeded) throw new InvalidOperationException(string.Join(", ", updated.Errors.Select(e => e.Description)));
        var result = await manager.AddToRoleAsync(user, "Admin");
        if (!result.Succeeded) throw new InvalidOperationException(string.Join(", ", result.Errors.Select(e => e.Description)));
        Console.WriteLine($"Administrador inicial ativado: {email}. Feche este comando e inicie normalmente.");
    }
}
