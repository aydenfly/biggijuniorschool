import { useId, useState, type ReactNode } from 'react';
import { useApp, useField, useForm } from '../state';
import { getPath } from '../util';

// ================================================================= basic field chrome

export function Field({ label, hint, error, htmlFor, children, optional }: {
  label: string; hint?: ReactNode; error?: string; htmlFor?: string; children: ReactNode; optional?: boolean;
}) {
  return (
    <div className={`field${error ? ' has-error' : ''}`}>
      <label htmlFor={htmlFor}>{label}{optional && <span className="optional"> optional</span>}</label>
      {children}
      {error ? <p className="field-error" role="alert">{error}</p> : hint ? <p className="hint">{hint}</p> : null}
    </div>
  );
}

function Counter({ value, max }: { value: string; max: number }) {
  const left = max - value.length;
  return <span className={`counter${left < 0 ? ' over' : left < max * 0.1 ? ' near' : ''}`}>{value.length}/{max}</span>;
}

export function TextField({ path, label, hint, placeholder, max, optional, type = 'text', prefix }: {
  path: string; label: string; hint?: ReactNode; placeholder?: string; max?: number; optional?: boolean; type?: string; prefix?: string;
}) {
  const id = useId();
  const f = useField<string>(path);
  const form = useForm();
  const value = f.value ?? '';
  return (
    <Field label={label} hint={hint} error={f.error} htmlFor={id} optional={optional}>
      <div className="input-wrap">
        {prefix && <span className="input-prefix">{prefix}</span>}
        <input id={id} type={type} value={value} placeholder={placeholder} disabled={form.readOnly}
          onChange={e => f.set(e.target.value)} aria-invalid={!!f.error} />
        {max !== undefined && <Counter value={value} max={max} />}
      </div>
    </Field>
  );
}

export function AreaField({ path, label, hint, placeholder, max, rows = 4, optional }: {
  path: string; label: string; hint?: ReactNode; placeholder?: string; max?: number; rows?: number; optional?: boolean;
}) {
  const id = useId();
  const f = useField<string>(path);
  const value = f.value ?? '';
  return (
    <Field label={label} hint={hint} error={f.error} htmlFor={id} optional={optional}>
      <div className="input-wrap textarea">
        <textarea id={id} rows={rows} value={value} placeholder={placeholder} onChange={e => f.set(e.target.value)} aria-invalid={!!f.error} />
        {max !== undefined && <Counter value={value} max={max} />}
      </div>
    </Field>
  );
}

export function NumberField({ path, label, hint, min, max, suffix }: {
  path: string; label: string; hint?: ReactNode; min?: number; max?: number; suffix?: string;
}) {
  const id = useId();
  const f = useField<number>(path);
  return (
    <Field label={label} hint={hint} error={f.error} htmlFor={id}>
      <div className="input-wrap narrow">
        <input id={id} type="number" min={min} max={max} value={f.value ?? ''} onChange={e => f.set(Number(e.target.value))} />
        {suffix && <span className="input-suffix">{suffix}</span>}
      </div>
    </Field>
  );
}

export function ToggleField({ path, label, hint }: { path: string; label: string; hint?: ReactNode }) {
  const id = useId();
  const f = useField<boolean>(path);
  return (
    <div className="field toggle-field">
      <label className="toggle" htmlFor={id}>
        <input id={id} type="checkbox" role="switch" checked={!!f.value} onChange={e => f.set(e.target.checked)} />
        <span className="track" aria-hidden="true"><span className="thumb" /></span>
        <span className="toggle-text">{label}</span>
      </label>
      {hint && <p className="hint">{hint}</p>}
    </div>
  );
}

export function SelectField({ path, label, hint, options }: {
  path: string; label: string; hint?: ReactNode; options: { value: string; label: string }[];
}) {
  const id = useId();
  const f = useField<string>(path);
  return (
    <Field label={label} hint={hint} error={f.error} htmlFor={id}>
      <select id={id} value={f.value ?? ''} onChange={e => f.set(e.target.value)}>
        {options.map(o => <option key={o.value} value={o.value}>{o.label}</option>)}
      </select>
    </Field>
  );
}

// ================================================================= link target (internal page or external address)

const CUSTOM = '__custom';

/** Choose a page on this site, or type any address. */
export function LinkTargetField({ path, label = 'Link to', hint, allowAnchors = true }: { path: string; label?: string; hint?: ReactNode; allowAnchors?: boolean }) {
  const id = useId();
  const { meta } = useApp();
  const f = useField<string>(path);
  const value = f.value ?? '';
  const known = meta.routes.some(r => r.path === value);
  const [forceCustom, setForceCustom] = useState(false);
  const custom = forceCustom || (!known && value !== '');
  return (
    <Field label={label} hint={hint} error={f.error} htmlFor={id}>
      <div className="link-target">
        <select id={id} value={custom ? CUSTOM : value}
          onChange={e => {
            if (e.target.value === CUSTOM) { setForceCustom(true); if (known) f.set(''); }
            else { setForceCustom(false); f.set(e.target.value); }
          }}>
          <option value="">Choose…</option>
          <optgroup label="Pages on this site">
            {meta.routes.map(r => <option key={r.path} value={r.path}>{r.label}</option>)}
          </optgroup>
          <option value={CUSTOM}>Another address (external link, email, phone)…</option>
        </select>
        {custom && (
          <input type="text" aria-label="Address" value={value} placeholder={allowAnchors ? 'https://…, /page, #section, mailto:…, tel:…' : 'https://…'}
            onChange={e => f.set(e.target.value)} aria-invalid={!!f.error} />
        )}
      </div>
    </Field>
  );
}

