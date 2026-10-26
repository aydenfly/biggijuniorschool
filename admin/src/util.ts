// Path helpers for JSON documents: "people[2].bio.title"

type Key = string | number;

export function parsePath(path: string): Key[] {
  const keys: Key[] = [];
  path.replace(/[^.[\]]+|\[(\d+)\]/g, (m, idx) => { keys.push(idx !== undefined ? Number(idx) : m); return m; });
  return keys;
}

export function getPath(obj: any, path: string): any {
  let cur = obj;
  for (const k of parsePath(path)) { if (cur == null) return undefined; cur = cur[k]; }
  return cur;
}

/** Sets a value on a deep copy and returns the copy. */
export function setPath<T>(obj: T, path: string, value: unknown): T {
  const copy: any = structuredClone(obj);
  const keys = parsePath(path);
  let cur = copy;
  keys.slice(0, -1).forEach((k, i) => {
    if (cur[k] == null) cur[k] = typeof keys[i + 1] === 'number' ? [] : {};
    cur = cur[k];
  });
  cur[keys[keys.length - 1]] = value;
  return copy;
}

export const uid = () => Math.random().toString(36).slice(2, 10);

export function formatDate(iso?: string | null): string {
  if (!iso) return '';
  const d = new Date(iso.endsWith('Z') ? iso : iso + 'Z');
  return d.toLocaleString(undefined, { day: 'numeric', month: 'short', year: 'numeric', hour: '2-digit', minute: '2-digit' });
}

export function timeAgo(iso?: string | null): string {
  if (!iso) return 'never';
  const d = new Date(iso.endsWith('Z') ? iso : iso + 'Z').getTime();
  const s = Math.max(0, (Date.now() - d) / 1000);
  if (s < 60) return 'just now';
  if (s < 3600) return `${Math.floor(s / 60)} min ago`;
  if (s < 86400) return `${Math.floor(s / 3600)} h ago`;
  if (s < 86400 * 30) return `${Math.floor(s / 86400)} d ago`;
  return formatDate(iso);
}

export function fileSize(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(0)} KB`;
  return `${(bytes / 1024 / 1024).toFixed(1)} MB`;
}

/** Uploaded files live on the CMS host; older files live on the public website. */
export function resolveMedia(src: string | undefined | null, siteUrl: string): string {
  if (!src) return '';
  if (/^https?:\/\//i.test(src) || src.startsWith('/uploads/')) return src;
  return siteUrl.replace(/\/$/, '') + src;
}
