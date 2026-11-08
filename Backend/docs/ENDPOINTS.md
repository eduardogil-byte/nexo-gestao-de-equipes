# Nexo — Referência de endpoints (v1)

Base: `http://localhost:5088`. Tipos enum JSON são **strings** (`"InProgress"`, `"Normal"`, `"Research"`, etc.). Todos os endpoints requerem `Authorization: Bearer <JWT>`, **exceto** `/api/health`, `/api/auth/register` e `/api/auth/login`.

## Autenticação e pessoas

| Método | Caminho | Quem |
|---|---|---|
| POST | `/api/auth/register` | Público, aguarda aprovação |
| POST | `/api/auth/login` | Público, usuário aprovado |
| GET / PUT | `/api/auth/me` | Próprio usuário |
| GET / PUT | `/api/auth/me/schedule` | Próprio usuário |
| GET | `/api/users` | Coordenação |
| POST | `/api/users/{id}/approve` | Coordenação |
| POST | `/api/users/{id}/reject` | Coordenação |
| PUT | `/api/users/{id}` | Coordenação |
| GET / PUT | `/api/users/{id}/schedule` | Coordenação |

## Projetos e equipes

| Método | Caminho | Quem |
|---|---|---|
| GET | `/api/projects` | Administrador: todos; integrante: próprios + públicos |
| GET | `/api/projects/{id}` | Público interno ou membro; código de colaboração só líder/admin |
| POST | `/api/projects` | Coordenação |
| PUT | `/api/projects/{id}` | Líder/coordenação |
| POST | `/api/projects/{id}/status` | Coordenação |
| POST | `/api/projects/{id}/leader` | Coordenação |
| PUT | `/api/projects/{id}/progress` | Líder/coordenação |
| GET | `/api/projects/{id}/members` | Equipe/coordenação |
| POST | `/api/projects/{id}/members` | Coordenação |
| DELETE | `/api/projects/{id}/members/{userId}` | Coordenação; desligamento lógico |
| GET | `/api/projects/{id}/audit` | Líder/coordenação |

## Tarefas e colaboração

| Método | Caminho | Quem |
|---|---|---|
| GET / POST | `/api/projects/{projectId}/tasks` | Leitores / participantes |
| GET | `/api/tasks/mine` | Tarefas do usuário |
| GET / PUT | `/api/tasks/{id}` | Leitores / responsáveis e líderes |
| POST | `/api/tasks/{id}/state` | Responsáveis/líder |
| POST | `/api/tasks/{id}/complete` | Responsável aceito ou coordenação |
| POST | `/api/tasks/{id}/reopen` | Líder/coordenação |
| POST | `/api/tasks/{id}/assignees` | Integrante propõe; líder atribui direto |
| POST | `/api/tasks/{id}/accept` | Destinatário da proposta |
| POST | `/api/tasks/{id}/decline` | Destinatário da proposta |
| DELETE | `/api/tasks/{id}/assignees/{userId}` | Líder/coordenação |
| GET / POST | `/api/tasks/{id}/comments` | Equipe / participantes |
| POST | `/api/contributions` | Usuário aprovado fora da equipe |
| GET | `/api/contributions/mine` | Próprio usuário |
| GET | `/api/projects/{projectId}/contributions` | Líder/coordenação |
| POST | `/api/contributions/{id}/review` | Líder/coordenação |

`POST /api/tasks/{id}/complete` com corpo `{ "forceAll": false }` conclui apenas participação pessoal; com `true`, um responsável pode finalizar a tarefa inteira. Os registros individuais de conclusão são preservados no evento de auditoria.

## Pesquisa e registros

| Método | Caminho | Quem |
|---|---|---|
| GET / POST | `/api/activities` | Próprio usuário; administração pode consultar outros |
| PUT | `/api/activities/{id}` | Autor ou coordenação |
| GET | `/api/activities/missing-days` | Próprio usuário / coordenação |
| GET / POST | `/api/projects/{projectId}/milestones` | Leitores / líder |
| PUT | `/api/milestones/{id}/status` | Líder/coordenação |
| GET / POST | `/api/projects/{projectId}/deliverables` | Leitores / membros |
| POST | `/api/deliverables/{id}/complete` | Membros |
| GET / POST | `/api/projects/{projectId}/obstacles` | Integrantes |
| POST | `/api/obstacles/{id}/resolve` | Autor/líder/coordenação |

Atividades admitem `projectId=null` e `taskId=null` para trabalho geral do núcleo. A edição mantém vínculo original. `noActivity=true` impede informar horas e vínculos.

## Documentos e notificações

| Método | Caminho | Quem |
|---|---|---|
| GET | `/api/projects/{projectId}/files` | Membros/coordenação |
| POST | `/api/projects/{projectId}/files` | Membros/coordenação; multipart `file` |
| GET | `/api/files/{id}/download` | Membros/coordenação |
| GET / POST | `/api/projects/{projectId}/links` | Membros/coordenação |
| GET | `/api/notifications` | Próprio usuário |
| POST | `/api/notifications/{id}/read` | Próprio usuário |
| POST | `/api/notifications/read-all` | Próprio usuário |

## Dashboard e infraestrutura

| Método | Caminho | Quem |
|---|---|---|
| GET | `/api/dashboard` | Autenticado, dados conforme permissão |
| GET | `/api/dashboard/projects/{id}` | Leitor autorizado |
| GET | `/api/dashboard/recent` | Histórico cronológico filtrado |
| GET | `/api/dashboard/activity-summary` | Resumo por dia/categoria/projeto |
| GET | `/api/health` | Público; verifica processo, **não** banco |
| GET | `/api/health/database` | Autenticado; conecta SQL Server |

### Exemplos de payloads adicionais

Criar jornada: `PUT /api/auth/me/schedule`

```json
{ "items": [
  {"weekday":"Monday","shift":"Morning"},
  {"weekday":"Wednesday","shift":"Afternoon"}
] }
```

Mudar situação de tarefa: `POST /api/tasks/{id}/state`

```json
{ "status": "Blocked" }
```

Registrar obstáculo: `POST /api/projects/{id}/obstacles`

```json
{ "description": "Falta componente para continuar os testes", "taskId": null }
```

Registrar colaboração fora da equipe (código fornecido pelo líder):

```json
{
  "projectId": null,
  "collaborationCode": "CODIGO_INFORMADO_PELO_LIDER",
  "activityDate": "2026-11-04",
  "description": "Auxílio em testes de eletrônica",
  "minutesSpent": 90
}
```

Confirmar colaboração: `POST /api/contributions/{id}/review`

```json
{ "status": "Approved" }
```
