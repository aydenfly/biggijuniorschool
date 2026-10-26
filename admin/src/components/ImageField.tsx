import { useCallback, useEffect, useRef, useState } from 'react';
import { api, ApiError, type MediaItem } from '../api';
import { uploadImage } from '../imageTools';
import { useApp, useField } from '../state';
import { resolveMedia } from '../util';
import { Field, Modal, Spinner } from './ui';

// ================================================================= upload zone (shared with the media page)

export function UploadZone({ onUploaded, compact }: { onUploaded: (items: MediaItem[]) => void; compact?: boolean }) {
  const { toast, meta } = useApp();
  const input = useRef<HTMLInputElement>(null);
  const [busy, setBusy] = useState<string | null>(null);
  const [over, setOver] = useState(false);

  const handle = async (files: FileList | File[]) => {
    const list = Array.from(files).filter(f => f.type.startsWith('image/'));
    if (list.length === 0) { toast('Please choose image files (JPG, PNG, WebP or GIF).', 'error'); return; }
    const done: MediaItem[] = [];
    for (let i = 0; i < list.length; i++) {
      setBusy(`Uploading ${i + 1} of ${list.length}…`);
      try { done.push(await uploadImage(list[i])); }
      catch (e) { toast(`${list[i].name}: ${e instanceof Error ? e.message : 'upload failed'}`, 'error'); }
    }
    setBusy(null);
    if (done.length) { toast(done.length === 1 ? 'Image uploaded.' : `${done.length} images uploaded.`); onUploaded(done); }
  };

  return (
    <div className={`dropzone${over ? ' over' : ''}${compact ? ' compact' : ''}`}
      onDragOver={e => { e.preventDefault(); setOver(true); }} onDragLeave={() => setOver(false)}
      onDrop={e => { e.preventDefault(); setOver(false); void handle(e.dataTransfer.files); }}>
      {busy ? <Spinner label={busy} /> : (
        <>
          <p><strong>Drop images here</strong> or</p>
          <button type="button" className="btn primary" onClick={() => input.current?.click()}>Choose images</button>
          <p className="hint">Large photos are resized automatically. Up to {meta.upload.maxMegabytes} MB each.</p>
        </>
      )}
      <input ref={input} type="file" accept="image/*" multiple hidden onChange={e => { if (e.target.files) void handle(e.target.files); e.target.value = ''; }} />
    </div>
  );
}

// ================================================================= picker

