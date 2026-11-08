using Microsoft.AspNetCore.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Nexo.Api.Endpoints;
using Nexo.Api.Infrastructure;

var builder = WebApplication.CreateBuilder(args);
builder.AddNexoInfrastructure();
var app = builder.Build();

app.UseExceptionHandler(handler => handler.Run(async context =>
{
    var error = context.Features.Get<IExceptionHandlerFeature>()?.Error;
    var logger = context.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("NexoException");
    if (error is not null) logger.LogError(error, "Erro não tratado em {Path}", context.Request.Path);
    context.Response.StatusCode = error is DbUpdateConcurrencyException ? 409 : 500;
    await context.Response.WriteAsJsonAsync(new { error = context.Response.StatusCode == 409
        ? "O registro foi alterado por outra pessoa. Atualize e tente novamente."
        : "Erro interno. Consulte os logs do servidor." });
}));

await app.InitializeDbForClassroom(args);
if (Array.IndexOf(args, "--bootstrap-admin") >= 0) return;

if (app.Environment.IsDevelopment()) { app.UseSwagger(); app.UseSwaggerUI(); }
app.UseHttpsRedirection();
app.UseRouting();
app.UseCors("NexoFrontend");
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapGet("/api/health", () => Results.Ok(new { status = "ok", name = "Nexo API", utc = DateTime.UtcNow }))
    .AllowAnonymous().WithTags("Sistema");
app.MapGet("/api/health/database", async (Nexo.Api.Data.NexoDbContext db) =>
    await db.Database.CanConnectAsync() ? Results.Ok(new { database = "connected" })
        : EndpointHelpers.Error("Banco indisponível", 503)).RequireAuthorization().WithTags("Sistema");
app.MapAuthEndpoints();
app.MapProjectEndpoints();
app.MapTaskEndpoints();
app.MapActivityEndpoints();
app.MapResearchEndpoints();
app.MapFilesEndpoints();
app.MapDashboardEndpoints();
app.MapNotificationEndpoints();
app.Run();

public partial class Program { }
