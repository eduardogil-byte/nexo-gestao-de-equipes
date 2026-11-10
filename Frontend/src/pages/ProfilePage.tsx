import { useState, type FormEvent } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { CalendarDays, ShieldCheck, UserRound } from 'lucide-react';
import { get, put } from '../lib/api';
import { useAuth } from '../contexts/AuthContext';
import { ErrorMessage, Field, Loading, PageHead, useNotice } from '../components/UI';
const days = [
  ['Monday', 'Segunda-feira'],
  ['Tuesday', 'Terça-feira'],
  ['Wednesday', 'Quarta-feira'],
  ['Thursday', 'Quinta-feira'],
  ['Friday', 'Sexta-feira'],
  ['Saturday', 'Sábado'],
  ['Sunday', 'Domingo'],
];
const shifts = [
  ['Morning', 'Manhã'],
  ['Afternoon', 'Tarde'],
  ['Evening', 'Noite'],
];
type ScheduleItem = { weekday: string; shift: string };
export default function ProfilePage() {
  const { session, refresh, isAdmin } = useAuth();
  const qc = useQueryClient();
  const { notice, success, failure } = useNotice();
  const [name, setName] = useState(session?.user.fullName || '');
  const [specialty, setSpecialty] = useState(session?.user.specialty || '');
  const [schedule, setSchedule] = useState<ScheduleItem[]>([]);
  const [loaded, setLoaded] = useState(false);
  const { isLoading, error } = useQuery({
    queryKey: ['my-schedule'],
    queryFn: async () => {
      const items = await get<ScheduleItem[]>('/auth/me/schedule');
      setSchedule(items);
      setLoaded(true);
      return items;
    },
  });
  async function saveProfile(e: FormEvent) {
    e.preventDefault();
    try {
      await put('/auth/me', { fullName: name, specialty });
      await refresh();
      success('Dados pessoais atualizados.');
    } catch (e) {
      failure(e);
    }
  }
  async function saveSchedule() {
    try {
      await put('/auth/me/schedule', { items: schedule });
      qc.invalidateQueries({ queryKey: ['missing-days'] });
      success('Jornada de trabalho atualizada.');
    } catch (e) {
      failure(e);
    }
  }
  function toggle(weekday: string, shift: string) {
    setSchedule((prev) =>
      prev.some((item) => item.weekday === weekday && item.shift === shift)
        ? prev.filter((i) => !(i.weekday === weekday && i.shift === shift))
        : [...prev, { weekday, shift }],
    );
  }
  return (
    <>
      <PageHead
        title="Meu perfil"
        description="Atualize seus dados e os turnos habituais de trabalho."
      />
      <div className="grid gap-6 xl:grid-cols-2">
        <section className="card p-6">
          <h2 className="section-title flex items-center gap-2">
            <UserRound size={20} className="text-nexo-blue" /> Dados pessoais
          </h2>
          <form onSubmit={saveProfile} className="mt-5 space-y-4">
            <Field label="Nome completo">
              <input
                className="field"
                required
                minLength={3}
                maxLength={150}
                value={name}
                onChange={(e) => setName(e.target.value)}
              />
            </Field>
            <Field label="E-mail">
              <input className="field bg-slate-50" readOnly value={session?.user.email || ''} />
            </Field>
            <Field label="Área de atuação">
              <input
                className="field"
                maxLength={120}
                placeholder="Ex.: Eletrônica, Programação"
                value={specialty}
                onChange={(e) => setSpecialty(e.target.value)}
              />
            </Field>
            <div className="flex gap-2 rounded-xl bg-blue-50 p-3 text-xs text-blue-800">
              <ShieldCheck size={17} />
              {isAdmin
                ? 'Você possui acesso de coordenação.'
                : 'As permissões são gerenciadas pela coordenação.'}
            </div>
            <button className="btn-primary">Salvar dados</button>
          </form>
        </section>
        <section className="card p-6">
          <h2 className="section-title flex items-center gap-2">
            <CalendarDays size={20} className="text-nexo-blue" /> Minha jornada habitual
          </h2>
          <p className="mt-2 text-xs text-slate-500">
            Selecione os dias e turnos. A coordenação poderá ajustar essas informações. Não
            substitui controle de ponto.
          </p>
          {isLoading || !loaded ? (
            <Loading />
          ) : error ? (
            <ErrorMessage error={error} />
          ) : (
            <>
              <div className="mt-5 overflow-x-auto">
                <table className="w-full text-sm">
                  <thead>
                    <tr>
                      <th className="pb-3 text-left text-xs text-slate-500">Dia</th>
                      {shifts.map(([key, label]) => (
                        <th className="pb-3 text-center text-xs text-slate-500" key={key}>
                          {label}
                        </th>
                      ))}
                    </tr>
                  </thead>
                  <tbody>
                    {days.map(([day, label]) => (
                      <tr className="border-t" key={day}>
                        <td className="py-3 text-xs font-semibold">{label}</td>
                        {shifts.map(([shift]) => (
                          <td className="py-3 text-center" key={shift}>
                            <input
                              aria-label={`${label}, ${shift}`}
                              type="checkbox"
                              checked={schedule.some((i) => i.weekday === day && i.shift === shift)}
                              onChange={() => toggle(day, shift)}
                              className="h-4 w-4 accent-blue-600"
                            />
                          </td>
                        ))}
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
              <button className="btn-primary mt-4" onClick={() => void saveSchedule()}>
                Salvar jornada
              </button>
            </>
          )}
        </section>
      </div>
      {notice}
    </>
  );
}
