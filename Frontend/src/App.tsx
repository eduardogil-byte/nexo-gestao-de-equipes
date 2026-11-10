import { Navigate, NavLink, Route, Routes, useLocation } from 'react-router-dom';
import {
  Activity,
  Bell,
  ChevronDown,
  FolderKanban,
  Handshake,
  LayoutDashboard,
  ListTodo,
  LogOut,
  Menu,
  Settings,
  ShieldCheck,
  X,
} from 'lucide-react';
import { useQuery } from '@tanstack/react-query';
import { useState } from 'react';
import { useAuth } from './contexts/AuthContext';
import { get } from './lib/api';
import type { Notification } from './lib/types';
import { Brand, Loading } from './components/UI';
import AuthPage from './pages/AuthPage';
import DashboardPage from './pages/DashboardPage';
import ProjectsPage from './pages/ProjectsPage';
import ProjectPage from './pages/ProjectPage';
import TasksPage from './pages/TasksPage';
import ActivitiesPage from './pages/ActivitiesPage';
import AdminPage from './pages/AdminPage';
import NotificationsPage from './pages/NotificationsPage';
import ProfilePage from './pages/ProfilePage';
import ContributionsPage from './pages/ContributionsPage';
const nav = [
  { to: '/', label: 'Dashboard', icon: LayoutDashboard },
  { to: '/projetos', label: 'Projetos', icon: FolderKanban },
  { to: '/tarefas', label: 'Minhas tarefas', icon: ListTodo },
  { to: '/atividades', label: 'Atividades', icon: Activity },
  { to: '/colaboracoes', label: 'Colaborações', icon: Handshake },
  { to: '/notificacoes', label: 'Notificações', icon: Bell },
  { to: '/perfil', label: 'Meu perfil', icon: Settings },
];
function Frame() {
  const { session, logout, isAdmin } = useAuth();
  const [open, setOpen] = useState(false);
  const location = useLocation();
  const { data: notifications = [] } = useQuery({
    queryKey: ['notification-head'],
    queryFn: () => get<Notification[]>('/notifications?limit=40'),
    refetchInterval: 60000,
  });
  const unread = notifications.filter((n) => !n.readAtUtc).length;
  const navigation = [
    ...nav,
    ...(isAdmin ? [{ to: '/administracao', label: 'Administração', icon: ShieldCheck }] : []),
  ];
  const sidebar = (
    <>
      <div className="mb-10 px-3">
        <Brand light />
      </div>
      <div className="mb-2 px-4 text-[10px] font-bold uppercase tracking-[.2em] text-slate-500">
        Workspace
      </div>
      <nav className="space-y-1">
        {navigation.map((i) => (
          <NavLink
            key={i.to}
            to={i.to}
            end={i.to === '/'}
            onClick={() => setOpen(false)}
            className={({ isActive }) =>
              `flex items-center gap-3 rounded-xl px-3 py-3 text-[13px] font-semibold transition ${isActive ? 'bg-blue-600/20 text-white ring-1 ring-blue-400/20' : 'text-slate-400 hover:bg-slate-800 hover:text-white'}`
            }
          >
            <i.icon size={19} />
            <span className="flex-1">{i.label}</span>
            {i.to === '/notificacoes' && unread > 0 && (
              <span className="rounded-full bg-nexo-blue px-2 py-0.5 text-[10px] text-white">
                {unread}
              </span>
            )}
          </NavLink>
        ))}
      </nav>
      <div className="mt-auto border-t border-slate-700/60 pt-4">
        <div className="mb-4 flex items-center gap-3 rounded-xl bg-slate-800/80 p-3">
          <div className="flex h-9 w-9 shrink-0 items-center justify-center rounded-full bg-blue-600 text-sm font-bold text-white">
            {session?.user.fullName.slice(0, 1).toUpperCase()}
          </div>
          <div className="min-w-0">
            <div className="truncate text-sm font-semibold text-white">
              {session?.user.fullName}
            </div>
            <div className="text-[11px] text-slate-400">
              {isAdmin ? 'Coordenação' : 'Integrante'}
            </div>
          </div>
        </div>
        <button
          onClick={logout}
          className="flex w-full items-center gap-3 px-3 py-2 text-sm text-slate-400 hover:text-white"
        >
          <LogOut size={18} /> Sair da conta
        </button>
      </div>
    </>
  );
  return (
    <div className="min-h-screen bg-nexo-bg">
      <aside className="fixed inset-y-0 left-0 z-30 hidden w-64 flex-col bg-nexo-navy px-4 pb-5 pt-7 lg:flex">
        {sidebar}
      </aside>
      {open && (
        <>
          <div
            className="fixed inset-0 z-40 bg-slate-950/60 lg:hidden"
            onClick={() => setOpen(false)}
          />
          <aside className="fixed inset-y-0 left-0 z-50 flex w-[280px] flex-col overflow-y-auto bg-nexo-navy px-4 pb-5 pt-7 lg:hidden">
            <button
              aria-label="Fechar menu"
              onClick={() => setOpen(false)}
              className="absolute right-4 top-5 text-white"
            >
              <X />
            </button>
            {sidebar}
          </aside>
        </>
      )}
      <div className="lg:pl-64">
        <header className="sticky top-0 z-20 flex h-20 items-center justify-between border-b border-slate-200 bg-white/95 px-4 backdrop-blur sm:px-8">
          <div className="flex items-center gap-3">
            <button
              aria-label="Abrir menu"
              onClick={() => setOpen(true)}
              className="rounded-lg p-2 text-slate-600 lg:hidden"
            >
              <Menu />
            </button>
            <div className="text-sm font-semibold text-slate-600">
              Núcleo de Pesquisa <span className="mx-2 text-slate-300">/</span>
              <span className="text-nexo-navy">
                {navigation.find((n) => n.to === location.pathname)?.label || 'Projeto'}
              </span>
            </div>
          </div>
          <div className="flex items-center gap-3">
            <NavLink
              to="/notificacoes"
              aria-label="Notificações"
              className="relative rounded-xl border border-slate-200 p-2.5 text-slate-600 hover:bg-slate-50"
            >
              <Bell size={18} />
              {unread > 0 && (
                <span className="absolute -right-1 -top-1 h-2.5 w-2.5 rounded-full bg-red-500 ring-2 ring-white" />
              )}
            </NavLink>
            <NavLink
              to="/perfil"
              className="hidden items-center gap-2 rounded-xl p-1.5 hover:bg-slate-50 sm:flex"
            >
              <div className="flex h-9 w-9 items-center justify-center rounded-full bg-nexo-blue/10 font-bold text-nexo-blue">
                {session?.user.fullName[0]}
              </div>
              <span className="max-w-28 truncate text-sm font-semibold">
                {session?.user.fullName.split(' ')[0]}
              </span>
              <ChevronDown size={14} className="text-slate-400" />
            </NavLink>
          </div>
        </header>
        <main className="mx-auto max-w-[1520px] p-4 pb-16 sm:p-8">
          <Routes>
            <Route path="/" element={<DashboardPage />} />
            <Route path="/projetos" element={<ProjectsPage />} />
            <Route path="/projetos/:id" element={<ProjectPage />} />
            <Route path="/tarefas" element={<TasksPage />} />
            <Route path="/atividades" element={<ActivitiesPage />} />
            <Route path="/colaboracoes" element={<ContributionsPage />} />
            <Route path="/notificacoes" element={<NotificationsPage />} />
            <Route path="/perfil" element={<ProfilePage />} />
            {isAdmin && <Route path="/administracao" element={<AdminPage />} />}
            <Route path="*" element={<Navigate to="/" replace />} />
          </Routes>
        </main>
      </div>
    </div>
  );
}
export default function App() {
  const { session, ready } = useAuth();
  if (!ready) return <Loading />;
  return session ? (
    <Frame />
  ) : (
    <Routes>
      <Route path="/entrar" element={<AuthPage />} />
      <Route path="*" element={<Navigate to="/entrar" replace />} />
    </Routes>
  );
}
