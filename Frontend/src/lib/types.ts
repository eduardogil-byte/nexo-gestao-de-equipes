export type Id = string;
export type ProjectState = 'Proposed' | 'InProgress' | 'Paused' | 'Completed' | 'Cancelled';
export type WorkState = 'NotStarted' | 'InProgress' | 'Completed' | 'Blocked' | 'Cancelled';
export type Priority = 'Low' | 'Normal' | 'High' | 'Urgent';
export type ActivityCategory =
  'Development' | 'Research' | 'Testing' | 'Documentation' | 'Meeting' | 'Support' | 'Other';
export type Approval = 'Pending' | 'Approved' | 'Rejected';
export interface User {
  id: Id;
  fullName: string;
  email: string;
  specialty?: string | null;
  approval: Approval;
  roles?: string[];
  createdAtUtc?: string;
}
export interface Project {
  id: Id;
  name: string;
  description: string;
  objectives?: string;
  justification?: string;
  researchArea: string;
  budget?: number | null;
  expectedResults?: string;
  plannedStart?: string | null;
  plannedEnd?: string | null;
  leaderId: Id;
  status: ProjectState;
  visibility: 'Private' | 'PublicInsideNucleus';
  leaderProgressPercent?: number | null;
  updatedAtUtc?: string;
  collaborationCode?: string | null;
}
export interface Member {
  id: Id;
  fullName: string;
  specialty?: string;
  isLeader: boolean;
  joinedAtUtc?: string;
}
export interface Task {
  id: Id;
  projectId: Id;
  projectName?: string;
  title: string;
  description?: string;
  priority: Priority;
  status: WorkState;
  dueDate?: string | null;
  needsRedistribution?: boolean;
  accepted?: boolean;
  completedAtUtc?: string | null;
  assignees?: { userId: Id; accepted: boolean; completedAtUtc?: string | null; proposedById: Id }[];
  createdAtUtc?: string;
}
export interface Activity {
  id: Id;
  userId: Id;
  projectId?: Id | null;
  taskId?: Id | null;
  activityDate: string;
  category: ActivityCategory;
  description: string;
  minutesSpent: number;
  partialResult?: string | null;
  noActivity: boolean;
  createdAtUtc: string;
  updatedAtUtc: string;
  isRetroactive: boolean;
}
export interface Notification {
  id: Id;
  kind: string;
  message: string;
  projectId?: Id | null;
  taskId?: Id | null;
  createdAtUtc: string;
  readAtUtc?: string | null;
}
export interface Dashboard {
  totalProjects: number;
  activeProjects: number;
  tasksOpen: number;
  completedTasks: number;
  overdueTasks: number;
  openObstacles: number;
  hoursLast7Days: number;
  unreadNotifications: number;
  upcomingTasks: Task[];
  recentActivities: Pick<
    Activity,
    'id' | 'activityDate' | 'createdAtUtc' | 'projectId' | 'description' | 'noActivity'
  >[];
  note: string;
}
export interface ProjectSummary {
  id: Id;
  name: string;
  status: ProjectState;
  leaderId: Id;
  visibility: string;
  tasks: { total: number; completed: number; percent: number | null };
  milestones: { total: number; completed: number; percent: number | null };
  leaderProgressPercent?: number | null;
  deliverables: { total: number; completed: number };
  openObstacles?: number | null;
  note: string;
}
export interface Milestone {
  id: Id;
  title: string;
  description?: string;
  status: WorkState;
  dueDate?: string | null;
}
export interface Deliverable {
  id: Id;
  title: string;
  kind: string;
  description?: string;
  externalUrl?: string | null;
  status: WorkState;
}
export interface Obstacle {
  id: Id;
  description: string;
  taskId?: Id | null;
  status: 'Open' | 'Resolved';
  resolution?: string | null;
  createdAtUtc?: string;
}
export interface FileEntry {
  id: Id;
  originalName: string;
  length: number;
  uploadedAtUtc: string;
  uploadedById: Id;
}
export interface LinkEntry {
  id: Id;
  title: string;
  url: string;
  taskId?: string | null;
}
export interface Contribution {
  id: Id;
  projectId: Id;
  userId: Id;
  description: string;
  activityDate: string;
  minutesSpent: number;
  status: 'Pending' | 'Approved' | 'Rejected';
}
export const projectStateLabels: Record<ProjectState, string> = {
  Proposed: 'Proposto',
  InProgress: 'Em andamento',
  Paused: 'Pausado',
  Completed: 'Concluído',
  Cancelled: 'Cancelado',
};
export const taskStateLabels: Record<WorkState, string> = {
  NotStarted: 'Não iniciada',
  InProgress: 'Em andamento',
  Completed: 'Concluída',
  Blocked: 'Bloqueada',
  Cancelled: 'Cancelada',
};
export const priorityLabels: Record<Priority, string> = {
  Low: 'Baixa',
  Normal: 'Normal',
  High: 'Alta',
  Urgent: 'Urgente',
};
export const categoryLabels: Record<ActivityCategory, string> = {
  Development: 'Desenvolvimento',
  Research: 'Pesquisa',
  Testing: 'Testes e experimentos',
  Documentation: 'Documentação',
  Meeting: 'Reunião e planejamento',
  Support: 'Suporte e manutenção',
  Other: 'Outros',
};
export function prettyDate(value?: string | null) {
  if (!value) return 'Sem prazo';
  const date = new Date(`${value.slice(0, 10)}T12:00:00`);
  return Number.isNaN(date.getTime()) ? value : date.toLocaleDateString('pt-BR');
}
export function prettyDateTime(value?: string | null) {
  if (!value) return '—';
  const d = new Date(value);
  return Number.isNaN(d.getTime())
    ? '—'
    : d.toLocaleString('pt-BR', { dateStyle: 'short', timeStyle: 'short' });
}
export function localToday() {
  const d = new Date();
  const local = new Date(d.getTime() - d.getTimezoneOffset() * 60000);
  return local.toISOString().slice(0, 10);
}
