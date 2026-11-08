# Nexo — Backend ASP.NET Core 8 + SQL Server

Backend de referência para testes de laboratório do **Nexo**, sistema de gestão de projetos, equipes e atividades de um núcleo de pesquisa.

> **Atenção (04/11/2026):** o suporte de segurança oficial do .NET 8 termina em **10/11/2026**. Esta entrega utiliza .NET 8 por solicitação/compatibilidade com PCs do SENAI. Não publique na internet como produto definitivo sem atualizar para versão suportada, revisar segurança, implantar migrations, backups e executar testes reais.
>
> **Estado de validação:** os arquivos foram gerados e passam por verificações estáticas, mas **não foram compilados nem conectados a um SQL Server neste ambiente**, que não possui SDK .NET/SQL Server. Compile e execute nos computadores do SENAI antes de considerar qualquer módulo validado.

## 1. Requisitos do computador

- Windows 10/11, Visual Studio 2022 ou VS Code + extensão C#.
- **SDK .NET 8** (`dotnet --info` / `dotnet --list-sdks`).
- SQL Server Express/Developer/LocalDB ou instância institucional com permissão de criar banco; o programa não usa SQLite.
- Internet para restaurar dependências NuGet na primeira compilação (ou cache NuGet configurado pela instituição).
- Acesso local ao banco via autenticação integrada do Windows (mais simples para os PCs do SENAI).
- PowerShell para os scripts prontos; alternativa: comandos `dotnet` no terminal.

## 2. Rodando no SENAI — roteiro de 5 passos

Abra **PowerShell** na pasta que contém `Nexo.Backend.sln`.

**Passo 1 — Configurar SQL Server e JWT (uma vez por computador/perfil Windows).**

```powershell
.\scripts\configurar-senai.ps1 -SqlInstance '.\SQLEXPRESS'
```

Outras instâncias comuns:

```powershell
.\scripts\configurar-senai.ps1 -SqlInstance '(localdb)\MSSQLLocalDB'
.\scripts\configurar-senai.ps1 -SqlInstance '.'
.\scripts\configurar-senai.ps1 -SqlInstance '.\SQLEXPRESS' -Database 'NexoTeste'
```

O script configura a **connection string e a chave JWT em `dotnet user-secrets`**, fora do repositório. O projeto tem `InitializeForClassroom=true` **somente em Development**. Na primeira execução, cria automaticamente um banco **dedicado** (`NexoDb`) com as tabelas de Identity e Nexo. Não altera bancos existentes com outros nomes. **Não use EnsureCreated em banco de produção.**

**Passo 2 — Restaurar, compilar e executar.**

```powershell
dotnet restore .\Nexo.Backend.sln
dotnet build .\Nexo.Backend.sln
.\scripts\iniciar-senai.ps1
```

Em outro terminal, abra **http://localhost:5088/swagger**. O script de laboratório usa **HTTP somente em localhost**. Para uso remoto/produção, configure HTTPS/TLS e hospedagem adequada.

**Passo 3 — Criar o primeiro usuário.** Em `/swagger`, execute `POST /api/auth/register` com, por exemplo:

```json
{
  "fullName": "Coordenador Teste",
  "email": "coordenador@exemplo.com",
  "password": "SenhaForte#2026",
  "specialty": "Coordenação"
}
```

**Passo 4 — Promover o primeiro administrador.** Pare a API (`Ctrl+C`) e execute:

```powershell
.\scripts\primeiro-admin.ps1 -Email 'coordenador@exemplo.com'
```

Este comando funciona **somente se ainda não existir nenhum administrador**, exige acesso ao computador/configuração do servidor, aprova esse usuário e atribui a função `Admin`. Ele termina sem abrir o servidor.

**Passo 5 — Fazer login e testar.**

```powershell
.\scripts\iniciar-senai.ps1
```

Em `/swagger`, execute `POST /api/auth/login` com o e-mail/senha cadastrados. Copie `accessToken`, clique em **Authorize** no Swagger e cole **o token sem a palavra `Bearer`** (o esquema HTTP já acrescenta o prefixo).

Execute `GET /api/health/database`, `GET /api/projects` e `GET /api/dashboard`. Ou execute, em outro terminal:

```powershell
.\scripts\testar-api.ps1 -Email 'coordenador@exemplo.com'
```

O script de verificação pergunta a senha, sem gravá-la em arquivo, e consulta endpoints de leitura. **Não altera dados.**

## 3. Próximo teste funcional

1. Crie outro usuário em `/api/auth/register` e aprove-o em `/api/users/{id}/approve` usando token do coordenador.
2. Crie projeto em `POST /api/projects`, com `leaderId` de um usuário aprovado. O estado inicial é `Proposed`.
3. Inclua integrantes em `/api/projects/{id}/members`.
4. Crie uma tarefa via `POST /api/projects/{id}/tasks`. Se criada por um integrante da equipe, o projeto muda automaticamente para `InProgress`.
5. Conclua tarefa via `POST /api/tasks/{id}/complete` e registre atividades em `POST /api/activities`.
6. Consulte `GET /api/dashboard/projects/{id}`; os **três progressos permanecem separados**.
7. Simule uma colaboração de fora da equipe, enviando `collaborationCode` gerado pelo projeto; líder avalia via `POST /api/contributions/{id}/review`.

Exemplos de requisições: `src/Nexo.Api/Nexo.Api.http` (extensão REST Client do VS Code) e Swagger.

## 4. Organização do repositório

