import { useCallback, useEffect, useState, type FormEvent } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { api, ApiError, type Dashboard, type EnquiryItem, type Envelope, type MediaItem, type SectionInfo, type UserItem } from '../api';
import { UploadZone } from '../components/ImageField';
import { Field, Modal, Notice, Spinner } from '../components/ui';
import { useApp } from '../state';
import { fileSize, formatDate, timeAgo } from '../util';

function useLoad<T>(url: string, deps: unknown[] = []) {
  const [data, setData] = useState<T | null>(null);
  const [error, setError] = useState<string | null>(null);
  const { refreshKey } = useApp();
  const load = useCallback(async () => {
    try { setData(await api.get<T>(url)); setError(null); }
    catch (e) { setError(e instanceof ApiError ? e.message : 'Could not load.'); }
  }, [url]);
  // eslint-disable-next-line react-hooks/exhaustive-deps
  useEffect(() => { void load(); }, [load, refreshKey, ...deps]);
  return { data, setData, error, reload: load };
}

function greeting() {
  const h = new Date().getHours();
  return h < 12 ? 'Good morning' : h < 18 ? 'Good afternoon' : 'Good evening';
}

// ================================================================= dashboard

export function DashboardPage() {
  const { me } = useApp();
  const { data, error } = useLoad<Dashboard>('/api/admin/dashboard');
  if (error) return <div className="page"><Notice kind="error">{error}</Notice></div>;
  if (!data) return <div className="page"><Spinner /></div>;

  const todo = data.checklist.filter(c => !c.ok);
  const done = data.checklist.length - todo.length;

  return (
    <div className="page dashboard">
      <header className="page-head">
        <div>
          <h1>{greeting()}, {me.name.split(' ')[0]}</h1>
          <p className="page-desc">Everything you change here goes live on the website as soon as you save.</p>
        </div>
      </header>

      <dl className="stat-line">
        <div><dt>Unread enquiries</dt><dd className={data.stats.enquiriesUnread ? 'hot' : ''}>{data.stats.enquiriesUnread}</dd></div>
        <div><dt>Pages with meta data</dt><dd>{data.stats.seoComplete}<small>/{data.stats.seoTotal}</small></dd></div>
        <div><dt>Images in library</dt><dd>{data.stats.media}</dd></div>
        <div><dt>Editable sections</dt><dd>{data.stats.sections}</dd></div>
      </dl>

      <div className="dash-grid">
        <section className="panel">
          <header className="panel-head">
            <div><h2>Setup checklist</h2><p className="panel-desc">{todo.length === 0 ? 'Everything is in place.' : `${done} of ${data.checklist.length} done`}</p></div>
          </header>
          <ul className="checklist">
            {data.checklist.map(c => (
              <li key={c.id} className={c.ok ? 'ok' : 'todo'}>
                <span className="check" aria-hidden="true">{c.ok ? '✓' : ''}</span>
                <div>
                  <span className="check-label">{c.label}</span>
                  {!c.ok && c.detail && <span className="check-detail">{c.detail}</span>}
                </div>
                {!c.ok && c.link && <Link className="btn small" to={c.link}>Fix</Link>}
              </li>
            ))}
          </ul>
        </section>

        <div className="stack">
          <section className="panel">
            <header className="panel-head"><div><h2>Recently edited</h2></div></header>
            {data.recentEdits.length === 0 ? <p className="repeater-empty">Nothing has been edited yet.</p> : (
              <ul className="plain-list">
                {data.recentEdits.map(r => (
                  <li key={r.key}>
                    <Link to={r.key === 'general' ? '/settings/general' : `/content/${r.key}`}>{r.title}</Link>
                    <span className="muted">{r.updatedBy} · {timeAgo(r.updatedAtUtc)}</span>
                  </li>
                ))}
              </ul>
            )}
          </section>

          <section className="panel">
            <header className="panel-head">
              <div><h2>Latest enquiries</h2></div>
              <Link className="btn small ghost" to="/enquiries">Open inbox</Link>
            </header>
            {data.recentEnquiries.length === 0 ? <p className="repeater-empty">No messages yet.</p> : (
              <ul className="plain-list">
                {data.recentEnquiries.map(e => (
                  <li key={e.id}>
                    <Link to="/enquiries" className={e.read ? '' : 'unread'}>{e.fullName}</Link>
                    <span className="muted">{timeAgo(e.createdAtUtc)}</span>
                  </li>
                ))}
              </ul>
            )}
          </section>
        </div>
      </div>
    </div>
  );
}

