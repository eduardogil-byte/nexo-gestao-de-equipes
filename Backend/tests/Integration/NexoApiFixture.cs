using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Nexo.Api.Data;
using Nexo.Api.Domain;
using Xunit;

namespace Nexo.Api.Tests.Integration;

/// <summary>
/// Prepara o ambiente dos testes de integração, uma única vez para todos os testes da classe:
/// 1. apaga e recria um banco SQL Server exclusivo para testes (NexoDb_Tests);
/// 2. sobe a API inteira em memória (sem abrir porta de rede);
/// 3. cria três usuários aprovados (admin, líder e "de fora") e faz o login de cada um.
/// </summary>
public sealed class NexoApiFixture : IAsyncLifetime
{
    // Pode ser trocada pela variável de ambiente NEXO_TEST_CONNECTION (ex.: outra instância do SQL Server).
    // Usa o login do Windows, portanto não há senha de banco gravada aqui.
    private static readonly string ConnectionString =
        Environment.GetEnvironmentVariable("NEXO_TEST_CONNECTION")
        ?? @"Server=.\SQLEXPRESS;Database=NexoDb_Tests;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true";

    private WebApplicationFactory<Program> _factory = null!;

    public HttpClient Admin { get; private set; } = null!;
    public HttpClient Lider { get; private set; } = null!;
    public HttpClient ForaDoProjeto { get; private set; } = null!;
    public Guid LiderId { get; private set; }

    public async Task InitializeAsync()
    {
        GarantirQueEhBancoDeTeste();
        await ApagarBancoDeTesteAsync();

        // A chave JWT é gerada na hora: nunca existe em arquivo do repositório.
        var jwtKey = Convert.ToBase64String(Guid.NewGuid().ToByteArray().Concat(Guid.NewGuid().ToByteArray())
            .Concat(Guid.NewGuid().ToByteArray()).ToArray());

        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            // Em Development a API cria as tabelas sozinha (EnsureCreated) — é o que queremos nos testes.
            builder.UseEnvironment("Development");
            // UseSetting vale desde o início da inicialização; a API lê estes valores logo no começo do Program.cs.
            builder.UseSetting("ConnectionStrings:DefaultConnection", ConnectionString);
            builder.UseSetting("Jwt:Key", jwtKey);
            builder.UseSetting("Database:InitializeForClassroom", "true");
        });

        await ConferirBancoEmUsoAsync();

        var admin = await CriarUsuarioAsync("admin-teste@nexo.test", "Admin de Teste", "Admin");
        var lider = await CriarUsuarioAsync("lider-teste@nexo.test", "Líder de Teste", "Member");
        var fora = await CriarUsuarioAsync("fora-teste@nexo.test", "Pessoa de Fora", "Member");
        LiderId = lider.Id;

        Admin = await LogarAsync(admin.Email!, admin.Senha);
        Lider = await LogarAsync(lider.Email!, lider.Senha);
        ForaDoProjeto = await LogarAsync(fora.Email!, fora.Senha);
    }

    public Task DisposeAsync()
    {
        Admin?.Dispose();
        Lider?.Dispose();
        ForaDoProjeto?.Dispose();
        _factory?.Dispose();
        return Task.CompletedTask;
    }

    // Trava de segurança: nunca apagar um banco que não seja claramente de teste.
    private static void GarantirQueEhBancoDeTeste()
    {
        var nome = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(ConnectionString).InitialCatalog;
        if (!nome.EndsWith("_Tests", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                $"Por segurança, o banco de testes precisa terminar com '_Tests' (recebido: '{nome}').");
    }

    private static async Task ApagarBancoDeTesteAsync()
    {
        var options = new DbContextOptionsBuilder<NexoDbContext>().UseSqlServer(ConnectionString).Options;
        await using var db = new NexoDbContext(options);
        await db.Database.EnsureDeletedAsync();
    }

    // Garante que a API realmente está usando o banco de teste e não o NexoDb da demonstração.
    private async Task ConferirBancoEmUsoAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NexoDbContext>();
        var nome = db.Database.GetDbConnection().Database;
        if (!nome.EndsWith("_Tests", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"A API está usando o banco '{nome}', e não o banco de testes.");
        await Task.CompletedTask;
    }

    private async Task<(Guid Id, string? Email, string Senha)> CriarUsuarioAsync(string email, string nome, string papel)
    {
        // Senha aleatória e válida para a política do Identity (10+ caracteres, maiúscula, dígito e símbolo).
        var senha = "Aa1!" + Guid.NewGuid().ToString("N");
        using var scope = _factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var user = new AppUser
        {
            UserName = email, Email = email, FullName = nome, Approval = ApprovalState.Approved
        };
        var criado = await users.CreateAsync(user, senha);
        if (!criado.Succeeded) throw new InvalidOperationException(string.Join(", ", criado.Errors.Select(e => e.Description)));
        await users.AddToRoleAsync(user, papel);
        return (user.Id, email, senha);
    }

    private async Task<HttpClient> LogarAsync(string email, string senha)
    {
        var client = _factory.CreateClient();
        var resposta = await client.PostAsJsonAsync("/api/auth/login", new { email, password = senha });
        resposta.EnsureSuccessStatusCode();
        var json = await resposta.Content.ReadFromJsonAsync<JsonElement>();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", json.GetProperty("accessToken").GetString());
        return client;
    }
}
