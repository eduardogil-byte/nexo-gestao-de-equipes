import { useState, type FormEvent } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { Handshake, Plus } from 'lucide-react';
import { get, post } from '../lib/api';
import type { Contribution, Project } from '../lib/types';
import { localToday, prettyDate } from '../lib/types';
import { Empty, ErrorMessage, Field, Loading, PageHead, Status, useNotice } from '../components/UI';
export default function ContributionsPage() {
  const qc = useQueryClient();
  const { notice, success, failure } = useNotice();
  const {
    data: mine = [],
    error,
    isLoading,
  } = useQuery({
    queryKey: ['contributions-mine'],
    queryFn: () => get<Contribution[]>('/contributions/mine'),
  });
  const { data: projects = [] } = useQuery({
    queryKey: ['projects'],
    queryFn: () => get<Project[]>('/projects'),
  });
  const [form, setForm] = useState({
    projectId: '',
    collaborationCode: '',
    activityDate: localToday(),
    description: '',
    hours: 0,
    minutes: 0,
  });
  const save = async (e: FormEvent) => {
    e.preventDefault();
    try {
      await post('/contributions', {
        projectId: form.collaborationCode ? null : form.projectId || null,
        collaborationCode: form.collaborationCode || null,
        activityDate: form.activityDate,
        description: form.description,
        minutesSpent: Number(form.hours) * 60 + Number(form.minutes),
      });
      qc.invalidateQueries({ queryKey: ['contributions-mine'] });
      success('Contribuição enviada. Aguarde a confirmação do líder.');
      setForm({
        projectId: '',
        collaborationCode: '',
        activityDate: localToday(),
        description: '',
        hours: 0,
        minutes: 0,
      });
    } catch (err) {
      failure(err);
    }
  };
  return (
    <>
      <PageHead
        title="Colaborações externas"
        description="Registre sua ajuda pontual em projetos dos quais você não faz parte."
      />
      <div className="grid gap-6 xl:grid-cols-[1fr_1.2fr]">
        <section className="card p-6">
          <h2 className="section-title flex items-center gap-2">
            <Handshake size={20} className="text-nexo-teal" /> Registrar contribuição
          </h2>
          <p className="mt-2 text-sm text-slate-500">
            A colaboração ficará pendente até o líder confirmá-la. Não concede acesso à equipe ou
            aos arquivos privados.
          </p>
          <form className="mt-5 space-y-4" onSubmit={save}>
            <Field label="Projeto público do núcleo (opcional)">
              <select
                className="field"
                value={form.projectId}
                disabled={!!form.collaborationCode}
                onChange={(e) => setForm({ ...form, projectId: e.target.value })}
              >
                <option value="">Selecione ou informe um código abaixo</option>
                {projects
                  .filter((p) => p.visibility === 'PublicInsideNucleus')
                  .map((p) => (
                    <option key={p.id} value={p.id}>
                      {p.name}
                    </option>
                  ))}
              </select>
            </Field>
            <Field label="Código de projeto privado (opcional)">
              <input
                className="field"
                value={form.collaborationCode}
                onChange={(e) =>
                  setForm({ ...form, collaborationCode: e.target.value, projectId: '' })
                }
                placeholder="Código fornecido pelo líder"
              />
            </Field>
            <Field label="Data">
              <input
                className="field"
                type="date"
                max={localToday()}
                required
                value={form.activityDate}
                onChange={(e) => setForm({ ...form, activityDate: e.target.value })}
              />
            </Field>
            <Field label="O que você ajudou a realizar? *">
              <textarea
                className="field"
                rows={4}
                required
                minLength={3}
                value={form.description}
                onChange={(e) => setForm({ ...form, description: e.target.value })}
              />
            </Field>
            <div className="grid grid-cols-2 gap-3">
              <Field label="Horas">
                <input
                  className="field"
                  type="number"
                  min="0"
                  max="24"
                  value={form.hours}
                  onChange={(e) => setForm({ ...form, hours: Number(e.target.value) })}
                />
              </Field>
              <Field label="Minutos">
                <input
                  className="field"
                  type="number"
                  min="0"
                  max="59"
                  value={form.minutes}
                  onChange={(e) => setForm({ ...form, minutes: Number(e.target.value) })}
                />
              </Field>
            </div>
            <button className="btn-primary w-full">
              <Plus size={16} /> Enviar contribuição
            </button>
          </form>
        </section>
        <section className="card p-6">
          <h2 className="section-title">Minhas contribuições enviadas</h2>
          {isLoading ? (
            <Loading />
          ) : error ? (
            <ErrorMessage error={error} />
          ) : mine.length === 0 ? (
            <div className="mt-5">
              <Empty text="Nenhuma colaboração externa registrada." />
            </div>
          ) : (
            <div className="mt-5 space-y-3">
              {mine.map((c) => (
                <div key={c.id} className="rounded-xl border p-4">
                  <div className="flex flex-wrap items-center justify-between gap-2">
                    <span className="text-sm font-bold">{c.description}</span>
                    <Status value={c.status} />
                  </div>
                  <div className="mt-2 text-xs text-slate-500">
                    {prettyDate(c.activityDate)} · {c.minutesSpent} minutos
                  </div>
                </div>
              ))}
            </div>
          )}
        </section>
      </div>
      {notice}
    </>
  );
}
