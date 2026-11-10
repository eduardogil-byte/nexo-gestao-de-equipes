import { useMemo, useState, type FormEvent } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  ArrowLeft,
  CheckCircle,
  ClipboardList,
  FileText,
  FolderOpen,
  Link2,
  MessageCircle,
  Plus,
  Users,
  AlertTriangle,
  Upload,
  Copy,
  Clock3,
  History,
} from 'lucide-react';
import { Link, useParams, useSearchParams } from 'react-router-dom';
import { downloadFile, get, post, put, remove, upload } from '../lib/api';
import type {
  Contribution,
  Deliverable,
  FileEntry,
  LinkEntry,
  Member,
  Milestone,
  Obstacle,
  Priority,
  Project,
  ProjectSummary,
  Task,
  User,
  WorkState,
} from '../lib/types';
import {
  prettyDate,
  prettyDateTime,
  priorityLabels,
  projectStateLabels,
  taskStateLabels,
} from '../lib/types';
import {
  Empty,
  ErrorMessage,
  Field,
  Loading,
  Modal,
  PageHead,
  ProgressBar,
  Status,
  useNotice,
} from '../components/UI';
import { useAuth } from '../contexts/AuthContext';
const TABS = [
  'Visão geral',
  'Tarefas',
  'Equipe',
  'Etapas e entregas',
  'Obstáculos',
  'Documentos',
  'Colaborações',
] as const;
type Tab = (typeof TABS)[number];
export default function ProjectPage() {
  const { id = '' } = useParams();
  const [params] = useSearchParams();
  const { session, isAdmin } = useAuth();
  const qc = useQueryClient();
  const [tab, setTab] = useState<Tab>(params.has('tarefa') ? 'Tarefas' : 'Visão geral');
  const { notice, success, failure } = useNotice();
  const {
    data: project,
    isLoading,
    error,
  } = useQuery({ queryKey: ['project', id], queryFn: () => get<Project>(`/projects/${id}`) });
  const { data: summary } = useQuery({
    queryKey: ['project-summary', id],
    queryFn: () => get<ProjectSummary>(`/dashboard/projects/${id}`),
  });
  const { data: tasks = [] } = useQuery({
    queryKey: ['tasks', id],
    queryFn: () => get<Task[]>(`/projects/${id}/tasks`),
  });
  const { data: members = [] } = useQuery({
    queryKey: ['members', id],
    queryFn: () => get<Member[]>(`/projects/${id}/members`),
    retry: false,
  });
  const { data: users = [] } = useQuery({
    queryKey: ['users'],
    queryFn: () => get<User[]>('/users'),
    enabled: isAdmin,
  });
  const canLead = isAdmin || project?.leaderId === session?.user.id;
  const canWork = isAdmin || members.some((m) => m.id === session?.user.id);
  const reload = () => {
    for (const key of [
      'project',
      'project-summary',
      'projects',
      'tasks',
      'members',
      'dashboard',
      'milestones',
      'deliverables',
      'obstacles',
      'files',
      'links',
      'contributions',
      'notifications',
    ])
      qc.invalidateQueries({ queryKey: [key] });
  };
  const run = async (action: () => Promise<unknown>, message = 'Alteração salva.') => {
    try {
      await action();
      reload();
      success(message);
    } catch (e) {
      failure(e);
    }
  };
  if (isLoading) return <Loading />;
  if (error || !project)
    return <ErrorMessage error={error || new Error('Projeto não encontrado')} />;
  return (
    <>
      <div className="mb-4">
        <Link
          className="inline-flex items-center gap-1 text-sm font-semibold text-slate-500 hover:text-nexo-blue"
          to="/projetos"
        >
          <ArrowLeft size={15} /> Voltar aos projetos
        </Link>
      </div>
      <PageHead
        title={project.name}
        description={`${project.researchArea || 'Pesquisa'} · ${project.visibility === 'Private' ? 'Projeto privado' : 'Visível para o núcleo'}`}
        actions={<Status value={project.status} />}
      />
      <div className="mb-6 grid gap-4 sm:grid-cols-3">
        <div className="card p-5">
          <div className="mb-2 text-xs text-slate-500">Tarefas concluídas</div>
          <div className="mb-2 text-2xl font-extrabold">
            {summary?.tasks.percent == null ? '—' : `${summary.tasks.percent}%`}
          </div>
          <ProgressBar value={summary?.tasks.percent} />
          <div className="mt-2 text-xs text-slate-400">
            {summary?.tasks.completed ?? 0} de {summary?.tasks.total ?? 0}
          </div>
        </div>
        <div className="card p-5">
          <div className="mb-2 text-xs text-slate-500">Etapas concluídas</div>
          <div className="mb-2 text-2xl font-extrabold">
            {summary?.milestones.percent == null ? '—' : `${summary.milestones.percent}%`}
          </div>
          <ProgressBar value={summary?.milestones.percent} />
          <div className="mt-2 text-xs text-slate-400">
            {summary?.milestones.completed ?? 0} de {summary?.milestones.total ?? 0}
          </div>
        </div>
        <div className="card p-5">
          <div className="mb-2 text-xs text-slate-500">Avaliação do líder</div>
          <div className="mb-2 text-2xl font-extrabold">
            {summary?.leaderProgressPercent == null ? '—' : `${summary.leaderProgressPercent}%`}
          </div>
          <ProgressBar value={summary?.leaderProgressPercent} />
          <div className="mt-2 text-xs text-slate-400">Indicador informado manualmente</div>
        </div>
      </div>
      <div className="mb-6 overflow-x-auto border-b border-slate-200">
        <div className="flex min-w-max gap-1">
          {TABS.map((t) => (
            <button
              key={t}
              onClick={() => setTab(t)}
              className={`border-b-2 px-4 py-3 text-xs font-bold transition ${tab === t ? 'border-nexo-blue text-nexo-blue' : 'border-transparent text-slate-500 hover:text-slate-900'}`}
            >
              {t}
            </button>
          ))}
        </div>
      </div>
      {tab === 'Visão geral' && (
        <Overview
          project={project}
          id={id}
          canLead={canLead}
          canWork={canWork}
          members={members}
          tasks={tasks}
          run={run}
        />
      )}
      {tab === 'Tarefas' && (
        <ProjectTasks
          id={id}
          project={project}
          tasks={tasks}
          members={members}
          canWork={canWork}
          canLead={canLead}
          onChanged={reload}
          success={success}
          failure={failure}
          initialTask={params.get('tarefa')}
        />
      )}
      {tab === 'Equipe' && (
        <Team id={id} members={members} users={users} isAdmin={isAdmin} run={run} />
      )}
      {tab === 'Etapas e entregas' && (
        <MilestonesAndDeliverables id={id} canWork={canWork} canLead={canLead} run={run} />
      )}
      {tab === 'Obstáculos' && <Obstacles id={id} canWork={canWork} run={run} />}
      {tab === 'Documentos' && <Documents id={id} canWork={canWork} run={run} />}
      {tab === 'Colaborações' && (
        <Collaborations id={id} canLead={canLead} code={project.collaborationCode} run={run} />
      )}
      {notice}
    </>
  );
}
function Overview({
  project,
  id,
  canLead,
  canWork,
  members,
  tasks,
  run,
}: {
  project: Project;
  id: string;
  canLead: boolean;
  canWork: boolean;
  members: Member[];
  tasks: Task[];
  run: (action: () => Promise<unknown>, message?: string) => Promise<void>;
}) {
  const [percent, setPercent] = useState(String(project.leaderProgressPercent ?? ''));
  const [editing, setEditing] = useState(false);
  const [form, setForm] = useState({
    name: project.name,
    description: project.description || '',
    objectives: project.objectives || '',
    justification: project.justification || '',
    researchArea: project.researchArea || '',
    expectedResults: project.expectedResults || '',
    budget: String(project.budget ?? ''),
    plannedStart: project.plannedStart || '',
    plannedEnd: project.plannedEnd || '',
    visibility: project.visibility,
  });
  return (
    <div className="grid gap-6 xl:grid-cols-[1.7fr_1fr]">
      <div className="space-y-5">
        <section className="card p-6">
          <div className="mb-5 flex items-center justify-between">
            <h2 className="section-title">Sobre o projeto</h2>
            {canLead && (
              <button className="btn-secondary" onClick={() => setEditing(true)}>
                Editar projeto
              </button>
            )}
          </div>
          <p className="whitespace-pre-wrap text-sm leading-relaxed text-slate-600">
            {project.description || 'Sem descrição cadastrada.'}
          </p>
          <div className="mt-6 grid gap-4 sm:grid-cols-2">
            <div>
              <span className="field-label">Objetivos</span>
              <p className="whitespace-pre-wrap text-sm">{project.objectives || 'Não informado'}</p>
            </div>
            <div>
              <span className="field-label">Resultados esperados</span>
              <p className="whitespace-pre-wrap text-sm">
                {project.expectedResults || 'Não informado'}
              </p>
            </div>
            <div>
              <span className="field-label">Início previsto</span>
              <p className="text-sm">{prettyDate(project.plannedStart)}</p>
            </div>
            <div>
              <span className="field-label">Término previsto</span>
              <p className="text-sm">{prettyDate(project.plannedEnd)}</p>
            </div>
            <div>
              <span className="field-label">Orçamento estimado</span>
              <p className="text-sm">
                {project.budget == null
                  ? 'Não informado'
                  : Number(project.budget).toLocaleString('pt-BR', {
                      style: 'currency',
                      currency: 'BRL',
                    })}
              </p>
            </div>
            <div>
              <span className="field-label">Líder do projeto</span>
              <p className="text-sm">
                {members.find((m) => m.id === project.leaderId)?.fullName || 'Acesso restrito'}
              </p>
            </div>
          </div>
        </section>
        <section className="card p-6">
          <h2 className="section-title">Tarefas do projeto</h2>
          <div className="mt-4 grid gap-3 sm:grid-cols-3">
            {(
              [
                ['NotStarted', 'Não iniciadas'],
                ['InProgress', 'Em andamento'],
                ['Completed', 'Concluídas'],
              ] as const
            ).map(([key, label]) => (
              <div key={key} className="rounded-xl bg-slate-50 p-4">
                <div className="text-2xl font-bold">
                  {tasks.filter((t) => t.status === key).length}
                </div>
                <div className="mt-1 text-xs text-slate-500">{label}</div>
              </div>
            ))}
          </div>
        </section>
      </div>
      <div className="space-y-5">
        <section className="card p-6">
          <h2 className="section-title">Progresso e situação</h2>
          <p className="mt-2 text-xs text-slate-500">
            Os três indicadores de avanço são independentes.
          </p>
          {canLead && (
            <form
              className="mt-5 flex gap-2"
              onSubmit={(e) => {
                e.preventDefault();
                void run(
                  () => put(`/projects/${id}/progress`, { percent: Number(percent) }),
                  'Progresso do líder atualizado.',
                );
              }}
            >
              <input
                className="field"
                type="number"
                min="0"
                max="100"
                placeholder="0–100%"
                required
                value={percent}
                onChange={(e) => setPercent(e.target.value)}
              />
              <button className="btn-primary">Atualizar</button>
            </form>
          )}
        </section>
        <section className="card p-6">
          <h2 className="section-title">Equipe</h2>
          {!canWork ? (
            <p className="mt-4 text-xs text-slate-500">
              A lista de integrantes é restrita à equipe.
            </p>
          ) : (
            <div className="mt-4 space-y-3">
              {members.map((m) => (
                <div key={m.id} className="flex items-center gap-3">
                  <div className="flex h-9 w-9 items-center justify-center rounded-full bg-blue-50 font-bold text-nexo-blue">
                    {m.fullName[0]}
                  </div>
                  <div>
                    <p className="text-sm font-semibold">
                      {m.fullName}{' '}
                      {m.isLeader && (
                        <span className="text-xs font-normal text-nexo-teal">· Líder</span>
                      )}
                    </p>
                    <p className="text-xs text-slate-500">{m.specialty || 'Integrante'}</p>
                  </div>
                </div>
              ))}
            </div>
          )}
        </section>
        <section className="card p-6">
          <h2 className="section-title">Controle do projeto</h2>
          <p className="mt-1 text-xs text-slate-500">
            Situação atual: {projectStateLabels[project.status]}
          </p>
          <div className="mt-3 flex flex-col gap-2">
            {canLead && project.status !== 'Completed' && project.status !== 'Cancelled' && (
              <p className="text-xs text-slate-500">
                Somente a coordenação pode alterar o estado geral do projeto.
              </p>
            )}
          </div>
        </section>
      </div>
      {editing && (
        <Modal title="Editar informações do projeto" onClose={() => setEditing(false)} wide>
          <form
            className="grid gap-4 sm:grid-cols-2"
            onSubmit={async (e: FormEvent) => {
              e.preventDefault();
              await run(
                () =>
                  put(`/projects/${id}`, {
                    ...form,
                    budget: form.budget ? Number(form.budget) : null,
                    plannedStart: form.plannedStart || null,
                    plannedEnd: form.plannedEnd || null,
                    leaderId: project.leaderId,
                  }),
                'Projeto atualizado.',
              );
              setEditing(false);
            }}
          >
            <div className="sm:col-span-2">
              <Field label="Nome">
                <input
                  className="field"
                  value={form.name}
                  required
                  minLength={3}
                  onChange={(e) => setForm({ ...form, name: e.target.value })}
                />
              </Field>
            </div>
            {(['description', 'objectives', 'justification', 'expectedResults'] as const).map(
              (k) => (
                <div className="sm:col-span-2" key={k}>
                  <Field
                    label={
                      {
                        description: 'Descrição',
                        objectives: 'Objetivos',
                        justification: 'Justificativa',
                        expectedResults: 'Resultados esperados',
                      }[k]
                    }
                  >
                    <textarea
                      className="field"
                      rows={2}
                      value={form[k]}
                      onChange={(e) => setForm({ ...form, [k]: e.target.value })}
                    />
                  </Field>
                </div>
              ),
            )}
            <Field label="Área de pesquisa">
              <input
                className="field"
                value={form.researchArea}
                onChange={(e) => setForm({ ...form, researchArea: e.target.value })}
              />
            </Field>
            <Field label="Orçamento">
              <input
                type="number"
                min="0"
                step="0.01"
                className="field"
                value={form.budget}
                onChange={(e) => setForm({ ...form, budget: e.target.value })}
              />
            </Field>
            <Field label="Início previsto">
              <input
                type="date"
                className="field"
                value={form.plannedStart}
                onChange={(e) => setForm({ ...form, plannedStart: e.target.value })}
              />
            </Field>
            <Field label="Término previsto">
              <input
                type="date"
                className="field"
                value={form.plannedEnd}
                onChange={(e) => setForm({ ...form, plannedEnd: e.target.value })}
              />
            </Field>
            <Field label="Visibilidade">
              <select
                className="field"
                value={form.visibility}
                onChange={(e) =>
                  setForm({ ...form, visibility: e.target.value as Project['visibility'] })
                }
              >
                <option value="Private">Privado</option>
                <option value="PublicInsideNucleus">Visível no núcleo</option>
              </select>
            </Field>
            <div className="sm:col-span-2 flex justify-end gap-2">
              <button className="btn-secondary" type="button" onClick={() => setEditing(false)}>
                Cancelar
              </button>
              <button className="btn-primary">Salvar</button>
            </div>
          </form>
        </Modal>
      )}
    </div>
  );
}
function ProjectTasks({
  id,
  project,
  tasks,
  members,
  canWork,
  canLead,
  onChanged,
  success,
  failure,
  initialTask,
}: {
  id: string;
  project: Project;
  tasks: Task[];
  members: Member[];
  canWork: boolean;
  canLead: boolean;
  onChanged: () => void;
  success: (m: string) => void;
  failure: (e: unknown) => void;
  initialTask: string | null;
}) {
  const [show, setShow] = useState(false);
  const [selected, setSelected] = useState<string | null>(initialTask);
  const [newForm, setNewForm] = useState({
    title: '',
    description: '',
    priority: 'Normal' as Priority,
    dueDate: '',
    assigneeIds: [] as string[],
  });
  const { data: detail, isLoading } = useQuery({
    queryKey: ['task', selected],
    queryFn: () => get<Task>(`/tasks/${selected}`),
    enabled: !!selected,
  });
  const { data: comments = [] } = useQuery({
    queryKey: ['comments', selected],
    queryFn: () =>
      get<{ id: string; text: string; createdAtUtc: string }[]>(`/tasks/${selected}/comments`),
    enabled: !!selected,
  });
  const [comment, setComment] = useState('');
  const [editing, setEditing] = useState(false);
  const [edit, setEdit] = useState({
    title: '',
    description: '',
    priority: 'Normal' as Priority,
    dueDate: '',
  });
  const [forceAll, setForceAll] = useState(false);
  const qc = useQueryClient();
  const run = async (fn: () => Promise<unknown>, msg: string) => {
    try {
      await fn();
      onChanged();
      qc.invalidateQueries({ queryKey: ['task', selected] });
      qc.invalidateQueries({ queryKey: ['comments', selected] });
      success(msg);
    } catch (e) {
      failure(e);
    }
  };
  return (
    <>
      <div className="mb-5 flex flex-wrap items-center justify-between gap-3">
        <div>
          <h2 className="section-title">Atividades planejadas</h2>
          <p className="text-xs text-slate-500">{tasks.length} tarefas neste projeto</p>
        </div>
        {canWork && project.status !== 'Completed' && project.status !== 'Cancelled' && (
          <button className="btn-primary" onClick={() => setShow(true)}>
            <Plus size={16} /> Nova tarefa
          </button>
        )}
      </div>
      <div className="grid gap-4 xl:grid-cols-3">
        {(['NotStarted', 'InProgress', 'Completed'] as const).map((status) => (
          <div key={status} className="rounded-2xl border border-slate-200 bg-slate-100/60 p-3">
            <div className="mb-3 flex items-center justify-between px-2">
              <h3 className="text-sm font-bold">{taskStateLabels[status]}</h3>
              <span className="rounded-full bg-white px-2 py-0.5 text-xs font-semibold">
                {tasks.filter((t) => t.status === status).length}
              </span>
            </div>
            <div className="space-y-3">
              {tasks
                .filter((t) => t.status === status)
                .map((t) => (
                  <button
                    key={t.id}
                    onClick={() => setSelected(t.id)}
                    className="w-full rounded-xl border border-slate-200 bg-white p-4 text-left transition hover:border-blue-300"
                  >
                    <div className="text-sm font-bold">{t.title}</div>
                    <p className="mt-2 line-clamp-2 text-xs text-slate-500">
                      {t.description || 'Sem descrição'}
                    </p>
                    <div className="mt-4 flex justify-between gap-2">
                      <Status value={t.priority} />
                      <span className="text-xs text-slate-500">{prettyDate(t.dueDate)}</span>
                    </div>
                    {t.needsRedistribution && (
                      <div className="mt-2 text-xs font-semibold text-orange-600">
                        Aguardando redistribuição
                      </div>
                    )}
                  </button>
                ))}
              {tasks.filter((t) => t.status === status).length === 0 && (
                <div className="rounded-xl border border-dashed border-slate-200 px-3 py-6 text-center text-xs text-slate-400">
                  Nenhuma tarefa
                </div>
              )}
            </div>
          </div>
        ))}
      </div>
      {tasks.filter((t) => t.status === 'Blocked' || t.status === 'Cancelled').length > 0 && (
        <div className="card mt-4 p-4">
          <h3 className="mb-3 font-semibold">Bloqueadas e canceladas</h3>
          <div className="flex flex-wrap gap-2">
            {tasks
              .filter((t) => t.status === 'Blocked' || t.status === 'Cancelled')
              .map((t) => (
                <button key={t.id} onClick={() => setSelected(t.id)} className="btn-secondary">
                  {t.title} <Status value={t.status} />
                </button>
              ))}
          </div>
        </div>
      )}
      {show && (
        <Modal title="Criar tarefa" onClose={() => setShow(false)}>
          <form
            className="space-y-4"
            onSubmit={async (e: FormEvent) => {
              e.preventDefault();
              try {
                await post(`/projects/${id}/tasks`, {
                  ...newForm,
                  dueDate: newForm.dueDate || null,
                  assigneeIds: newForm.assigneeIds,
                });
                setShow(false);
                onChanged();
                success('Tarefa criada.');
              } catch (e) {
                failure(e);
              }
            }}
          >
            <Field label="Título *">
              <input
                required
                minLength={3}
                className="field"
                value={newForm.title}
                onChange={(e) => setNewForm({ ...newForm, title: e.target.value })}
              />
            </Field>
            <Field label="Descrição">
              <textarea
                className="field"
                rows={3}
                value={newForm.description}
                onChange={(e) => setNewForm({ ...newForm, description: e.target.value })}
              />
            </Field>
            <div className="grid grid-cols-2 gap-3">
              <Field label="Prioridade">
                <select
                  className="field"
                  value={newForm.priority}
                  onChange={(e) => setNewForm({ ...newForm, priority: e.target.value as Priority })}
                >
                  {Object.entries(priorityLabels).map(([k, v]) => (
                    <option value={k} key={k}>
                      {v}
                    </option>
                  ))}
                </select>
              </Field>
              <Field label="Prazo (opcional)">
                <input
                  type="date"
                  className="field"
                  value={newForm.dueDate}
                  onChange={(e) => setNewForm({ ...newForm, dueDate: e.target.value })}
                />
              </Field>
            </div>
            <Field label="Responsáveis (selecione um ou mais)">
              <div className="max-h-40 space-y-2 overflow-auto rounded-xl border p-3">
                {members.map((m) => (
                  <label key={m.id} className="flex items-center gap-2 text-sm">
                    <input
                      type="checkbox"
                      checked={newForm.assigneeIds.includes(m.id)}
                      onChange={(e) =>
                        setNewForm({
                          ...newForm,
                          assigneeIds: e.target.checked
                            ? [...newForm.assigneeIds, m.id]
                            : newForm.assigneeIds.filter((v) => v !== m.id),
                        })
                      }
                    />
                    {m.fullName}
                  </label>
                ))}
              </div>
            </Field>
            <p className="text-xs text-slate-500">
              Se não selecionar ninguém, o backend atribuirá a tarefa a você quando permitido.
              Atribuições de colegas podem exigir aceite.
            </p>
            <div className="flex justify-end gap-2">
              <button className="btn-secondary" type="button" onClick={() => setShow(false)}>
                Cancelar
              </button>
              <button className="btn-primary">Criar tarefa</button>
            </div>
          </form>
        </Modal>
      )}
      {selected && (
        <Modal
          title="Detalhes da tarefa"
          onClose={() => {
            setSelected(null);
            setEditing(false);
          }}
          wide
        >
          {isLoading ? (
            <Loading />
          ) : detail ? (
            <div className="space-y-5">
              <div className="flex flex-wrap items-center justify-between gap-3">
                <div>
                  <h3 className="text-xl font-bold">{detail.title}</h3>
                  <p className="mt-1 text-sm text-slate-500">
                    {detail.description || 'Sem descrição'}
                  </p>
                </div>
                <Status value={detail.status} />
              </div>
              <div className="grid gap-3 sm:grid-cols-3">
                <div className="rounded-xl bg-slate-50 p-3">
                  <span className="field-label">Prioridade</span>
                  <Status value={detail.priority} />
                </div>
                <div className="rounded-xl bg-slate-50 p-3">
                  <span className="field-label">Prazo</span>
                  <div className="text-sm">{prettyDate(detail.dueDate)}</div>
                </div>
                <div className="rounded-xl bg-slate-50 p-3">
                  <span className="field-label">Responsáveis</span>
                  <div className="text-sm">{detail.assignees?.length || 0}</div>
                </div>
              </div>
              <section>
                <h4 className="mb-3 font-bold">Participantes</h4>
                <div className="space-y-2">
                  {detail.assignees?.map((a) => (
                    <div
                      key={a.userId}
                      className="flex items-center justify-between gap-2 rounded-xl border p-3 text-sm"
                    >
                      <div>
                        <span className="font-semibold">
                          {members.find((m) => m.id === a.userId)?.fullName || a.userId}
                        </span>
                        <div className="text-xs text-slate-500">
                          {!a.accepted
                            ? 'Aguardando aceite'
                            : a.completedAtUtc
                              ? 'Participação concluída'
                              : 'Participação em andamento'}
                        </div>
                      </div>
                      {a.accepted && a.completedAtUtc && (
                        <CheckCircle size={17} className="text-teal-600" />
                      )}
                    </div>
                  ))}
                </div>
              </section>
              {canWork && detail.status !== 'Cancelled' && (
                <div className="flex flex-wrap gap-2">
                  {detail.status !== 'Completed' && (
                    <>
                      <button
                        className="btn-primary"
                        onClick={() => {
                          if (
                            !forceAll ||
                            window.confirm('Finalizar a tarefa para todos os responsáveis?')
                          )
                            void run(
                              () => post(`/tasks/${selected}/complete`, { forceAll }),
                              forceAll ? 'Tarefa finalizada.' : 'Sua participação foi concluída.',
                            );
                        }}
                      >
                        Concluir {forceAll ? 'tarefa inteira' : 'minha parte'}
                      </button>
                      <label className="flex items-center gap-2 text-xs">
                        <input
                          type="checkbox"
                          checked={forceAll}
                          onChange={(e) => setForceAll(e.target.checked)}
                        />{' '}
                        Finalizar para todos
                      </label>
                    </>
                  )}
                  {detail.status !== 'Completed' && (
                    <select
                      className="field w-auto"
                      value={detail.status}
                      onChange={(e) =>
                        void run(
                          () => post(`/tasks/${selected}/state`, { status: e.target.value }),
                          'Situação atualizada.',
                        )
                      }
                    >
                      <option value="NotStarted">Não iniciada</option>
                      <option value="InProgress">Em andamento</option>
                      <option value="Blocked">Bloqueada</option>
                      {canLead && <option value="Cancelled">Cancelada</option>}
                    </select>
                  )}
                  {canLead && detail.status === 'Completed' && (
                    <button
                      className="btn-secondary"
                      onClick={() =>
                        void run(() => post(`/tasks/${selected}/reopen`), 'Tarefa reaberta.')
                      }
                    >
                      Reabrir tarefa
                    </button>
                  )}
                  <button
                    className="btn-secondary"
                    onClick={() => {
                      setEdit({
                        title: detail.title,
                        description: detail.description || '',
                        priority: detail.priority,
                        dueDate: detail.dueDate || '',
                      });
                      setEditing(!editing);
                    }}
                  >
                    Editar dados
                  </button>
                </div>
              )}
              {editing && (
                <form
                  className="rounded-xl bg-slate-50 p-4"
                  onSubmit={async (e) => {
                    e.preventDefault();
                    await run(
                      () => put(`/tasks/${selected}`, { ...edit, dueDate: edit.dueDate || null }),
                      'Tarefa atualizada.',
                    );
                    setEditing(false);
                  }}
                >
                  <h4 className="mb-3 font-bold">Editar tarefa</h4>
                  <div className="space-y-3">
                    <Field label="Título">
                      <input
                        className="field"
                        required
                        minLength={3}
                        value={edit.title}
                        onChange={(e) => setEdit({ ...edit, title: e.target.value })}
                      />
                    </Field>
                    <Field label="Descrição">
                      <textarea
                        className="field"
                        value={edit.description}
                        onChange={(e) => setEdit({ ...edit, description: e.target.value })}
                      />
                    </Field>
                    <div className="grid grid-cols-2 gap-2">
                      <Field label="Prioridade">
                        <select
                          className="field"
                          value={edit.priority}
                          onChange={(e) =>
                            setEdit({ ...edit, priority: e.target.value as Priority })
                          }
                        >
                          {Object.entries(priorityLabels).map(([k, v]) => (
                            <option key={k} value={k}>
                              {v}
                            </option>
                          ))}
                        </select>
                      </Field>
                      <Field label="Prazo">
                        <input
                          type="date"
                          className="field"
                          value={edit.dueDate}
                          onChange={(e) => setEdit({ ...edit, dueDate: e.target.value })}
                        />
                      </Field>
                    </div>
                    <button className="btn-primary">Salvar alterações</button>
                  </div>
                </form>
              )}
              <div className="border-t pt-4">
                <h4 className="mb-3 flex items-center gap-2 font-bold">
                  <MessageCircle size={17} /> Comentários
                </h4>
                {comments.map((c) => (
                  <div key={c.id} className="mb-2 rounded-lg bg-slate-50 p-3 text-sm">
                    <p>{c.text}</p>
                    <span className="text-xs text-slate-400">{prettyDateTime(c.createdAtUtc)}</span>
                  </div>
                ))}
                {canWork && (
                  <form
                    onSubmit={async (e) => {
                      e.preventDefault();
                      await run(
                        () => post(`/tasks/${selected}/comments`, { text: comment }),
                        'Comentário enviado.',
                      );
                      setComment('');
                    }}
                    className="mt-3 flex gap-2"
                  >
                    <input
                      className="field"
                      minLength={1}
                      maxLength={2000}
                      required
                      placeholder="Escreva um comentário..."
                      value={comment}
                      onChange={(e) => setComment(e.target.value)}
                    />
                    <button className="btn-primary">Enviar</button>
                  </form>
                )}
              </div>
            </div>
          ) : (
            <ErrorMessage error={new Error('Tarefa indisponível.')} />
          )}
        </Modal>
      )}
    </>
  );
}
function Team({
  id,
  members,
  users,
  isAdmin,
  run,
}: {
  id: string;
  members: Member[];
  users: User[];
  isAdmin: boolean;
  run: (f: () => Promise<unknown>, m?: string) => Promise<void>;
}) {
  const [newMember, setNewMember] = useState('');
  return (
    <section className="card p-6">
      <div className="mb-5 flex flex-wrap items-center justify-between gap-3">
        <h2 className="section-title">Equipe do projeto</h2>
        <span className="text-sm text-slate-500">{members.length} integrantes</span>
      </div>
      {isAdmin && (
        <form
          className="mb-6 flex flex-wrap gap-2"
          onSubmit={(e) => {
            e.preventDefault();
            void run(
              () => post(`/projects/${id}/members`, { userId: newMember }),
              'Integrante adicionado.',
            );
            setNewMember('');
          }}
        >
          <select
            className="field min-w-[230px] flex-1"
            required
            value={newMember}
            onChange={(e) => setNewMember(e.target.value)}
          >
            <option value="">Adicionar integrante aprovado...</option>
            {users
              .filter((u) => u.approval === 'Approved' && !members.some((m) => m.id === u.id))
              .map((u) => (
                <option value={u.id} key={u.id}>
                  {u.fullName} · {u.specialty || 'Integrante'}
                </option>
              ))}
          </select>
          <button className="btn-primary">
            <Plus size={16} /> Adicionar
          </button>
        </form>
      )}
      {members.length === 0 && (
        <Empty text="Sem acesso à lista de integrantes ou nenhuma pessoa incluída." />
      )}
      <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-3">
        {members.map((m) => (
          <div key={m.id} className="flex items-center gap-3 rounded-xl border p-4">
            <div className="flex h-11 w-11 items-center justify-center rounded-full bg-blue-50 text-lg font-bold text-blue-600">
              {m.fullName[0]}
            </div>
            <div className="min-w-0 flex-1">
              <div className="truncate text-sm font-bold">{m.fullName}</div>
              <div className="text-xs text-slate-500">
                {m.isLeader ? 'Líder · ' : ''}
                {m.specialty || 'Integrante'}
              </div>
            </div>
            {isAdmin && !m.isLeader && (
              <button
                className="text-xs font-semibold text-red-600"
                onClick={() => {
                  if (
                    window.confirm(
                      `Remover ${m.fullName} da equipe? As tarefas pendentes ficarão para redistribuição.`,
                    )
                  )
                    void run(
                      () => remove(`/projects/${id}/members/${m.id}`),
                      'Integrante removido.',
                    );
                }}
              >
                Remover
              </button>
            )}
          </div>
        ))}
      </div>
    </section>
  );
}
function MilestonesAndDeliverables({
  id,
  canLead,
  canWork,
  run,
}: {
  id: string;
  canLead: boolean;
  canWork: boolean;
  run: (f: () => Promise<unknown>, m?: string) => Promise<void>;
}) {
  const { data: milestones = [] } = useQuery({
    queryKey: ['milestones', id],
    queryFn: () => get<Milestone[]>(`/projects/${id}/milestones`),
  });
  const { data: deliverables = [] } = useQuery({
    queryKey: ['deliverables', id],
    queryFn: () => get<Deliverable[]>(`/projects/${id}/deliverables`),
  });
  const [milestone, setMilestone] = useState({ title: '', description: '', dueDate: '' });
  const [deliverable, setDeliverable] = useState({
    title: '',
    kind: 'Relatório',
    description: '',
    externalUrl: '',
  });
  return (
    <div className="grid gap-5 xl:grid-cols-2">
      <section className="card p-6">
        <h2 className="section-title">Etapas e marcos</h2>
        {canLead && (
          <form
            className="my-4 flex gap-2"
            onSubmit={(e) => {
              e.preventDefault();
              void run(
                () =>
                  post(`/projects/${id}/milestones`, {
                    ...milestone,
                    dueDate: milestone.dueDate || null,
                  }),
                'Etapa criada.',
              );
              setMilestone({ title: '', description: '', dueDate: '' });
            }}
          >
            <input
              className="field"
              required
              minLength={3}
              placeholder="Nova etapa..."
              value={milestone.title}
              onChange={(e) => setMilestone({ ...milestone, title: e.target.value })}
            />
            <button className="btn-primary">
              <Plus size={16} />
            </button>
          </form>
        )}
        {milestones.length === 0 && (
          <Empty text="Nenhuma etapa cadastrada. Etapas são opcionais." />
        )}
        <div className="mt-4 space-y-3">
          {milestones.map((m) => (
            <div className="rounded-xl border p-4" key={m.id}>
              <div className="flex justify-between gap-2">
                <div className="font-semibold">{m.title}</div>
                <Status value={m.status} />
              </div>
              <div className="mt-1 text-xs text-slate-500">Prazo: {prettyDate(m.dueDate)}</div>
              {canLead && (
                <select
                  className="field mt-3"
                  value={m.status}
                  onChange={(e) =>
                    void run(
                      () => put(`/milestones/${m.id}/status`, { status: e.target.value }),
                      'Etapa atualizada.',
                    )
                  }
                >
                  <option value="NotStarted">Não iniciada</option>
                  <option value="InProgress">Em andamento</option>
                  <option value="Completed">Concluída</option>
                  <option value="Blocked">Bloqueada</option>
                  <option value="Cancelled">Cancelada</option>
                </select>
              )}
            </div>
          ))}
        </div>
      </section>
      <section className="card p-6">
        <h2 className="section-title">Entregas e resultados</h2>
        {canWork && (
          <form
            className="my-4 space-y-2 rounded-xl bg-slate-50 p-4"
            onSubmit={(e) => {
              e.preventDefault();
              void run(
                () =>
                  post(`/projects/${id}/deliverables`, {
                    ...deliverable,
                    externalUrl: deliverable.externalUrl || null,
                  }),
                'Entrega registrada.',
              );
              setDeliverable({ title: '', kind: 'Relatório', description: '', externalUrl: '' });
            }}
          >
            <input
              className="field"
              required
              minLength={3}
              placeholder="Título da entrega"
              value={deliverable.title}
              onChange={(e) => setDeliverable({ ...deliverable, title: e.target.value })}
            />
            <div className="grid grid-cols-2 gap-2">
              <select
                className="field"
                value={deliverable.kind}
                onChange={(e) => setDeliverable({ ...deliverable, kind: e.target.value })}
              >
                {[
                  'Relatório',
                  'Artigo',
                  'Software',
                  'Protótipo',
                  'Experimento',
                  'Apresentação',
                  'Documentação',
                  'Outro',
                ].map((x) => (
                  <option key={x}>{x}</option>
                ))}
              </select>
              <input
                type="url"
                className="field"
                placeholder="https://... (opcional)"
                value={deliverable.externalUrl}
                onChange={(e) => setDeliverable({ ...deliverable, externalUrl: e.target.value })}
              />
            </div>
            <button className="btn-primary">
              <Plus size={15} /> Registrar entrega
            </button>
          </form>
        )}
        {deliverables.length === 0 && <Empty text="Nenhuma entrega cadastrada." />}
        <div className="mt-4 space-y-3">
          {deliverables.map((d) => (
            <div className="flex items-start gap-3 rounded-xl border p-4" key={d.id}>
              <FileText size={19} className="text-nexo-blue" />
              <div className="flex-1">
                <div className="font-bold">{d.title}</div>
                <div className="text-xs text-slate-500">{d.kind}</div>
                {d.externalUrl && (
                  <a
                    className="mt-1 block text-xs text-blue-600 underline"
                    rel="noopener noreferrer"
                    target="_blank"
                    href={d.externalUrl}
                  >
                    Abrir material
                  </a>
                )}
              </div>
              {d.status === 'Completed' ? (
                <Status value="Completed" />
              ) : (
                canWork && (
                  <button
                    className="btn-secondary text-xs"
                    onClick={() =>
                      void run(() => post(`/deliverables/${d.id}/complete`), 'Entrega concluída.')
                    }
                  >
                    Concluir
                  </button>
                )
              )}
            </div>
          ))}
        </div>
      </section>
    </div>
  );
}
function Obstacles({
  id,
  canWork,
  run,
}: {
  id: string;
  canWork: boolean;
  run: (f: () => Promise<unknown>, m?: string) => Promise<void>;
}) {
  const { data: obstacles = [] } = useQuery({
    queryKey: ['obstacles', id],
    queryFn: () => get<Obstacle[]>(`/projects/${id}/obstacles`),
    retry: false,
  });
  const [description, setDescription] = useState('');
  return (
    <section className="card max-w-4xl p-6">
      <h2 className="section-title">Obstáculos registrados</h2>
      <p className="mt-1 text-xs text-slate-500">
        Registrar um obstáculo não muda automaticamente o estado das tarefas.
      </p>
      {canWork && (
        <form
          className="my-5 flex gap-2"
          onSubmit={(e) => {
            e.preventDefault();
            void run(
              () => post(`/projects/${id}/obstacles`, { description, taskId: null }),
              'Obstáculo registrado.',
            );
            setDescription('');
          }}
        >
          <input
            className="field"
            required
            minLength={3}
            value={description}
            placeholder="Descreva o obstáculo encontrado..."
            onChange={(e) => setDescription(e.target.value)}
          />
          <button className="btn-primary">Registrar</button>
        </form>
      )}
      {obstacles.length === 0 && <Empty text="Nenhum obstáculo registrado." />}
      <div className="space-y-3">
        {obstacles.map((o) => (
          <div key={o.id} className="flex items-start gap-3 rounded-xl border p-4">
            <AlertTriangle size={19} className="mt-0.5 text-amber-600" />
            <div className="flex-1">
              <div className="text-sm font-semibold">{o.description}</div>
              <div className="mt-2">
                <Status value={o.status} />
              </div>
              {o.resolution && (
                <p className="mt-2 text-sm text-teal-700">Solução: {o.resolution}</p>
              )}
            </div>
            {o.status === 'Open' && canWork && (
              <button
                className="btn-secondary"
                onClick={() => {
                  const resolution = window.prompt('Descreva a solução do obstáculo:');
                  if (resolution?.trim())
                    void run(
                      () => post(`/obstacles/${o.id}/resolve`, { resolution }),
                      'Obstáculo resolvido.',
                    );
                }}
              >
                Resolver
              </button>
            )}
          </div>
        ))}
      </div>
    </section>
  );
}
function Documents({
  id,
  canWork,
  run,
}: {
  id: string;
  canWork: boolean;
  run: (f: () => Promise<unknown>, m?: string) => Promise<void>;
}) {
  const { data: files = [] } = useQuery({
    queryKey: ['files', id],
    queryFn: () => get<FileEntry[]>(`/projects/${id}/files`),
    enabled: canWork,
  });
  const { data: links = [] } = useQuery({
    queryKey: ['links', id],
    queryFn: () => get<LinkEntry[]>(`/projects/${id}/links`),
    enabled: canWork,
  });
  const [file, setFile] = useState<File | null>(null);
  const [link, setLink] = useState({ title: '', url: '', taskId: null });
  if (!canWork)
    return <Empty text="Os arquivos deste projeto são restritos aos integrantes autorizados." />;
  return (
    <div className="grid gap-5 xl:grid-cols-2">
      <section className="card p-6">
        <h2 className="section-title">Arquivos enviados</h2>
        <form
          className="my-5 flex flex-wrap gap-2"
          onSubmit={(e) => {
            e.preventDefault();
            if (!file) return;
            const fd = new FormData();
            fd.set('file', file);
            void run(() => upload(`/projects/${id}/files`, fd), 'Arquivo enviado.');
            setFile(null);
          }}
        >
          <input
            className="field flex-1"
            type="file"
            accept=".pdf,.txt,.md,.csv,.png,.jpg,.jpeg,.docx,.xlsx,.pptx,.zip"
            onChange={(e) => setFile(e.target.files?.[0] || null)}
          />
          <button disabled={!file} className="btn-primary">
            <Upload size={16} /> Enviar
          </button>
        </form>
        <p className="mb-5 text-xs text-slate-500">Até 10 MB por arquivo, conforme o backend.</p>
        {files.length === 0 && <Empty text="Nenhum arquivo enviado." />}
        {files.map((f) => (
          <button
            key={f.id}
            className="mb-2 flex w-full items-center gap-3 rounded-xl border p-3 text-left hover:bg-slate-50"
            onClick={() => void run(() => downloadFile(f.id, f.originalName), 'Download iniciado.')}
          >
            <FolderOpen size={19} className="text-nexo-blue" />
            <span className="flex-1 truncate text-sm font-semibold">{f.originalName}</span>
            <span className="text-xs text-slate-500">{Math.ceil(f.length / 1024)} KB</span>
          </button>
        ))}
      </section>
      <section className="card p-6">
        <h2 className="section-title">Links externos</h2>
        <form
          className="my-5 space-y-2"
          onSubmit={(e) => {
            e.preventDefault();
            void run(() => post(`/projects/${id}/links`, link), 'Link adicionado.');
            setLink({ title: '', url: '', taskId: null });
          }}
        >
          <input
            className="field"
            required
            minLength={3}
            placeholder="Título do material"
            value={link.title}
            onChange={(e) => setLink({ ...link, title: e.target.value })}
          />
          <input
            className="field"
            required
            type="url"
            placeholder="https://..."
            value={link.url}
            onChange={(e) => setLink({ ...link, url: e.target.value })}
          />
          <button className="btn-primary">
            <Link2 size={16} /> Adicionar link
          </button>
        </form>
        {links.length === 0 && <Empty text="Nenhum link cadastrado." />}
        {links.map((l) => (
          <a
            href={l.url}
            target="_blank"
            rel="noopener noreferrer"
            key={l.id}
            className="mb-2 flex items-center gap-2 rounded-xl border p-3 text-sm font-semibold text-blue-600 hover:bg-slate-50"
          >
            <Link2 size={17} />
            {l.title}
          </a>
        ))}
      </section>
    </div>
  );
}
function Collaborations({
  id,
  canLead,
  code,
  run,
}: {
  id: string;
  canLead: boolean;
  code?: string | null;
  run: (f: () => Promise<unknown>, m?: string) => Promise<void>;
}) {
  const { data: contributions = [] } = useQuery({
    queryKey: ['contributions', id],
    queryFn: () => get<Contribution[]>(`/projects/${id}/contributions`),
    enabled: canLead,
  });
  if (!canLead)
    return (
      <Empty text="A revisão das colaborações externas é restrita ao líder e à coordenação." />
    );
  return (
    <section className="card max-w-4xl p-6">
      <h2 className="section-title">Colaborações externas</h2>
      {code && (
        <div className="my-4 rounded-xl border bg-slate-50 p-4">
          <span className="field-label">
            Código de colaboração (compartilhe somente com pessoas autorizadas)
          </span>
          <div className="flex items-center gap-2">
            <code className="flex-1 break-all text-sm">{code}</code>
            <button
              className="btn-secondary"
              onClick={() => void navigator.clipboard.writeText(code)}
            >
              <Copy size={16} />
            </button>
          </div>
        </div>
      )}
      {contributions.length === 0 && <Empty text="Nenhuma contribuição externa enviada." />}
      <div className="space-y-3">
        {contributions.map((c) => (
          <div key={c.id} className="rounded-xl border p-4">
            <div className="flex flex-wrap justify-between gap-3">
              <div>
                <p className="text-sm font-semibold">{c.description}</p>
                <p className="mt-1 text-xs text-slate-500">
                  Data: {prettyDate(c.activityDate)} · {c.minutesSpent} minutos
                </p>
              </div>
              <Status value={c.status} />
            </div>
            {c.status === 'Pending' && (
              <div className="mt-3 flex gap-2">
                <button
                  className="btn-primary"
                  onClick={() =>
                    void run(
                      () => post(`/contributions/${c.id}/review`, { status: 'Approved' }),
                      'Colaboração confirmada.',
                    )
                  }
                >
                  Confirmar
                </button>
                <button
                  className="btn-secondary"
                  onClick={() =>
                    void run(
                      () => post(`/contributions/${c.id}/review`, { status: 'Rejected' }),
                      'Colaboração recusada.',
                    )
                  }
                >
                  Recusar
                </button>
              </div>
            )}
          </div>
        ))}
      </div>
    </section>
  );
}