// ================================================================= media library

export function MediaPage() {
  const { toast, confirm, meta } = useApp();
  const { data, reload, error } = useLoad<MediaItem[]>('/api/admin/media');
  const [active, setActive] = useState<MediaItem | null>(null);
  const [alt, setAlt] = useState('');

  useEffect(() => { setAlt(active?.alt ?? ''); }, [active]);

  const saveAlt = async () => {
    if (!active) return;
    try { await api.patch(`/api/admin/media/${active.id}`, { alt }); toast('Description saved.'); await reload(); setActive(a => a && { ...a, alt }); }
    catch (e) { toast(e instanceof ApiError ? e.message : 'Could not save.', 'error'); }
  };

  const remove = async () => {
    if (!active) return;
    const used = active.usedIn.length > 0;
    const ok = await confirm({
      title: 'Delete this image?',
      body: used ? <>This image is used in <b>{active.usedIn.join(', ')}</b>. Those places will show a broken image until you choose another. </> : 'This cannot be undone.',
      confirmLabel: 'Delete image', danger: true,
    });
    if (!ok) return;
    try { await api.del(`/api/admin/media/${active.id}`); toast('Image deleted.'); setActive(null); await reload(); }
    catch (e) { toast(e instanceof ApiError ? e.message : 'Could not delete.', 'error'); }
  };

  return (
    <div className="page">
      <header className="page-head">
        <div><h1>Media library</h1><p className="page-desc">Images you upload here can be used anywhere on the site. Each upload is resized into {meta.upload.variants.length + 1} sizes so pages load quickly on phones.</p></div>
      </header>
      <section className="panel"><div className="panel-body"><UploadZone onUploaded={() => void reload()} /></div></section>
      {error && <Notice kind="error">{error}</Notice>}
      {!data ? <Spinner /> : data.length === 0 ? <p className="repeater-empty">No images uploaded yet.</p> : (
        <ul className="media-grid">
          {data.map(m => (
            <li key={m.id}>
              <button type="button" className="media-tile" onClick={() => setActive(m)}>
                <img src={m.url.replace('-4k.', m.variants.includes(320) ? '-320.' : '-4k.')} alt="" loading="lazy" />
                <span className="media-name">{m.alt || m.originalName}</span>
                {m.usedIn.length > 0 && <span className="used-badge">In use</span>}
              </button>
            </li>
          ))}
        </ul>
      )}

      {active && (
        <Modal title="Image details" onClose={() => setActive(null)} wide>
          <div className="media-detail">
            <img src={active.url} alt={active.alt || ''} />
            <div>
              <Field label="Description (alt text)" hint="Read aloud by screen readers and shown if the image cannot load.">
                <input type="text" value={alt} maxLength={200} onChange={e => setAlt(e.target.value)} />
              </Field>
              <button className="btn primary" disabled={alt === active.alt} onClick={saveAlt}>Save description</button>
              <dl className="facts">
                <dt>File</dt><dd>{active.originalName}</dd>
                <dt>Size</dt><dd>{active.width && active.height ? `${active.width} × ${active.height} px · ` : ''}{fileSize(active.size)}</dd>
                <dt>Uploaded</dt><dd>{formatDate(active.uploadedAtUtc)} by {active.uploadedBy}</dd>
                <dt>Used in</dt><dd>{active.usedIn.length ? active.usedIn.join(', ') : 'Not used anywhere'}</dd>
                <dt>Path</dt><dd><code>{active.url}</code></dd>
              </dl>
              <button className="btn danger" onClick={remove}>Delete image</button>
            </div>
          </div>
        </Modal>
      )}
    </div>
  );
}

// ================================================================= enquiries