export function MediaPicker({ onPick, onClose, multiple }: { onPick: (items: { src: string; alt: string }[]) => void; onClose: () => void; multiple?: boolean }) {
  const { meta } = useApp();
  const [items, setItems] = useState<MediaItem[] | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [tab, setTab] = useState<'library' | 'upload' | 'path'>('library');
  const [selected, setSelected] = useState<string[]>([]);
  const [path, setPath] = useState('');

  const load = useCallback(async () => {
    try { setItems(await api.get<MediaItem[]>('/api/admin/media')); }
    catch (e) { setError(e instanceof ApiError ? e.message : 'Could not load the library.'); }
  }, []);
  useEffect(() => { void load(); }, [load]);

  const toggle = (id: string) => setSelected(s => multiple ? (s.includes(id) ? s.filter(x => x !== id) : [...s, id]) : [id]);
  const confirm = () => {
    const chosen = (items ?? []).filter(i => selected.includes(i.id)).map(i => ({ src: i.url, alt: i.alt }));
    if (chosen.length) onPick(chosen);
  };

  return (
    <Modal title="Choose an image" onClose={onClose} wide>
      <div className="tabs" role="tablist">
        {([['library', 'Library'], ['upload', 'Upload new'], ['path', 'Website image path']] as const).map(([k, label]) => (
          <button key={k} role="tab" aria-selected={tab === k} className={tab === k ? 'on' : ''} onClick={() => setTab(k)}>{label}</button>
        ))}
      </div>

      <div className="modal-body scroll">
        {tab === 'library' && (
          error ? <p className="field-error">{error}</p> :
          !items ? <Spinner /> :
          items.length === 0 ? <p className="repeater-empty">No uploaded images yet. Use “Upload new” to add some.</p> :
          <ul className="media-grid pick">
            {items.map(m => (
              <li key={m.id}>
                <button type="button" className={`media-tile${selected.includes(m.id) ? ' selected' : ''}`} onClick={() => toggle(m.id)}
                  onDoubleClick={() => { if (!multiple) onPick([{ src: m.url, alt: m.alt }]); }} aria-pressed={selected.includes(m.id)}>
                  <img src={m.url.replace('-4k.', m.variants.includes(320) ? '-320.' : '-4k.')} alt="" loading="lazy" />
                  <span className="media-name">{m.alt || m.originalName}</span>
                  {selected.includes(m.id) && <span className="tick" aria-hidden="true">✓</span>}
                </button>
              </li>
            ))}
          </ul>
        )}

        {tab === 'upload' && (
          <UploadZone onUploaded={async uploaded => {
            await load();
            if (!multiple && uploaded.length === 1) onPick([{ src: uploaded[0].url, alt: uploaded[0].alt }]);
            else { setSelected(uploaded.map(u => u.id)); setTab('library'); }
          }} />
        )}

        {tab === 'path' && (
          <div className="path-pick">
            <p>Images that are already part of the website can be used by their path, for example <code>/images/hero/hero-1-4k.webp</code>.</p>
            <Field label="Image path or address">
              <input type="text" value={path} placeholder="/images/…" onChange={e => setPath(e.target.value)} />
            </Field>
            {path && <img className="path-preview" src={resolveMedia(path, meta.siteUrl)} alt="Preview" onError={e => (e.currentTarget.style.opacity = '.25')} />}
            <button className="btn primary" disabled={!path.trim()} onClick={() => onPick([{ src: path.trim(), alt: '' }])}>Use this image</button>
          </div>
        )}
      </div>

      {tab === 'library' && (
        <div className="modal-actions">
          <span className="hint">{selected.length ? `${selected.length} selected` : 'Click an image to select it.'}</span>
          <button className="btn" onClick={onClose}>Cancel</button>
          <button className="btn primary" disabled={selected.length === 0} onClick={confirm}>{multiple ? 'Add selected' : 'Use image'}</button>
        </div>
      )}
    </Modal>
  );
}

// ================================================================= field

export function ImageField({ path, label, altPath, hint, optional = true, shape = 'wide' }: {
  path: string; label: string; altPath?: string; hint?: React.ReactNode; optional?: boolean; shape?: 'wide' | 'square' | 'portrait';
}) {
  const { meta } = useApp();
  const f = useField<string>(path);
  const alt = useField<string>(altPath ?? '__none');
  const [open, setOpen] = useState(false);
  const src = f.value ?? '';

  return (
    <Field label={label} hint={hint} error={f.error} optional={optional && !src}>
      <div className={`image-field ${shape}`}>
        <div className="thumb-box">
          {src ? <img src={resolveMedia(src, meta.siteUrl)} alt="" onError={e => (e.currentTarget.style.visibility = 'hidden')} /> : <span>No image</span>}
        </div>
        <div className="image-actions">
          <button type="button" className="btn" onClick={() => setOpen(true)}>{src ? 'Change image' : 'Choose image'}</button>
          {src && <button type="button" className="btn ghost danger" onClick={() => f.set('')}>Remove</button>}
          {src && <code className="src-path" title={src}>{src.length > 44 ? '…' + src.slice(-42) : src}</code>}
        </div>
      </div>
      {altPath && src && (
        <div className="alt-input">
          <input type="text" value={alt.value ?? ''} placeholder="Describe the image for people who cannot see it (alt text)" aria-label={`${label} description`}
            onChange={e => alt.set(e.target.value)} />
          {alt.error && <p className="field-error">{alt.error}</p>}
        </div>
      )}
      {open && <MediaPicker onClose={() => setOpen(false)} onPick={([m]) => {
        f.set(m.src);
        if (altPath && !alt.value && m.alt) alt.set(m.alt);
        setOpen(false);
      }} />}
    </Field>
  );
}
