import { createContext, useCallback, useContext, useEffect, useMemo, useRef, useState, type ReactNode } from 'react';
import { useBlocker } from 'react-router-dom';
import { api, ApiError, type Envelope, type Me, type Meta } from './api';
import { getPath, setPath } from './util';

// ================================================================= app-wide

interface ConfirmOptions { title: string; body?: ReactNode; confirmLabel?: string; danger?: boolean; }
interface Toast { id: number; kind: 'ok' | 'error'; text: string; }

interface AppState {
  me: Me;
  setMe: (m: Me | null) => void;
  meta: Meta;
  toast: (text: string, kind?: 'ok' | 'error') => void;
  confirm: (o: ConfirmOptions) => Promise<boolean>;
  refreshKey: number;
  bump: () => void;
}

const AppCtx = createContext<AppState | null>(null);
export const useApp = () => {
  const v = useContext(AppCtx);
  if (!v) throw new Error('useApp outside provider');
  return v;
};

export function AppProvider({ me, setMe, meta, children }: { me: Me; setMe: (m: Me | null) => void; meta: Meta; children: ReactNode }) {
  const [toasts, setToasts] = useState<Toast[]>([]);
  const [dialog, setDialog] = useState<(ConfirmOptions & { resolve: (v: boolean) => void }) | null>(null);
  const [refreshKey, setRefreshKey] = useState(0);
  const nextId = useRef(1);

  const toast = useCallback((text: string, kind: 'ok' | 'error' = 'ok') => {
    const id = nextId.current++;
    setToasts(t => [...t, { id, kind, text }]);
    setTimeout(() => setToasts(t => t.filter(x => x.id !== id)), kind === 'error' ? 7000 : 3500);
  }, []);

  const confirm = useCallback((o: ConfirmOptions) => new Promise<boolean>(resolve => setDialog({ ...o, resolve })), []);
  const bump = useCallback(() => setRefreshKey(k => k + 1), []);

  const value = useMemo(() => ({ me, setMe, meta, toast, confirm, refreshKey, bump }), [me, setMe, meta, toast, confirm, refreshKey, bump]);

  const close = (v: boolean) => { dialog?.resolve(v); setDialog(null); };

  return (
    <AppCtx.Provider value={value}>
      {children}
      <div className="toasts" role="status" aria-live="polite">
        {toasts.map(t => <div key={t.id} className={`toast ${t.kind}`}>{t.text}</div>)}
      </div>
      {dialog && (
        <div className="modal-backdrop" onMouseDown={e => { if (e.target === e.currentTarget) close(false); }}>
          <div className="modal small" role="alertdialog" aria-modal="true" aria-labelledby="confirm-title">
            <h2 id="confirm-title">{dialog.title}</h2>
            {dialog.body && <div className="modal-body">{dialog.body}</div>}
            <div className="modal-actions">
              <button className="btn" onClick={() => close(false)}>Cancel</button>
              <button className={`btn ${dialog.danger ? 'danger' : 'primary'}`} autoFocus onClick={() => close(true)}>{dialog.confirmLabel ?? 'Confirm'}</button>
            </div>
          </div>
        </div>
      )}
    </AppCtx.Provider>
  );
}

// ================================================================= form

export interface FormApi {
  data: any;
  set: (path: string, value: unknown) => void;
  update: (fn: (draft: any) => void) => void;
  errors: Record<string, string[]>;
  readOnly: boolean;
}

const FormCtx = createContext<FormApi | null>(null);
export const FormProvider = FormCtx.Provider;
export const useForm = () => {
  const v = useContext(FormCtx);
  if (!v) throw new Error('Field used outside a form');
  return v;
};

/** A bound value + its error message. */
export function useField<T = any>(path: string): { value: T; set: (v: T) => void; error?: string } {
  const f = useForm();
  const value = getPath(f.data, path) as T;
  const error = f.errors[path]?.[0];
  return { value, set: v => f.set(path, v), error };
}

export interface SectionForm extends FormApi {
  env: Envelope | null;
  loading: boolean;
  loadError: string | null;
  saving: boolean;
  dirty: boolean;
  conflict: boolean;
  save: () => Promise<boolean>;
  discard: () => void;
  reload: () => Promise<void>;
  errorCount: number;
}

/**
 * Loads one section, tracks edits, validates through the server on save, and warns before leaving with unsaved changes.
 */
export function useSectionForm(key: string): SectionForm {
  const { toast } = useApp();
  const [env, setEnv] = useState<Envelope | null>(null);
  const [data, setData] = useState<any>(null);
  const [baseline, setBaseline] = useState('');
  const [errors, setErrors] = useState<Record<string, string[]>>({});
  const [loading, setLoading] = useState(true);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);
  const [conflict, setConflict] = useState(false);

  const apply = (e: Envelope) => {
    setEnv(e);
    setData(e.data);
    setBaseline(JSON.stringify(e.data));
    setErrors({});
    setConflict(false);
  };

  const reload = useCallback(async () => {
    setLoading(true);
    setLoadError(null);
    try { apply(await api.get<Envelope>(`/api/admin/content/${key}`)); }
    catch (e) { setLoadError(e instanceof ApiError ? e.message : 'Could not load this section.'); }
    finally { setLoading(false); }
  }, [key]);

  useEffect(() => { setEnv(null); setData(null); void reload(); }, [reload]);

  const dirty = data !== null && JSON.stringify(data) !== baseline;

  // warn when closing the tab, and when navigating inside the app
  useEffect(() => {
    if (!dirty) return;
    const h = (e: BeforeUnloadEvent) => { e.preventDefault(); e.returnValue = ''; };
    window.addEventListener('beforeunload', h);
    return () => window.removeEventListener('beforeunload', h);
  }, [dirty]);
  const blocker = useBlocker(dirty);
  const { confirm } = useApp();
  useEffect(() => {
    if (blocker.state !== 'blocked') return;
    void confirm({ title: 'Leave without saving?', body: 'You have changes that have not been saved. They will be lost if you leave this page.', confirmLabel: 'Leave page', danger: true })
      .then(ok => (ok ? blocker.proceed() : blocker.reset()));
  }, [blocker, confirm]);

  const set = useCallback((path: string, value: unknown) => {
    setData((d: any) => setPath(d, path, value));
    setErrors(e => { if (!(path in e)) return e; const n = { ...e }; delete n[path]; return n; });
  }, []);

  const update = useCallback((fn: (draft: any) => void) => {
    setData((d: any) => { const c = structuredClone(d); fn(c); return c; });
    setErrors({}); // list edits shift indexes, so old messages may point at the wrong item
  }, []);

  const save = useCallback(async () => {
    if (!env || saving) return false;
    setSaving(true);
    try {
      const saved = await api.put<Envelope>(`/api/admin/content/${key}`, data, { 'If-Match': `"${env.version}"` });
      apply(saved);
      toast('Saved. The change is live on the site.');
      return true;
    } catch (e) {
      if (e instanceof ApiError) {
        if (e.status === 409) { setConflict(true); toast(e.message, 'error'); }
        else if (e.errors) { setErrors(e.errors); toast('Some fields need attention. They are marked below.', 'error'); }
        else toast(e.message, 'error');
      } else toast('Could not reach the server. Check your connection and try again.', 'error');
      return false;
    } finally { setSaving(false); }
  }, [env, saving, key, data, toast]);

  const discard = useCallback(() => { if (env) apply(env); }, [env]);

  return {
    env, data, set, update, errors, readOnly: false, loading, loadError, saving, dirty, conflict, save, discard, reload,
    errorCount: Object.keys(errors).length,
  };
}
