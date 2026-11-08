# Nexo — Frontend React + TypeScript + Tailwind CSS

**Interface para o backend ASP.NET Core 8 + SQL Server do Nexo.**

Identidade: **Paleta A — Institucional moderna**, baseada no logo enviado pela equipe: azul-escuro `#0F172A`, azul `#2563EB`, verde-petróleo `#0D9488`, fundo `#F8FAFC`, borda `#E2E8F0`.

> **Estado de validação:** código-fonte entregue e checado estaticamente (sintaxe dos arquivos TypeScript/TSX e imports relativos). A instalação de pacotes npm não concluiu neste ambiente, portanto **não foi possível executar `npm run build` nem abrir a interface em navegador**. Execute as verificações nos PCs do SENAI antes de apresentar o resultado como validado. Backend, SQL Server e autenticação não foram executados em conjunto aqui.

## 1. Pré-requisitos no SENAI

- **Node.js 20 LTS** recomendado (Node 18.18+ também compatível com Vite 5). Confirme: `node -v`, `npm -v`.
- VS Code com extensões React/TypeScript e Tailwind CSS IntelliSense (opcional).
- Backend **Nexo .NET 8** rodando na máquina, em `http://localhost:5088`.
- SQL Server conectado e banco inicializado pelo backend.
- Conexão com a internet **na primeira instalação do npm**, ou cache/repositório npm interno disponível.

## 2. Rodar o frontend (PowerShell / CMD)

Abra o terminal na pasta extraída (onde está `package.json`):

```powershell
npm install
Copy-Item .env.example .env
npm run dev
```

Acesse: **http://localhost:5173**.

O proxy do Vite redireciona `/api/*` para `http://localhost:5088`. Assim, não precisamos expor o frontend na mesma porta da API nem armazenar credenciais do SQL Server nele.

Se o backend usar outra porta, edite `.env`:

```dotenv
VITE_API_BASE_URL=/api
VITE_BACKEND_URL=http://localhost:5088
```

Reinicie `npm run dev` após editar o `.env`.

### Iniciar em um comando no PowerShell

```powershell
.\scripts\iniciar-senai.ps1
```

Esse script apenas verifica Node, instala dependências se necessário, configura `.env` de exemplo e roda o Vite. **O backend deve estar em execução em outro terminal**.

## 3. Fluxo do primeiro acesso

1. Inicie **SQL Server + backend** conforme o `COMECE_AQUI.txt` do ZIP Nexo Backend .NET 8.
2. Acesse `http://localhost:5173`, clique em **Criar conta** e cadastre o primeiro coordenador.
3. O primeiro administrador é promovido **pelo script seguro do backend** (pare a API e use `scripts\primeiro-admin.ps1 -Email '...'` na pasta do backend). O frontend **não promove usuários sozinho**.
4. Reinicie a API e faça login no Nexo. Na página **Administração**, aprove novas contas.
5. Crie projeto, selecione líder aprovado, adicione integrantes, crie tarefas e registre atividades.

## 4. Funcionalidades disponíveis na interface

| Área | Recursos |
|---|---|
| Login e cadastro | Autenticação JWT, registro aguardando aprovação, sessão por aba |
| Dashboard | Contadores reais, prazos, projetos, tarefas e atividades recentes |
| Projetos | Lista, busca e filtros, criação por administração, edição por líder/admin |
| Projeto detalhado | 3 indicadores independentes de progresso, equipe, estado, descrição |
| Tarefas | Quadro por situação, criação, edição, comentários, conclusão por pessoa ou geral, reabertura por líder |
| Minhas tarefas | Pendentes e concluídas, aceite/recusa de atribuições de colegas |
| Equipes | Consulta e inclusão/remoção de membros pela administração |
| Pesquisa | Etapas opcionais, entregas e obstáculos |
| Arquivos | Upload protegido, download via JWT, links externos |
| Colaborações | Registro de ajuda externa por projeto público ou código de projeto privado; revisão pelo líder |
| Atividades | Registro diário, horas manuais, dias sem atividade, registros retroativos, edição |
| Notificações | Central, marcação de leitura, alerta no sino |
| Administração | Aprovação/rejeição de usuários, edição de dados, edição de turnos, estado dos projetos |
| Perfil | Dados pessoais, escala habitual de dias/turnos |

**Limites conscientes deste protótipo:** gerenciamento avançado de responsáveis em tarefas já criadas, histórico visual detalhado de auditoria, troca de líder pela interface, telas sofisticadas de relatórios, paginação de grandes volumes, confirmação de e-mail, fluxo de recuperação de senha, internacionalização e modo escuro **não foram implementados**. A API possui mais endpoints que a interface usa atualmente. Documente e priorize essas evoluções antes da entrega definitiva.

## 5. Compilação e verificações

```powershell
npm run typecheck
npm run build
npm run preview
npm run test:api
```

`npm run build` gera a pasta `dist/` que contém os arquivos estáticos. `npm run test:api` consulta apenas a rota pública de saúde, sem modificar o banco.

Para executar os testes funcionais, veja [docs/ROTEIRO_TESTES.md](docs/ROTEIRO_TESTES.md).

## 6. Estrutura

```text
Nexo-Frontend-React/
├── public/
│   ├── favicon.svg
│   ├── nexo-mark.svg
│   └── logo-referencia.png          # prévia original enviada pela equipe
├── src/
│   ├── components/                  # elementos reutilizáveis
│   ├── contexts/AuthContext.tsx     # autenticação e sessão
│   ├── lib/api.ts                   # cliente HTTP / JWT / erros
│   ├── lib/types.ts                 # tipos e rótulos do backend
│   ├── pages/                       # telas por módulo
│   ├── App.tsx                      # layout e navegação
│   ├── main.tsx                     # providers e roteador
│   └── styles.css                   # Tailwind + componentes
├── scripts/
│   ├── iniciar-senai.ps1
│   └── check-api.mjs
├── docs/ROTEIRO_TESTES.md
├── .env.example
├── package.json
├── vite.config.ts
└── tailwind.config.js
```

## 7. Segurança e implantação

- **Nenhuma senha do banco** deve ser gravada no frontend. SQL Server é acessado **somente pelo backend**, que valida o JWT e as permissões.
- O token JWT fica em `sessionStorage`, isolado da persistência em disco entre sessões; isso não elimina o risco de XSS. Revise a estratégia de sessão, CSP e segurança antes da produção.
- As permissões do backend determinam quem pode alterar cada recurso. Esconder botões na interface **não substitui autorização real**.
- `http://localhost` é apenas para laboratório. Para acesso pela internet, configure domínio, HTTPS, proxy reverso, políticas de segurança e ambiente de produção.
- O suporte oficial ao **.NET 8 encerra em 10/11/2026**; planeje atualização do backend para versão suportada antes da publicação.

## 8. Marca e créditos

O arquivo `public/logo-referencia.png` é a **prévia original enviada pelo usuário**, sem alterações. O ícone `public/nexo-mark.svg` é uma **recriação vetorial aproximada** para permitir uso responsivo/transparentes em claro e escuro. Compare com a arte oficial e substitua pelo arquivo vetorial final da equipe quando estiver disponível.
