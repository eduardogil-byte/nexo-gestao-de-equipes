import { useEffect, useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { get, put } from '../lib/api';
import { ErrorMessage, Loading, Modal } from './UI';
import type { User } from '../lib/types';
const DAYS = [
  ['Monday', 'Segunda'],
  ['Tuesday', 'Terça'],
  ['Wednesday', 'Quarta'],
  ['Thursday', 'Quinta'],
  ['Friday', 'Sexta'],
  ['Saturday', 'Sábado'],
  ['Sunday', 'Domingo'],
];
const SHIFTS = [
  ['Morning', 'Manhã'],
  ['Afternoon', 'Tarde'],
  ['Evening', 'Noite'],
];
type Entry = { weekday: string; shift: string };
export default function AdminSchedule({
  user,
  onClose,
  onSaved,
  onError,
}: {
  user: User;
  onClose: () => void;
  onSaved: () => void;
  onError: (e: unknown) => void;
}) {
  const { data, isLoading, error } = useQuery({
    queryKey: ['user-schedule', user.id],
    queryFn: () => get<Entry[]>(`/users/${user.id}/schedule`),
  });
  const [items, setItems] = useState<Entry[]>([]);
  const [busy, setBusy] = useState(false);
  useEffect(() => {
    if (data) setItems(data);
  }, [data]);
  function toggle(weekday: string, shift: string) {
    setItems((old) =>
      old.some((x) => x.weekday === weekday && x.shift === shift)
        ? old.filter((x) => !(x.weekday === weekday && x.shift === shift))
        : [...old, { weekday, shift }],
    );
  }
  async function save() {
    setBusy(true);
    try {
      await put(`/users/${user.id}/schedule`, { items });
      onSaved();
      onClose();
    } catch (e) {
      onError(e);
    } finally {
      setBusy(false);
    }
  }
  return (
    <Modal title={`Jornada — ${user.fullName}`} onClose={onClose}>
      <p className="mb-4 text-xs text-slate-500">
        A coordenação pode ajustar os dias e turnos informados pelo integrante.
      </p>
      {isLoading ? (
        <Loading />
      ) : error ? (
        <ErrorMessage error={error} />
      ) : (
        <>
          <div className="overflow-x-auto">
            <table className="w-full text-sm">
              <thead>
                <tr>
                  <th className="p-2 text-left">Dia</th>
                  {SHIFTS.map(([key, label]) => (
                    <th key={key} className="p-2 text-center">
                      {label}
                    </th>
                  ))}
                </tr>
              </thead>
              <tbody>
                {DAYS.map(([day, label]) => (
                  <tr className="border-t" key={day}>
                    <td className="py-2 text-xs font-semibold">{label}</td>
                    {SHIFTS.map(([shift]) => (
                      <td className="py-2 text-center" key={shift}>
                        <input
                          className="h-4 w-4 accent-blue-600"
                          type="checkbox"
                          aria-label={`${day}, ${shift}`}
                          checked={items.some((i) => i.weekday === day && i.shift === shift)}
                          onChange={() => toggle(day, shift)}
                        />
                      </td>
                    ))}
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          <div className="mt-5 flex justify-end">
            <button className="btn-primary" disabled={busy} onClick={() => void save()}>
              {busy ? 'Salvando...' : 'Salvar jornada'}
            </button>
          </div>
        </>
      )}
    </Modal>
  );
}
