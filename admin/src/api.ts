export class ApiError extends Error {
  status: number;
  errors?: Record<string, string[]>;
  constructor(status: number, message: string, errors?: Record<string, string[]>) {
    super(message);
    this.status = status;
    this.errors = errors;
  }
}

let onUnauthorized: () => void = () => {};
export const setUnauthorizedHandler = (fn: () => void) => { onUnauthorized = fn; };

async function request<T>(method: string, url: string, body?: unknown, headers: Record<string, string> = {}): Promise<T> {
  const isForm = body instanceof FormData;
  const res = await fetch(url, {
    method,
    credentials: 'same-origin',
    headers: {
      'X-Requested-With': 'bjs-cms',
      ...(body !== undefined && !isForm ? { 'Content-Type': 'application/json' } : {}),
      ...headers,
    },
    body: body === undefined ? undefined : isForm ? (body as FormData) : JSON.stringify(body),
  });

  if (res.status === 401 && !url.includes('/auth/login')) onUnauthorized();
  if (res.status === 204) return undefined as T;

  const text = await res.text();
  let data: any = undefined;
  try { data = text ? JSON.parse(text) : undefined; } catch { /* not JSON */ }

  if (!res.ok) {
    const message = data?.message ?? (res.status === 403 ? 'You do not have permission to do that.' : `Something went wrong (${res.status}).`);
    throw new ApiError(res.status, message, data?.errors ?? undefined);
  }
  return data as T;
}

export const api = {
  get: <T,>(url: string) => request<T>('GET', url),
  post: <T,>(url: string, body?: unknown) => request<T>('POST', url, body ?? {}),
  put: <T,>(url: string, body: unknown, headers?: Record<string, string>) => request<T>('PUT', url, body, headers),
  patch: <T,>(url: string, body: unknown) => request<T>('PATCH', url, body),
  del: <T,>(url: string) => request<T>('DELETE', url),
  upload: <T,>(url: string, form: FormData) => request<T>('POST', url, form),
};

// ---------------------------------------------------------------- types

export interface Me { id: string; email: string; name: string; role: 'Admin' | 'Editor'; mustChangePassword: boolean; }

export interface Meta {
  siteUrl: string;
  routes: { path: string; anchor: string; label: string }[];
  icons: string[];
  platforms: string[];
  mail: { provider: string; configured: boolean };
  upload: { maxMegabytes: number; variants: number[] };
}

export interface SectionInfo {
  key: string; title: string; group: string; description: string; route: string | null;
  hasSeo: boolean; seoComplete: boolean | null; updatedAtUtc: string | null; updatedBy: string | null;
}

export interface Envelope<T = any> { key: string; version: string; updatedAtUtc: string | null; updatedBy: string | null; data: T; }

export interface MediaItem {
  id: string; originalName: string; ext: string; contentType: string; size: number; width: number; height: number;
  alt: string; variants: number[]; url: string; uploadedBy: string; uploadedAtUtc: string; usedIn: string[];
}

export interface EnquiryItem { id: string; fullName: string; email: string; message: string; createdAtUtc: string; read: boolean; delivery: string; }

export interface UserItem { id: string; email: string; name: string; role: 'Admin' | 'Editor'; mustChangePassword: boolean; createdAtUtc?: string; lastLoginUtc?: string | null; }

export interface Dashboard {
  stats: { sections: number; seoComplete: number; seoTotal: number; media: number; enquiriesUnread: number; enquiriesTotal: number; users: number };
  recentEdits: { key: string; title: string; updatedAtUtc: string; updatedBy: string }[];
  recentEnquiries: EnquiryItem[];
  checklist: { id: string; label: string; ok: boolean; link: string | null; detail: string }[];
}
