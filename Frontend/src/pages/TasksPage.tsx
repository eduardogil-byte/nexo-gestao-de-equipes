import { useMemo, useState } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { CheckCircle2, Clock3, Filter, Search } from 'lucide-react';
import { Link } from 'react-router-dom';
import { get, post } from '../lib/api';
import type { Task } from '../lib/types';
import { prettyDate, priorityLabels, taskStateLabels } from '../lib/types';
import { Empty, ErrorMessage, Loading, PageHead, Status, useNotice } from '../components/UI';
export default function TasksPage() {
  const qc = useQueryClient();
  const { notice, success, failure } = useNotice();
  const {
    data: tasks = [],
    isLoading,
    error,
  } = useQuery({ queryKey: ['my-tasks'], queryFn: () => get<Task[]>('/tasks/mine') });
  const [filter, setFilter] = useState('pending');
  const [search, setSearch] = useState('');
  const view = useMemo(
    () =>
      tasks.filter(
        (t) =>
          (filter === 'all' ||
            (filter === 'pending' && t.status !== 'Completed' && t.status !== 'Cancelled') ||
            filter === t.status) &&
          `${t.title} ${t.projectName}`.toLowerCase().includes(search.toLowerCase()),
      ),
    [tasks, filter, search],
  );
  async function action(id: string, kind: 'accept' | 'decline' | 'complete') {
    try {
      await post(`/tasks/${id}/${kind}`, kind === 'complete' ? { forceAll: false } : {});
      qc.invalidateQueries({ queryKey: ['my-tasks'] });
      qc.invalidateQueries({ queryKey: ['tasks'] });
      qc.invalidateQueries({ queryKey: ['dashboard'] });
      success(
        kind === 'complete'
          ? 'Sua participação foi concluída.'
          : kind === 'accept'
            ? 'Tarefa aceita.'
            : 'Solicitação recusada.',
      );
    } catch (e) {
      failure(e);
    }
  }
  return (
    <>
      <PageHead
        title="Minhas tarefas"
        description="Acompanhe suas responsabilidades em todos os projetos."
      />
      <div className="card mb-5 flex flex-wrap items-center gap-3 p-4">
        <div className="relative min-w-48 flex-1">
          <Search className="absolute left-3 top-3 text-slate-400" size={17} />
          <input
            className="field pl-10"
            placeholder="Buscar tarefa ou projeto"
            value={search}
            onChange={(e) => setSearch(e.target.value)}
          />
        </div>
        <div className="relative">
          <Filter size={15} className="absolute left-3 top-3 text-slate-400" />
          <select className="field pl-9" value={filter} onChange={(e) => setFilter(e.target.value)}>
            <option value="pending">Pendentes</option>
            <option value="all">Todas</option>
            {Object.entries(taskStateLabels).map(([k, v]) => (
              <option key={k} value={k}>
                {v}
              </option>
            ))}
          </select>
        </div>
      </div>
      {isLoading ? (
        <Loading />
      ) : error ? (
        <ErrorMessage error={error} />
      ) : view.length === 0 ? (
        <Empty text="Nenhuma tarefa encontrada." />
      ) : (
        <div className="space-y-3">
          {view.map((t) => (
            <article key={t.id} className="card flex flex-wrap items-center gap-4 p-4 sm:p-5">
              <div className="rounded-xl bg-blue-50 p-3 text-nexo-blue">
                {t.status === 'Completed' ? <CheckCircle2 size={22} /> : <Clock3 size={22} />}
              </div>
              <div className="min-w-48 flex-1">
                <Link
                  to={`/projetos/${t.projectId}?tarefa=${t.id}`}
                  className="font-bold hover:text-nexo-blue"
                >
                  {t.title}
                </Link>
                <p className="mt-1 text-xs text-slate-500">Projeto: {t.projectName}</p>
                <div className="mt-2 flex flex-wrap gap-2">
                  <Status value={t.status} />
                  <Status value={t.priority} />
                  {t.accepted === false && <Status value="Pending" />}
                </div>
              </div>
              <div className="text-xs text-slate-500">Prazo: {prettyDate(t.dueDate)}</div>
              <div className="flex gap-2">
                {t.accepted === false ? (
                  <>
                    <button className="btn-primary" onClick={() => void action(t.id, 'accept')}>
                      Aceitar
                    </button>
                    <button className="btn-secondary" onClick={() => void action(t.id, 'decline')}>
                      Recusar
                    </button>
                  </>
                ) : (
                  t.status !== 'Completed' &&
                  t.status !== 'Cancelled' &&
                  !t.completedAtUtc && (
                    <button className="btn-secondary" onClick={() => void action(t.id, 'complete')}>
                      Concluir minha parte
                    </button>
                  )
                )}
              </div>
            </article>
          ))}
        </div>
      )}
      {notice}
    </>
  );
}
