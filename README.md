# Nexo — Gestão de Projetos, Equipes e Atividades

O **Nexo** é um sistema web para organizar o trabalho de um **núcleo de pesquisa**: cadastrar
projetos, montar equipes, distribuir tarefas, registrar as atividades do dia a dia e acompanhar o
andamento em um painel (dashboard).

Este repositório é o **MVP** (primeira versão funcional) do PDI em grupo do SENAI.

> **Estado atual:** boa parte do código foi gerada com apoio de IA e **ainda não foi compilada nem
> executada de ponta a ponta**. Veja o [checklist de compilação](docs/CHECKLIST_COMPILACAO.md) e a
> seção [Limitações conhecidas](#limitações-conhecidas).

## Como as peças se conectam

```text
┌──────────────┐   HTTP + JSON (JWT)   ┌──────────────┐    EF Core    ┌──────────────┐
│   Frontend   │ ────────────────────▶ │   API        │ ────────────▶ │  SQL Server  │
│ React + Vite │ ◀──────────────────── │ ASP.NET Core │ ◀──────────── │   (NexoDb)   │
│ localhost:5173│                      │ localhost:5088│              │              │
└──────────────┘                       └──────────────┘               └──────────────┘
```

O navegador só conversa com a API. Quem acessa o banco é **somente o backend**.

## Tecnologias

| Camada   | Tecnologias                                                                 |
| -------- | --------------------------------------------------------------------------- |
| Backend  | ASP.NET Core 8 (Minimal APIs), Entity Framework Core, ASP.NET Identity, JWT |
| Banco    | SQL Server (Express, LocalDB ou instância institucional)                    |
| Frontend | React 18, TypeScript, Vite, Tailwind CSS, React Query, React Router         |
| Testes   | xUnit (projeto em `Backend/tests`)                                          |

## Funcionalidades do MVP

- **Login e cadastro** com JWT; novas contas ficam pendentes até a aprovação de um administrador.
- **Administração:** aprovar/rejeitar usuários, editar dados e turnos.
- **Projetos:** listar, buscar, criar, editar; indicadores de progresso e equipe.
- **Tarefas:** quadro por situação, criação, comentários e conclusão (por pessoa ou geral).
- **Atividades:** registro diário com horas, inclusive retroativo.
- **Dashboard:** contadores, prazos, projetos, tarefas e atividades recentes.
- **Notificações** de prazos e eventos.
- **Pesquisa e arquivos:** etapas, entregas, obstáculos, upload e links externos.

A lista completa está em [Frontend/README.md](Frontend/README.md) e os endpoints em
[Backend/docs/ENDPOINTS.md](Backend/docs/ENDPOINTS.md).

## Como rodar (resumo)

Você precisa de **SDK .NET 8**, **Node.js 20 LTS** e um **SQL Server**.

1. **Backend:** configure o banco e a chave JWT (guardados em `dotnet user-secrets`, fora do
   repositório), compile e inicie a API em `http://localhost:5088`.
   Passo a passo em [Backend/README.md](Backend/README.md).
2. **Frontend:** `npm install` e `npm run dev` em `Frontend/`, abrindo `http://localhost:5173`.
   Passo a passo em [Frontend/README.md](Frontend/README.md).
3. Para compilar e conferir tudo com calma, siga o
   [checklist de compilação](docs/CHECKLIST_COMPILACAO.md).

## Fluxo da demonstração

1. **Cadastro:** criar uma conta na tela de login.
2. **Aprovação:** um administrador aprova a conta em *Administração*.
3. **Projeto:** criar um projeto e definir líder e integrantes.
4. **Tarefa:** criar uma tarefa no projeto e atribuí-la a um integrante.
5. **Atividade:** registrar uma atividade (com horas) em dia de trabalho.
6. **Dashboard:** conferir os números e prazos atualizados no painel.

## Estrutura de pastas

```text
nexo-gestao-de-equipes/
├── Backend/
│   ├── Nexo.Backend.sln
│   ├── src/Nexo.Api/        # API: Domain, Data (DbContext), Endpoints, Infrastructure
│   ├── tests/               # testes automatizados
│   ├── scripts/             # scripts PowerShell/SQL de apoio
│   └── docs/                # endpoints, decisões e limites
├── Frontend/
│   ├── src/                 # components, contexts, lib, pages
│   ├── scripts/
│   └── docs/                # roteiro de testes
├── docs/
│   └── CHECKLIST_COMPILACAO.md
├── CONTRIBUINDO.md
└── README.md
```

## Divisão do trabalho

| Integrante | Responsabilidade                |
| ---------- | ------------------------------- |
| Eduardo    | Backend                         |
| Joana      | Frontend                        |
| Diogo      | Banco, testes e documentação    |

Como trabalhamos em equipe (branches e pull requests): [CONTRIBUINDO.md](CONTRIBUINDO.md).

## Limitações conhecidas

- **.NET 8 sem suporte oficial:** o suporte do .NET 8 terminou em **10/11/2026**. O projeto usa
  essa versão por compatibilidade com os computadores do SENAI; para uso real é preciso migrar para
  uma versão suportada (.NET 10 LTS).
- **Banco sem migrations:** o banco é criado com `EnsureCreated`, que não versiona mudanças no
  esquema. Antes de produção, é preciso adotar *migrations* do EF Core.
- **Código ainda não validado:** não houve compilação nem execução de ponta a ponta antes desta
  organização; podem existir erros de build e de execução.
- **Sem e-mail, MFA ou recuperação de senha.**
- **HTTP apenas em localhost** (modo laboratório); para internet é preciso HTTPS e hospedagem segura.
- **Arquivos enviados** ficam em disco local, sem antivírus nem política de retenção.
- Mais detalhes em [Backend/docs/DECISOES_E_LIMITES.md](Backend/docs/DECISOES_E_LIMITES.md).
