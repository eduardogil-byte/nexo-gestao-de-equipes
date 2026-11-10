using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace Nexo.Api.Tests.Integration;

/// <summary>
/// Testes de integração dos fluxos principais. Cada teste faz requisições HTTP reais à API
/// (rodando em memória) contra um SQL Server de teste.
/// Padrão usado nos testes: Preparar (Arrange) → Agir (Act) → Verificar (Assert).
/// </summary>
public sealed class FluxosPrincipaisTests(NexoApiFixture api) : IClassFixture<NexoApiFixture>
{
    // ---------- Funções de apoio ----------

    private async Task<Guid> CriarProjetoAsync(string nome)
    {
        var resposta = await api.Admin.PostAsJsonAsync("/api/projects", new
        {
            name = nome,
            description = "Projeto criado por teste automatizado",
            objectives = "Validar o fluxo",
            justification = "Teste",
            researchArea = "Software",
            budget = 1000,
            expectedResults = "Teste passando",
            plannedStart = "2026-11-01",
            plannedEnd = "2026-12-15",
            visibility = "Private",
            leaderId = api.LiderId
        });
        Assert.Equal(HttpStatusCode.Created, resposta.StatusCode);
        var json = await resposta.Content.ReadFromJsonAsync<JsonElement>();
        return json.GetProperty("id").GetGuid();
    }

    private Task<HttpResponseMessage> CriarTarefaAsync(HttpClient cliente, Guid projetoId, params Guid[] responsaveis) =>
        cliente.PostAsJsonAsync($"/api/projects/{projetoId}/tasks", new
        {
            title = "Tarefa de teste",
            description = "Criada por teste automatizado",
            priority = "Normal",
            dueDate = "2026-11-30",
            assigneeIds = responsaveis
        });

    // ---------- Os 4 testes ----------

    [Fact]
    public async Task Admin_CriaProjeto_RetornaCreatedComLiderDefinido()
    {
        // Agir: o administrador cria um projeto indicando o líder.
        var projetoId = await CriarProjetoAsync("Projeto do teste 1");

        // Verificar: o líder consegue ver o projeto e ele é o líder cadastrado.
        var resposta = await api.Lider.GetAsync($"/api/projects/{projetoId}");
        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        var projeto = await resposta.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(api.LiderId, projeto.GetProperty("leaderId").GetGuid());
        Assert.Equal("Proposed", projeto.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Lider_CriaTarefa_RetornaCreatedComoNaoIniciada()
    {
        var projetoId = await CriarProjetoAsync("Projeto do teste 2");

        var resposta = await CriarTarefaAsync(api.Lider, projetoId, api.LiderId);

        Assert.Equal(HttpStatusCode.Created, resposta.StatusCode);
        var tarefa = await resposta.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("NotStarted", tarefa.GetProperty("status").GetString());
        Assert.Equal(projetoId, tarefa.GetProperty("projectId").GetGuid());
    }

    [Fact]
    public async Task Responsavel_ConcluiTarefa_StatusViraCompleted()
    {
        var projetoId = await CriarProjetoAsync("Projeto do teste 3");
        var criada = await CriarTarefaAsync(api.Lider, projetoId, api.LiderId);
        var tarefaId = (await criada.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        // Único responsável conclui a sua parte, então a tarefa inteira fica concluída.
        var resposta = await api.Lider.PostAsJsonAsync($"/api/tasks/{tarefaId}/complete", new { forceAll = false });

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        var resultado = await resposta.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Completed", resultado.GetProperty("status").GetString());
        Assert.NotEqual(JsonValueKind.Null, resultado.GetProperty("completedAtUtc").ValueKind);
    }

    [Fact]
    public async Task QuemNaoEhMembro_NaoCriaTarefaNemLeProjeto_Recebe403()
    {
        var projetoId = await CriarProjetoAsync("Projeto do teste 4");

        var criar = await CriarTarefaAsync(api.ForaDoProjeto, projetoId);
        var ler = await api.ForaDoProjeto.GetAsync($"/api/projects/{projetoId}");

        Assert.Equal(HttpStatusCode.Forbidden, criar.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, ler.StatusCode);
    }
}