// ================================================================= layout helpers

export function Panel({ title, description, children, aside }: { title?: string; description?: ReactNode; children: ReactNode; aside?: ReactNode }) {
  return (
    <section className="panel">
      {(title || aside) && (
        <header className="panel-head">
          <div>
            {title && <h2>{title}</h2>}
            {description && <p className="panel-desc">{description}</p>}
          </div>
          {aside}
        </header>
      )}
      <div className="panel-body">{children}</div>
    </section>
  );
}

export function Row({ children, cols = 2 }: { children: ReactNode; cols?: 2 | 3 }) {
  return <div className={`row cols-${cols}`}>{children}</div>;
}

export function Notice({ kind = 'info', children }: { kind?: 'info' | 'warn' | 'error'; children: ReactNode }) {
  return <div className={`notice ${kind}`} role={kind === 'error' ? 'alert' : undefined}>{children}</div>;
}

// ================================================================= repeater (ordered list of items)

export function Repeater({ path, newItem, title, summary, max, addLabel = 'Add item', empty = 'Nothing here yet.', children, startOpen = false, minItems = 0 }: {
  path: string;
  newItem: () => any;
  title: (item: any, index: number) => string;
  summary?: (item: any) => ReactNode;
  max?: number;
  addLabel?: string;
  empty?: ReactNode;
  startOpen?: boolean;
  minItems?: number;
  children: (base: string, index: number, item: any) => ReactNode;
}) {
  const form = useForm();
  const { confirm } = useApp();
  const items: any[] = getPath(form.data, path) ?? [];
  const [open, setOpen] = useState<Record<number, boolean>>(() => (startOpen ? Object.fromEntries(items.map((_, i) => [i, true])) : {}));
  const listError = form.errors[path]?.[0];

  const move = (i: number, d: number) => {
    const j = i + d;
    if (j < 0 || j >= items.length) return;
    form.update(draft => {
      const arr = getPath(draft, path) as any[];
      [arr[i], arr[j]] = [arr[j], arr[i]];
    });
    setOpen(o => ({ ...o, [i]: !!o[j], [j]: !!o[i] }));
  };

  const remove = async (i: number) => {
    const name = title(items[i], i);
    if (!(await confirm({ title: `Remove “${name}”?`, body: 'It disappears from the site once you save.', confirmLabel: 'Remove', danger: true }))) return;
    form.update(draft => { (getPath(draft, path) as any[]).splice(i, 1); });
    setOpen({});
  };

  const add = () => {
    form.update(draft => {
      const arr = getPath(draft, path) as any[];
      arr.push(newItem());
    });
    setOpen(o => ({ ...o, [items.length]: true }));
  };

  return (
    <div className="repeater">
      {items.length === 0 && <p className="repeater-empty">{empty}</p>}
      {items.map((item, i) => {
        const isOpen = !!open[i];
        const base = `${path}[${i}]`;
        const hasError = Object.keys(form.errors).some(k => k.startsWith(base + '.') || k === base);
        return (
          <div key={i} className={`rep-item${isOpen ? ' open' : ''}${hasError ? ' has-error' : ''}`}>
            <div className="rep-head">
              <button type="button" className="rep-toggle" aria-expanded={isOpen || hasError} onClick={() => setOpen(o => ({ ...o, [i]: !o[i] }))}>
                <span className="rep-num">{i + 1}</span>
                <span className="rep-title">{title(item, i) || <em>Untitled</em>}</span>
                {summary && <span className="rep-summary">{summary(item)}</span>}
                {hasError && <span className="rep-flag">Needs attention</span>}
                <span className="chev" aria-hidden="true" />
              </button>
              <div className="rep-actions">
                <button type="button" className="icon-btn" onClick={() => move(i, -1)} disabled={i === 0} aria-label={`Move ${title(item, i)} up`} title="Move up">↑</button>
                <button type="button" className="icon-btn" onClick={() => move(i, 1)} disabled={i === items.length - 1} aria-label={`Move ${title(item, i)} down`} title="Move down">↓</button>
                <button type="button" className="icon-btn danger" onClick={() => remove(i)} disabled={items.length <= minItems} aria-label={`Remove ${title(item, i)}`} title="Remove">✕</button>
              </div>
            </div>
            {(isOpen || hasError) && <div className="rep-body">{children(base, i, item)}</div>}
          </div>
        );
      })}
      {listError && <p className="field-error">{listError}</p>}
      <button type="button" className="btn add" onClick={add} disabled={max !== undefined && items.length >= max}>+ {addLabel}</button>
      {max !== undefined && items.length >= max && <p className="hint">You have reached the limit of {max}.</p>}
    </div>
  );
}

// ================================================================= modal

export function Modal({ title, onClose, children, wide }: { title: string; onClose: () => void; children: ReactNode; wide?: boolean }) {
  return (
    <div className="modal-backdrop" onMouseDown={e => { if (e.target === e.currentTarget) onClose(); }}
      onKeyDown={e => { if (e.key === 'Escape') onClose(); }}>
      <div className={`modal${wide ? ' wide' : ''}`} role="dialog" aria-modal="true" aria-label={title}>
        <header className="modal-head">
          <h2>{title}</h2>
          <button className="icon-btn" onClick={onClose} aria-label="Close">✕</button>
        </header>
        {children}
      </div>
    </div>
  );
}

export function Spinner({ label = 'Loading…' }: { label?: string }) {
  return <div className="spinner" role="status"><span className="spin" aria-hidden="true" />{label}</div>;
}