export function EnquiriesPage() {
  const { toast, confirm, bump } = useApp();
  const { data, setData, error } = useLoad<EnquiryItem[]>('/api/admin/enquiries');
  const [openId, setOpenId] = useState<string | null>(null);
  const open = data?.find(e => e.id === openId) ?? null;

  const select = async (e: EnquiryItem) => {
    setOpenId(e.id);
    if (!e.read) {
      try {
        await api.patch(`/api/admin/enquiries/${e.id}`, { read: true });
        setData(d => d && d.map(x => x.id === e.id ? { ...x, read: true } : x));
        bump();
      } catch { /* non-critical */ }
    }
  };

  const markUnread = async (e: EnquiryItem) => {
    await api.patch(`/api/admin/enquiries/${e.id}`, { read: false });
    setData(d => d && d.map(x => x.id === e.id ? { ...x, read: false } : x));
    setOpenId(null); bump();
  };

  const remove = async (e: EnquiryItem) => {
    if (!(await confirm({ title: 'Delete this message?', body: `From ${e.fullName}. This cannot be undone.`, confirmLabel: 'Delete', danger: true }))) return;
    try { await api.del(`/api/admin/enquiries/${e.id}`); setData(d => d && d.filter(x => x.id !== e.id)); setOpenId(null); bump(); toast('Message deleted.'); }
    catch (err) { toast(err instanceof ApiError ? err.message : 'Could not delete.', 'error'); }
  };

  return (
    <div className="page">
      <header className="page-head"><div><h1>Enquiries</h1><p className="page-desc">Messages sent through the contact form. A copy is always kept here, even if the email fails.</p></div></header>
      {error && <Notice kind="error">{error}</Notice>}
      {!data ? <Spinner /> : data.length === 0 ? <p className="repeater-empty">No messages yet.</p> : (
        <div className="inbox">
          <ul className="inbox-list">
            {data.map(e => (
              <li key={e.id}>
                <button className={`inbox-row${e.id === openId ? ' on' : ''}${e.read ? '' : ' unread'}`} onClick={() => void select(e)}>
                  <span className="inbox-name">{e.fullName}</span>
                  <span className="inbox-time">{timeAgo(e.createdAtUtc)}</span>
                  <span className="inbox-snippet">{e.message}</span>
                </button>
              </li>
            ))}
          </ul>
          <article className="inbox-detail">
            {!open ? <p className="repeater-empty">Select a message to read it.</p> : (
              <>
                <h2>{open.fullName}</h2>
                <p className="muted"><a href={`mailto:${open.email}`}>{open.email}</a> · {formatDate(open.createdAtUtc)}</p>
                {open.delivery === 'failed' && <Notice kind="warn">The notification email for this message could not be sent, so this is the only copy.</Notice>}
                <p className="message-body">{open.message}</p>
                <div className="btn-row">
                  <a className="btn primary" href={`mailto:${open.email}?subject=${encodeURIComponent('Re: your enquiry')}`}>Reply by email</a>
                  <button className="btn" onClick={() => void markUnread(open)}>Mark unread</button>
                  <button className="btn ghost danger" onClick={() => void remove(open)}>Delete</button>
                </div>
              </>
            )}
          </article>
        </div>
      )}
    </div>
  );
}

// ================================================================= SEO overview