```
Nexo.Backend.sln
src/Nexo.Api/
  Domain/          Entidades e enums das regras de negócio
  Data/            EF Core e mapeamento SQL Server
  Infrastructure/  Identity, JWT, permissões, eventos e notificações
  Endpoints/       Rotas por módulo
  Properties/      Configuração de debug
  Nexo.Api.http     Exemplos HTTP
scripts/
  configurar-senai.ps1
  iniciar-senai.ps1
  primeiro-admin.ps1
  testar-api.ps1
tests/
  Nexo.Api.Tests.csproj
  EndpointHelpersTests.cs
docs/
  ENDPOINTS.md
  DECISOES_E_LIMITES.md
```

## 5. Recursos incluídos

- Cadastro de usuários com aprovação, login JWT, papéis `Admin` e `Member` e perfis.
- Líder definido por projeto; um usuário pode ser líder em um e integrante em outro.
- Equipes exclusivas de projetos, entrada/saída sem apagar histórico e sinalização de tarefas para redistribuição.
- Projetos privados por padrão; públicos dentro do núcleo permitem leitura de detalhes, tarefas e progresso.
- Projetos propostos, em andamento, pausados, concluídos ou cancelados; início automático com primeira ação de trabalho de integrante.
- Tarefas com múltiplos responsáveis, aceite de propostas, conclusão individual/antecipada, edição e reabertura pelo líder.
- Registros diários (incluindo retroativos), jornadas por dia e turno, registro de dias sem atividades e consulta de dias sem registro.
- Categorias de atividade, tempo declarado, obstáculos e resoluções.
- Metas/marcos opcionais e entregas simples.
- Colaborações externas submetidas por código (para projetos privados) ou por identificador público, com confirmação do líder.
- Upload autenticado de arquivos (até 10 MB) e links externos, com acesso restrito a membros.
- Notificações internas por ações e verificação periódica de prazos (no ciclo do serviço).
- Dashboard geral, indicadores separados por projeto e resumos diários/categorias/projetos.
- Eventos de auditoria das alterações relevantes.

## 6. SQL Server e compatibilidade

`appsettings.json` vem com exemplo **SQL Express Windows Authentication**:

```text
Server=.\SQLEXPRESS;Database=NexoDb;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true
```

O script substitui essa conexão no `user-secrets` do usuário Windows atual. Para instância do SENAI que exige login SQL, solicite credenciais próprias ao laboratório e configure `ConnectionStrings:DefaultConnection` **fora do repositório**, de preferência via variável `ConnectionStrings__DefaultConnection`. Não envie senha pelo Git ou ZIP.

Foi configurado `UseCompatibilityLevel(120)` para gerar consultas compatíveis com **nível SQL Server 2014** (compatibility level 120). Isso **não constitui certificação de execução em SQL Server 2014**, que é antigo e fora do suporte. Use instância moderna quando possível.

O exemplo `TrustServerCertificate=True` destina-se exclusivamente a ambiente de laboratório com certificado local; em produção utilize certificado confiável e criptografia adequada.

### Importante sobre o schema de teste

A inicialização do laboratório usa `EnsureCreated` em `Development`. Esse recurso simplifica os testes, mas **não gera migrations**. Para implantação real, crie um novo banco com migrations do EF Core (`dotnet ef migrations add InitialCreate ...`) e aplique com controle de versão e backup, sem rodar `EnsureCreated` nesse banco. Não tente aplicar a primeira migration por cima de um banco já criado via `EnsureCreated` sem procedimento de migração de dados planejado.

## 7. Testes automatizados

```powershell
dotnet test .\Nexo.Backend.sln
```

Há testes de validação simples e um smoke test HTTP/SQL (`scripts/testar-api.ps1`). Ainda são necessários testes integrados de permissões, condições de concorrência, upload e fluxos com múltiplos usuários em um SQL Server real antes da entrega.

## 8. Segurança e limites

- **Não há credenciais, chaves JWT nem senhas de SQL gravadas no ZIP.**
- Passwords guardadas pelo ASP.NET Core Identity usando hash adequado.
- Aprovação obrigatória antes do login; contas bloqueadas/rejeitadas têm token invalidado na próxima requisição.
- Autorização em cada endpoint e checagem por projeto/relação de equipe.
- Nenhuma exclusão física de projeto/tarefa/atividade; mantém histórico.
- Proteção básica de login/cadastro por limite de requisições.
- Anexos gravados em `App_Data/uploads` fora dos arquivos estáticos, com nome aleatório e download protegido.
- Arquivos são limitados por extensão e tamanho, **mas não há antivírus/análise profunda do conteúdo**; necessário em produção.
- Sem confirmação de e-mail, refresh token/revogação granular, MFA, calendário de feriados/afastamentos, painel de administração de permissões avançadas ou infraestrutura de backups automatizados.
- Os horários de alerta e dias sem registro usam UTC como referência; definir fuso do núcleo antes de produção.

## 9. Fontes de referência

- [Suporte oficial do .NET](https://dotnet.microsoft.com/pt-br/platform/support/policy/dotnet-core)
- [Provedor SQL Server para EF Core](https://learn.microsoft.com/pt-br/ef/core/providers/sql-server/)
- [Identity em ASP.NET Core](https://learn.microsoft.com/aspnet/core/security/authentication/identity)
- [JWT Bearer Authentication](https://learn.microsoft.com/aspnet/core/security/authentication/configure-jwt-bearer-authentication)

**Entrega destinada à validação local do backend e à evolução gradual com o frontend React.**
