const root = (import.meta.env.VITE_API_BASE_URL || '/api').replace(/\/$/, '');
let tokenProvider: () => string | null = () => null;
let unauthorizedHandler: () => void = () => {};
export function setupApi(getToken: () => string | null, onUnauthorized: () => void) {
  tokenProvider = getToken;
  unauthorizedHandler = onUnauthorized;
}
export class ApiError extends Error {
  constructor(
    message: string,
    public status: number,
  ) {
    super(message);
    this.name = 'ApiError';
  }
}
export async function api<T>(path: string, options: RequestInit = {}): Promise<T> {
  const token = tokenProvider();
  const headers = new Headers(options.headers);
  if (token) headers.set('Authorization', `Bearer ${token}`);
  if (options.body && !(options.body instanceof FormData) && !headers.has('Content-Type'))
    headers.set('Content-Type', 'application/json');
  let response: Response;
  try {
    response = await fetch(`${root}${path}`, { ...options, headers, cache: 'no-store' });
  } catch {
    throw new ApiError(
      'Não foi possível conectar à API. Confirme que o backend está rodando na porta 5088.',
      0,
    );
  }
  if (response.status === 401 && token) unauthorizedHandler();
  if (!response.ok) {
    const value = await response.json().catch(() => null);
    const message =
      typeof value?.error === 'string'
        ? value.error
        : typeof value?.title === 'string'
          ? value.title
          : `Erro HTTP ${response.status}`;
    throw new ApiError(message, response.status);
  }
  if (response.status === 204) return undefined as T;
  const ct = response.headers.get('content-type') || '';
  return ct.includes('application/json') ? (response.json() as Promise<T>) : (undefined as T);
}
export const get = <T>(path: string) => api<T>(path);
export const post = <T>(path: string, payload: unknown = {}) =>
  api<T>(path, { method: 'POST', body: JSON.stringify(payload) });
export const put = <T>(path: string, payload: unknown) =>
  api<T>(path, { method: 'PUT', body: JSON.stringify(payload) });
export const remove = <T>(path: string) => api<T>(path, { method: 'DELETE' });
export const upload = <T>(path: string, body: FormData) => api<T>(path, { method: 'POST', body });
export async function downloadFile(id: string, name: string) {
  const token = tokenProvider();
  const res = await fetch(`${root}/files/${id}/download`, {
    headers: token ? { Authorization: `Bearer ${token}` } : {},
  });
  if (!res.ok) throw new ApiError('Não foi possível baixar o arquivo.', res.status);
  const blob = await res.blob();
  const url = URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = url;
  a.download = name;
  a.click();
  URL.revokeObjectURL(url);
}
