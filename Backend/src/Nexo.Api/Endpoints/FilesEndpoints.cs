using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Nexo.Api.Data;
using Nexo.Api.Domain;
using Nexo.Api.Infrastructure;

namespace Nexo.Api.Endpoints;

public static class FilesEndpoints
{
    public sealed record DocumentLink(string Title, string Url, Guid? TaskId);
    private static readonly Dictionary<string, string> AllowedTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        [".pdf"] = "application/pdf", [".txt"] = "text/plain", [".md"] = "text/plain",
        [".csv"] = "text/csv", [".png"] = "image/png", [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg", [".docx"] = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        [".xlsx"] = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        [".pptx"] = "application/vnd.openxmlformats-officedocument.presentationml.presentation",
        [".zip"] = "application/zip"
    };

    public static void MapFilesEndpoints(this WebApplication app)
    {
        var g = app.MapGroup("/api/projects/{projectId:guid}").RequireAuthorization().WithTags("Arquivos e documentos");
        g.MapGet("/files", ListFiles);
        // Autenticação via Bearer JWT, não por cookies. Validação CSRF de formulários não se aplica aqui.
        g.MapPost("/files", Upload).DisableAntiforgery();
        g.MapGet("/links", ListLinks);
        g.MapPost("/links", NewLink);
        app.MapGet("/api/files/{id:guid}/download", Download).RequireAuthorization().WithTags("Arquivos e documentos");
    }

    private static async Task<IResult> ListFiles(Guid projectId, NexoDbContext db, AccessService access)
    {
        var p = await db.Projects.AsNoTracking().FirstOrDefaultAsync(x => x.Id == projectId);
        if (p is null) return Results.NotFound();
        if (!await access.CanWork(p)) return EndpointHelpers.Error("Somente integrantes autorizados podem consultar documentos.", 403);
        return Results.Ok(await db.Files.AsNoTracking().Where(x => x.ProjectId == projectId)
            .OrderByDescending(x => x.UploadedAtUtc)
            .Select(x => new { x.Id, x.ProjectId, x.TaskId, x.OriginalName, x.ContentType,
                x.Length, x.UploadedById, x.UploadedAtUtc }).ToListAsync());
    }

    private static async Task<IResult> Upload(Guid projectId, IFormFile file, [FromQuery] Guid? taskId,
        NexoDbContext db, AccessService access, DomainEvents events, IConfiguration cfg, IWebHostEnvironment env)
    {
        var p = await db.Projects.FirstOrDefaultAsync(x => x.Id == projectId);
        if (p is null) return Results.NotFound();
        if (!await access.CanWork(p)) return EndpointHelpers.Error("Somente membros podem enviar arquivos.", 403);
        if (!EndpointHelpers.IsEditable(p)) return EndpointHelpers.Error("Projeto encerrado.", 409);
        if (taskId.HasValue && !await db.Tasks.AnyAsync(x => x.Id == taskId && x.ProjectId == projectId))
            return EndpointHelpers.Error("Tarefa inválida.");
        var name = Path.GetFileName(file.FileName);
        var extension = Path.GetExtension(name);
        var maxBytes = cfg.GetValue<long>("Uploads:MaxBytes", 10 * 1024 * 1024);
        if (name.Length is < 1 or > 250 || !AllowedTypes.TryGetValue(extension, out var mime))
            return EndpointHelpers.Error("Extensão não permitida. Utilize PDF, TXT, MD, CSV, PNG, JPG, DOCX, XLSX, PPTX ou ZIP.");
        if (file.Length == 0 || file.Length > maxBytes) return EndpointHelpers.Error("Arquivo vazio ou acima do limite de 10 MB.");
        // O nome real nunca é utilizado no caminho do servidor.
        var stored = Guid.NewGuid().ToString("N") + extension.ToLowerInvariant();
        var directory = Path.Combine(env.ContentRootPath, "App_Data", "uploads");
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, stored);
        try
        {
            await using (var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                await file.CopyToAsync(output);
            var asset = new StoredFile
            {
                ProjectId = projectId, TaskId = taskId, UploadedById = access.UserId,
                OriginalName = name, StoredName = stored, ContentType = mime, Length = file.Length
            };
            db.Files.Add(asset);
            events.Audit("File", asset.Id, "Uploaded", projectId, new { asset.OriginalName, asset.Length });
            await events.BeginOnMemberWork(p, "FileUploaded");
            await db.SaveChangesAsync();
            return Results.Created($"/api/files/{asset.Id}/download", new { asset.Id, asset.OriginalName, asset.Length });
        }
        catch
        {
            if (File.Exists(path)) File.Delete(path);
            throw;
        }
    }

    private static async Task<IResult> Download(Guid id, NexoDbContext db, AccessService access, IWebHostEnvironment env)
    {
        var f = await db.Files.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (f is null) return Results.NotFound();
        var p = await db.Projects.AsNoTracking().FirstOrDefaultAsync(x => x.Id == f.ProjectId);
        if (p is null) return Results.NotFound();
        if (!await access.CanWork(p)) return EndpointHelpers.Error("Sem permissão para baixar este documento.", 403);
        var path = Path.Combine(env.ContentRootPath, "App_Data", "uploads", f.StoredName);
        if (!File.Exists(path)) return EndpointHelpers.Error("Arquivo não disponível no servidor.", 404);
        return Results.File(path, f.ContentType, fileDownloadName: f.OriginalName, enableRangeProcessing: false);
    }

    private static async Task<IResult> ListLinks(Guid projectId, NexoDbContext db, AccessService access)
    {
        var p = await db.Projects.AsNoTracking().FirstOrDefaultAsync(x => x.Id == projectId);
        if (p is null) return Results.NotFound();
        if (!await access.CanWork(p)) return EndpointHelpers.Error("Sem acesso aos documentos.", 403);
        return Results.Ok(await db.ExternalDocuments.AsNoTracking().Where(x => x.ProjectId == projectId)
            .OrderByDescending(x => x.CreatedAtUtc).ToListAsync());
    }

    private static async Task<IResult> NewLink(Guid projectId, DocumentLink req, NexoDbContext db,
        AccessService access, DomainEvents events)
    {
        var p = await db.Projects.FirstOrDefaultAsync(x => x.Id == projectId);
        if (p is null) return Results.NotFound();
        if (!await access.CanWork(p)) return EndpointHelpers.Error("Sem acesso.", 403);
        if (!EndpointHelpers.IsEditable(p)) return EndpointHelpers.Error("Projeto encerrado.", 409);
        if (!EndpointHelpers.IsValid(req.Title, 200) || string.IsNullOrWhiteSpace(req.Url) ||
            !EndpointHelpers.IsHttpLink(req.Url)) return EndpointHelpers.Error("Título ou URL inválido.");
        if (req.TaskId.HasValue && !await db.Tasks.AnyAsync(x => x.Id == req.TaskId && x.ProjectId == projectId))
            return EndpointHelpers.Error("Tarefa não pertence ao projeto.");
        var link = new ExternalDocument { ProjectId = projectId, AddedById = access.UserId,
            Title = req.Title.Trim(), Url = req.Url.Trim(), TaskId = req.TaskId };
        db.ExternalDocuments.Add(link);
        events.Audit("ExternalDocument", link.Id, "Added", projectId, new { link.Title });
        await events.BeginOnMemberWork(p, "LinkAdded");
        await db.SaveChangesAsync();
        return Results.Created($"/api/projects/{projectId}/links", new { link.Id, link.Title, link.Url });
    }
}
