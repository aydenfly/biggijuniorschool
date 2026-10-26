import { useEffect, type ReactNode } from 'react';
import { useApp, useSectionForm, FormProvider, type SectionForm } from '../state';
import { formatDate } from '../util';
import { Notice, Spinner } from './ui';

export function EditorPage({ sectionKey, title, description, route, children }: {
  sectionKey: string; title: string; description?: ReactNode; route?: string | null; children: ReactNode | ((f: SectionForm) => ReactNode);
}) {
  const form = useSectionForm(sectionKey);
  const { meta } = useApp();

  // Ctrl/Cmd + S saves
  useEffect(() => {
    const h = (e: KeyboardEvent) => {
      if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === 's') { e.preventDefault(); if (form.dirty) void form.save(); }
    };
    window.addEventListener('keydown', h);
    return () => window.removeEventListener('keydown', h);
  }, [form]);

  const viewUrl = route && meta.siteUrl ? meta.siteUrl.replace(/\/$/, '') + (route === '/' ? '/' : route) : null;

  return (
    <div className="page editor">
      <header className="page-head">
        <div>
          <h1>{title}</h1>
          {description && <p className="page-desc">{description}</p>}
        </div>
        <div className="page-head-meta">
          {viewUrl && <a className="btn ghost" href={viewUrl} target="_blank" rel="noreferrer">View on site ↗</a>}
          {form.env?.updatedAtUtc && <span className="last-saved">Last saved {formatDate(form.env.updatedAtUtc)}{form.env.updatedBy && form.env.updatedBy !== 'system' ? ` by ${form.env.updatedBy}` : ''}</span>}
        </div>
      </header>

      {form.loading && !form.data && <Spinner />}
      {form.loadError && <Notice kind="error">{form.loadError}</Notice>}

      {form.data && (
        <FormProvider value={form}>
          {form.conflict && (
            <Notice kind="warn">
              Someone else saved this section while you were editing. <button className="link-btn" onClick={() => void form.reload()}>Reload their version</button> (your unsaved changes will be lost).
            </Notice>
          )}
          {typeof children === 'function' ? children(form) : children}

          <div className={`savebar${form.dirty ? ' dirty' : ''}`}>
            <div className="savebar-inner">
              <span className="savebar-status" aria-live="polite">
                {form.errorCount > 0 ? <span className="bad">{form.errorCount} field{form.errorCount > 1 ? 's' : ''} need attention</span>
                  : form.dirty ? <><span className="dot" /> Unsaved changes</> : 'All changes saved'}
              </span>
              <button className="btn" onClick={form.discard} disabled={!form.dirty || form.saving}>Discard</button>
              <button className="btn primary" onClick={() => void form.save()} disabled={!form.dirty || form.saving}>
                {form.saving ? 'Saving…' : 'Save changes'}
              </button>
            </div>
          </div>
        </FormProvider>
      )}
    </div>
  );
}