export function SeoOverviewPage() {
  const { data: sections } = useLoad<SectionInfo[]>('/api/admin/sections');
  const [rows, setRows] = useState<{ info: SectionInfo; title: string; description: string }[] | null>(null);

  useEffect(() => {
    if (!sections) return;
    void Promise.all(sections.filter(s => s.hasSeo).map(async info => {
      const env = await api.get<Envelope>(`/api/admin/content/${info.key}`);
      return { info, title: env.data.seo?.title ?? '', description: env.data.seo?.description ?? '' };
    })).then(setRows);
  }, [sections]);

  return (
    <div className="page">
      <header className="page-head"><div><h1>Meta data overview</h1><p className="page-desc">The title and description search engines show for each page. Click a page to edit.</p></div></header>
      {!rows ? <Spinner /> : (
        <div className="table-wrap">
          <table className="table">
            <thead><tr><th>Page</th><th>Meta title</th><th>Meta description</th><th>Status</th></tr></thead>
            <tbody>
              {rows.map(r => {
                const tOk = r.title.length >= 20 && r.title.length <= 70;
                const dOk = r.description.length >= 70 && r.description.length <= 200;
                return (
                  <tr key={r.info.key}>
                    <td><Link to={`/content/${r.info.key}`}>{r.info.title}</Link></td>
                    <td><span className={tOk ? '' : 'warn-text'}>{r.title || <em>Missing</em>}</span><small className="muted"> {r.title.length}</small></td>
                    <td><span className={dOk ? '' : 'warn-text'}>{r.description || <em>Missing</em>}</span><small className="muted"> {r.description.length}</small></td>
                    <td>{r.title && r.description ? (tOk && dOk ? <span className="badge ok">Good</span> : <span className="badge warn">Check length</span>) : <span className="badge bad">Incomplete</span>}</td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}

// ================================================================= users

export function UsersPage() {
  const { me, toast, confirm } = useApp();
  const { data, reload, error } = useLoad<UserItem[]>('/api/admin/users');
  const [adding, setAdding] = useState(false);
  const [resetting, setResetting] = useState<UserItem | null>(null);

  const changeRole = async (u: UserItem, role: string) => {
    try { await api.patch(`/api/admin/users/${u.id}`, { role }); toast('Role updated.'); await reload(); }
    catch (e) { toast(e instanceof ApiError ? e.message : 'Could not update.', 'error'); }
  };
  const remove = async (u: UserItem) => {
    if (!(await confirm({ title: `Remove ${u.name}?`, body: 'They will no longer be able to sign in.', confirmLabel: 'Remove user', danger: true }))) return;
    try { await api.del(`/api/admin/users/${u.id}`); toast('User removed.'); await reload(); }
    catch (e) { toast(e instanceof ApiError ? e.message : 'Could not remove.', 'error'); }
  };

  return (
    <div className="page">
      <header className="page-head">
        <div><h1>Users</h1><p className="page-desc">Administrators can change everything. Editors can edit page content, media and enquiries, but not General settings or users.</p></div>
        <button className="btn primary" onClick={() => setAdding(true)}>Add user</button>
      </header>
      {error && <Notice kind="error">{error}</Notice>}
      {!data ? <Spinner /> : (
        <div className="table-wrap">
          <table className="table">
            <thead><tr><th>Name</th><th>Email</th><th>Role</th><th>Last sign-in</th><th /></tr></thead>
            <tbody>
              {data.map(u => (
                <tr key={u.id}>
                  <td>{u.name}{u.id === me.id && <span className="badge"> You</span>}</td>
                  <td>{u.email}</td>
                  <td>
                    <select value={u.role} disabled={u.id === me.id} aria-label={`Role of ${u.name}`} onChange={e => void changeRole(u, e.target.value)}>
                      <option>Admin</option><option>Editor</option>
                    </select>
                  </td>
                  <td className="muted">{u.lastLoginUtc ? timeAgo(u.lastLoginUtc) : 'Never'}</td>
                  <td className="actions">
                    <button className="btn small" onClick={() => setResetting(u)}>Reset password</button>
                    {u.id !== me.id && <button className="btn small ghost danger" onClick={() => void remove(u)}>Remove</button>}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
      {adding && <UserForm onClose={() => setAdding(false)} onDone={async () => { setAdding(false); await reload(); }} />}
      {resetting && <ResetForm user={resetting} onClose={() => setResetting(null)} />}
    </div>
  );
}

function UserForm({ onClose, onDone }: { onClose: () => void; onDone: () => void }) {
  const { toast } = useApp();
  const [f, setF] = useState({ name: '', email: '', role: 'Editor', password: '' });
  const [err, setErr] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const submit = async (e: FormEvent) => {
    e.preventDefault(); setBusy(true); setErr(null);
    try { await api.post('/api/admin/users', f); toast('User added. They will be asked to choose a new password at first sign-in.'); onDone(); }
    catch (x) { setErr(x instanceof ApiError ? x.message : 'Could not add the user.'); }
    finally { setBusy(false); }
  };
  return (
    <Modal title="Add user" onClose={onClose}>
      <form onSubmit={submit} className="modal-body">
        {err && <Notice kind="error">{err}</Notice>}
        <Field label="Name"><input value={f.name} onChange={e => setF({ ...f, name: e.target.value })} required autoFocus /></Field>
        <Field label="Email"><input type="email" value={f.email} onChange={e => setF({ ...f, email: e.target.value })} required /></Field>
        <Field label="Role"><select value={f.role} onChange={e => setF({ ...f, role: e.target.value })}><option>Editor</option><option>Admin</option></select></Field>
        <Field label="Temporary password" hint="At least 10 characters. Share it privately; they must change it when they first sign in.">
          <input type="text" value={f.password} onChange={e => setF({ ...f, password: e.target.value })} required minLength={10} autoComplete="off" />
        </Field>
        <div className="modal-actions"><button type="button" className="btn" onClick={onClose}>Cancel</button><button className="btn primary" disabled={busy}>Add user</button></div>
      </form>
    </Modal>
  );
}

function ResetForm({ user, onClose }: { user: UserItem; onClose: () => void }) {
  const { toast } = useApp();
  const [pw, setPw] = useState('');
  const [err, setErr] = useState<string | null>(null);
  const submit = async (e: FormEvent) => {
    e.preventDefault(); setErr(null);
    try { await api.post(`/api/admin/users/${user.id}/reset-password`, { newPassword: pw }); toast('Password reset. They must choose a new one at next sign-in.'); onClose(); }
    catch (x) { setErr(x instanceof ApiError ? x.message : 'Could not reset the password.'); }
  };
  return (
    <Modal title={`Reset password for ${user.name}`} onClose={onClose}>
      <form onSubmit={submit} className="modal-body">
        {err && <Notice kind="error">{err}</Notice>}
        <Field label="New temporary password" hint="At least 10 characters."><input type="text" value={pw} onChange={e => setPw(e.target.value)} required minLength={10} autoFocus autoComplete="off" /></Field>
        <div className="modal-actions"><button type="button" className="btn" onClick={onClose}>Cancel</button><button className="btn primary">Reset password</button></div>
      </form>
    </Modal>
  );
}

// ================================================================= account

export function AccountPage() {
  const { me, setMe, toast } = useApp();
  const navigate = useNavigate();
  const [f, setF] = useState({ current: '', next: '', again: '' });
  const [err, setErr] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  const submit = async (e: FormEvent) => {
    e.preventDefault(); setErr(null);
    if (f.next !== f.again) { setErr('The new passwords do not match.'); return; }
    setBusy(true);
    try {
      const updated = await api.post<typeof me>('/api/auth/change-password', { currentPassword: f.current, newPassword: f.next });
      setMe(updated); setF({ current: '', next: '', again: '' }); toast('Password changed.');
      if (me.mustChangePassword) navigate('/');
    } catch (x) { setErr(x instanceof ApiError ? x.message : 'Could not change the password.'); }
    finally { setBusy(false); }
  };

  return (
    <div className="page narrow">
      <header className="page-head"><div><h1>Your account</h1><p className="page-desc">{me.name} · {me.email} · {me.role}</p></div></header>
      {me.mustChangePassword && <Notice kind="warn">You are using a temporary password. Please choose your own before continuing.</Notice>}
      <form className="panel" onSubmit={submit}>
        <header className="panel-head"><div><h2>Change password</h2></div></header>
        <div className="panel-body">
          {err && <Notice kind="error">{err}</Notice>}
          <Field label="Current password"><input type="password" value={f.current} onChange={e => setF({ ...f, current: e.target.value })} autoComplete="current-password" required /></Field>
          <Field label="New password" hint="At least 10 characters."><input type="password" value={f.next} onChange={e => setF({ ...f, next: e.target.value })} autoComplete="new-password" required minLength={10} /></Field>
          <Field label="Repeat new password"><input type="password" value={f.again} onChange={e => setF({ ...f, again: e.target.value })} autoComplete="new-password" required /></Field>
          <button className="btn primary" disabled={busy}>Change password</button>
        </div>
      </form>
    </div>
  );
}
