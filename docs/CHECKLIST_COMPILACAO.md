# Checklist de compilação e execução

Use este checklist para compilar e rodar o Nexo pela primeira vez. Marque cada item e registre
qualquer erro na [tabela do final](#registro-de-erros-encontrados).

> Como o código nunca foi compilado antes, **é esperado** encontrar erros. Corrija um de cada vez,
> com a menor mudança possível, e anote na tabela.

## 0. Ambiente

- [x] SDK .NET 8 instalado: `dotnet --list-sdks` mostra uma linha começando com `8.`
- [x] Node.js 18.18+ (ideal 20 LTS): `node -v`
- [x] SQL Server acessível (Express, LocalDB ou instância institucional)
- [x] Você está na branch certa (não na `main`): `git branch --show-current`

## 1. Backend (pasta `Backend/`)

- [x] Configurar banco e chave JWT em `user-secrets` (fora do repositório):
      `.\scripts\configurar-senai.ps1 -SqlInstance '.\SQLEXPRESS'`
- [x] `dotnet restore Nexo.Backend.sln` sem erros
- [x] `dotnet build Nexo.Backend.sln` com **0 erros** (anote os avisos relevantes)
- [x] `dotnet test Nexo.Backend.sln` com todos os testes passando
- [x] A API sobe: `.\scripts\iniciar-senai.ps1` (ou `dotnet run --project src/Nexo.Api`)
- [x] O Swagger abre em `http://localhost:5088/swagger`
- [x] O banco `NexoDb` foi criado com as tabelas (confira com `scripts/verificar-sql.sql`)
- [x] `GET /api/health/database` responde com sucesso

## 2. Frontend (pasta `Frontend/`)

- [x] `npm install` sem erros
- [x] `npm run format:check` sem diferenças
- [x] `npm run typecheck` com **0 erros**
- [x] `npm run build` gera a pasta `dist/`
- [x] `Copy-Item .env.example .env` (o `.env` não vai para o Git)
- [x] `npm run dev` abre em `http://localhost:5173`

## 3. Fluxo de ponta a ponta (backend + frontend rodando)

> O mesmo fluxo já foi validado **pela API** em 10/11/2026 (cadastro, aprovação, projeto, tarefa,
> atividade, dashboard e bloqueio de não-membro). Falta conferir **pela interface no navegador**.

- [ ] Cadastrar uma conta
- [ ] Promover o primeiro admin (`Backend/scripts/primeiro-admin.ps1`) e reiniciar a API
- [ ] Fazer login como admin e aprovar a conta cadastrada
- [ ] Criar um projeto
- [ ] Criar uma tarefa no projeto
- [ ] Registrar uma atividade
- [ ] Conferir os números no dashboard

## Registro de erros encontrados

| # | Etapa | Comando | Mensagem de erro | Causa | Correção aplicada | Commit |
| - | ----- | ------- | ---------------- | ----- | ----------------- | ------ |
| 1 | Frontend | `npm run typecheck` | `src/lib/api.ts(1,27): error TS2339: Property 'env' does not exist on type 'ImportMeta'` | Faltava o arquivo de tipos do Vite, então o TypeScript não conhecia `import.meta.env` | Criado `Frontend/src/vite-env.d.ts` com `/// <reference types="vite/client" />` | Etapa 2 |
| 2 |       |         |                  |       |                   |        |
| 3 |       |         |                  |       |                   |        |
