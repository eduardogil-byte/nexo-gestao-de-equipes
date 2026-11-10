import { useMemo, useState, type FormEvent } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { Calendar, Clock3, Edit3, Plus, Send, AlertCircle } from 'lucide-react';
import { get, post, put } from '../lib/api';
import {
  categoryLabels,
  localToday,
  prettyDate,
  prettyDateTime,
  type Activity,
  type ActivityCategory,
  type Project,
  type Task,
} from '../lib/types';
import { Empty, ErrorMessage, Field, Loading, Modal, PageHead, useNotice } from '../components/UI';
const defaultForm = () => ({
  activityDate: localToday(),
  projectId: '',
  taskId: '',
  category: 'Development' as ActivityCategory,
  description: '',
  hours: '0',
  minutes: '0',
  partialResult: '',
  noActivity: false,
});
export default function ActivitiesPage() {
  const [days, setDays] = useState('30');
  const [open, setOpen] = useState(false);
  const [editing, setEditing] = useState<string | null>(null);
  const [form, setForm] = useState(defaultForm);
  const qc = useQueryClient();
  const { notice, success, failure } = useNotice();
  const from = useMemo(() => {
    const d = new Date();
    d.setDate(d.getDate() - Number(days));
    const local = new Date(d.getTime() - d.getTimezoneOffset() * 60000);
    return local.toISOString().slice(0, 10);
  }, [days]);
  const {
    data: activities = [],
    isLoading,
    error,
  } = useQuery({
    queryKey: ['activities', days],
    queryFn: () => get<Activity[]>(`/activities?from=${from}&to=${localToday()}`),
  });
  const { data: missing } = useQuery({
    queryKey: ['missing-days'],
    queryFn: () => get<{ missing: string[] }>('/activities/missing-days'),
  });
  const { data: projects = [] } = useQuery({
    queryKey: ['projects'],
    queryFn: () => get<Project[]>('/projects'),
  });
  const { data: projectTasks = [] } = useQuery({
    queryKey: ['tasks', form.projectId],
    queryFn: () => get<Task[]>(`/projects/${form.projectId}/tasks`),
    enabled: !!form.projectId,
  });
  const entries = useMemo(
    () => [...activities].sort((a, b) => b.activityDate.localeCompare(a.activityDate)),
    [activities],
  );
  const save = async (e: FormEvent) => {
    e.preventDefault();
    const body = {
      activityDate: form.activityDate,
      category: form.category,
      description: form.noActivity ? '' : form.description,
      minutesSpent: form.noActivity ? 0 : Number(form.hours) * 60 + Number(form.minutes),
      partialResult: form.partialResult || null,
      noActivity: form.noActivity,
    };
    try {
      if (editing) await put(`/activities/${editing}`, body);
      else
        await post('/activities', {
          ...body,
          projectId: form.noActivity ? null : form.projectId || null,
          taskId: form.noActivity ? null : form.taskId || null,
        });
      setOpen(false);
      setEditing(null);
      setForm(defaultForm());
      qc.invalidateQueries({ queryKey: ['activities'] });
      qc.invalidateQueries({ queryKey: ['missing-days'] });
      qc.invalidateQueries({ queryKey: ['dashboard'] });
      success(editing ? 'Registro atualizado com histórico.' : 'Atividade registrada.');
    } catch (err) {
      failure(err);
    }
  };
  const startEdit = (a: Activity) => {
    setForm({
      activityDate: a.activityDate,
      projectId: a.projectId || '',
      taskId: a.taskId || '',
      category: a.category,
      description: a.description || '',
      hours: String(Math.floor(a.minutesSpent / 60)),
      minutes: String(a.minutesSpent % 60),
      partialResult: a.partialResult || '',
      noActivity: a.noActivity,
    });
    setEditing(a.id);
    setOpen(true);
  };
  return (
    <>
      <PageHead
        title="Registro de atividades"
        description="Registre o que realizou e acompanhe seu histórico diário."
        actions={
          <button
            className="btn-primary"
            onClick={() => {
              setEditing(null);
              setForm(defaultForm());
              setOpen(true);
            }}
          >
            <Plus size={16} /> Nova atividade
          </button>
        }
      />
      {(missing?.missing?.length || 0) > 0 && (
        <div className="mb-5 flex gap-3 rounded-2xl border border-amber-200 bg-amber-50 p-4">
          <AlertCircle className="shrink-0 text-amber-600" size={20} />
          <div>
            <p className="text-sm font-bold text-amber-900">Dias sem registro</p>
            <p className="mt-1 text-xs text-amber-800">
              {missing?.missing.map(prettyDate).join(', ')}. Considera a jornada configurada e não
              equivale a controle de ponto.
            </p>
          </div>
        </div>
      )}
      <div className="card mb-5 flex items-center justify-between gap-3 p-4">
        <div className="flex items-center gap-2 text-sm font-semibold">
          <Calendar size={17} className="text-nexo-blue" /> Histórico de atividades
        </div>
        <select className="field w-auto" value={days} onChange={(e) => setDays(e.target.value)}>
          <option value="7">Últimos 7 dias</option>
          <option value="30">Últimos 30 dias</option>
          <option value="90">Últimos 90 dias</option>
        </select>
      </div>
      {isLoading ? (
        <Loading />
      ) : error ? (
        <ErrorMessage error={error} />
      ) : entries.length === 0 ? (
        <Empty text="Ainda não existem registros neste período." />
      ) : (
        <div className="space-y-3">
          {entries.map((a) => (
            <article className="card flex flex-wrap items-start gap-4 p-5" key={a.id}>
              <div className="rounded-xl bg-blue-50 p-3">
                <Clock3 className="text-nexo-blue" size={20} />
              </div>
              <div className="min-w-0 flex-1">
                <div className="flex flex-wrap items-center gap-2">
                  <h3 className="font-bold">
                    {a.noActivity ? 'Dia sem atividades' : categoryLabels[a.category]}
                  </h3>
                  {a.isRetroactive && (
                    <span className="pill bg-amber-50 text-amber-700">Registro retroativo</span>
                  )}
                </div>
                <p className="mt-2 whitespace-pre-wrap text-sm text-slate-600">
                  {a.description || 'Nenhuma atividade registrada.'}
                </p>
                {a.partialResult && (
                  <p className="mt-2 text-xs text-slate-500">
                    <b>Resultado:</b> {a.partialResult}
                  </p>
                )}
                <div className="mt-3 flex flex-wrap gap-4 text-xs text-slate-500">
                  <span>Realizada: {prettyDate(a.activityDate)}</span>
                  <span>Registrada: {prettyDateTime(a.createdAtUtc)}</span>
                  <span>{a.minutesSpent} minutos</span>
                </div>
              </div>
              <button onClick={() => startEdit(a)} className="btn-secondary">
                <Edit3 size={15} /> Editar
              </button>
            </article>
          ))}
        </div>
      )}
      {open && (
        <Modal
          title={editing ? 'Editar registro' : 'Registrar atividade diária'}
          onClose={() => setOpen(false)}
        >
          <form onSubmit={save} className="space-y-4">
            <Field label="Data da atividade *">
              <input
                type="date"
                className="field"
                required
                max={localToday()}
                value={form.activityDate}
                onChange={(e) => setForm({ ...form, activityDate: e.target.value })}
              />
            </Field>
            <label className="flex items-center gap-2 rounded-xl bg-slate-50 p-3 text-sm">
              <input
                type="checkbox"
                checked={form.noActivity}
                onChange={(e) =>
                  setForm({ ...form, noActivity: e.target.checked, projectId: '', taskId: '' })
                }
              />{' '}
              Sem atividades neste dia
            </label>
            {!form.noActivity && (
              <>
                <Field label="Projeto (opcional)">
                  <select
                    className="field"
                    disabled={!!editing}
                    value={form.projectId}
                    onChange={(e) => setForm({ ...form, projectId: e.target.value, taskId: '' })}
                  >
                    <option value="">Atividade geral do núcleo</option>
                    {projects.map((p) => (
                      <option key={p.id} value={p.id}>
                        {p.name}
                      </option>
                    ))}
                  </select>
                </Field>
                {form.projectId && (
                  <Field label="Tarefa relacionada (opcional)">
                    <select
                      className="field"
                      disabled={!!editing}
                      value={form.taskId}
                      onChange={(e) => setForm({ ...form, taskId: e.target.value })}
                    >
                      <option value="">Sem tarefa específica</option>
                      {projectTasks.map((t) => (
                        <option key={t.id} value={t.id}>
                          {t.title}
                        </option>
                      ))}
                    </select>
                  </Field>
                )}
                <Field label="Categoria *">
                  <select
                    className="field"
                    value={form.category}
                    onChange={(e) =>
                      setForm({ ...form, category: e.target.value as ActivityCategory })
                    }
                  >
                    {Object.entries(categoryLabels).map(([key, label]) => (
                      <option key={key} value={key}>
                        {label}
                      </option>
                    ))}
                  </select>
                </Field>
                <Field label="O que você realizou? *">
                  <textarea
                    required
                    minLength={3}
                    maxLength={4000}
                    className="field"
                    rows={4}
                    placeholder="Descreva as atividades realizadas..."
                    value={form.description}
                    onChange={(e) => setForm({ ...form, description: e.target.value })}
                  />
                </Field>
                <div className="grid grid-cols-2 gap-3">
                  <Field label="Horas dedicadas">
                    <input
                      type="number"
                      min="0"
                      max="24"
                      className="field"
                      value={form.hours}
                      onChange={(e) => setForm({ ...form, hours: e.target.value })}
                    />
                  </Field>
                  <Field label="Minutos">
                    <input
                      type="number"
                      min="0"
                      max="59"
                      className="field"
                      value={form.minutes}
                      onChange={(e) => setForm({ ...form, minutes: e.target.value })}
                    />
                  </Field>
                </div>
                <Field label="Resultados parciais (opcional)">
                  <textarea
                    className="field"
                    rows={2}
                    maxLength={2000}
                    value={form.partialResult}
                    onChange={(e) => setForm({ ...form, partialResult: e.target.value })}
                  />
                </Field>
              </>
            )}
            {editing && (
              <p className="rounded-lg bg-blue-50 p-3 text-xs text-blue-700">
                A vinculação ao projeto/tarefa é mantida ao editar o registro. O backend guarda o
                histórico da alteração.
              </p>
            )}
            <div className="flex justify-end gap-2">
              <button className="btn-secondary" type="button" onClick={() => setOpen(false)}>
                Cancelar
              </button>
              <button className="btn-primary">
                <Send size={16} /> Salvar registro
              </button>
            </div>
          </form>
        </Modal>
      )}
      {notice}
    </>
  );
}
