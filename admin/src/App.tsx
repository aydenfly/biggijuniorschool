import { useEffect, useMemo, useState, type FormEvent } from 'react';
import { createBrowserRouter, Navigate, NavLink, Outlet, RouterProvider, useLocation } from 'react-router-dom';
import { api, ApiError, setUnauthorizedHandler, type Me, type Meta } from './api';
import { Field, Notice, Spinner } from './components/ui';
import { AppProvider, useApp } from './state';
import { AccountPage, DashboardPage, EnquiriesPage, MediaPage, SeoOverviewPage, UsersPage } from './pages/Manage';
import {
  AboutPage, BottomStripPage, ContactPage, FooterPage, GalleryPage, GeneralPage, HeroPage, LeadershipPage, MenuPage, TextPage, TuitionPage,
} from './pages/Sections';

// ================================================================= login

function Login({ onLogin }: { onLogin: (m: Me) => void }) {
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  const submit = async (e: FormEvent) => {
    e.preventDefault(); setBusy(true); setError(null);
    try { onLogin(await api.post<Me>('/api/auth/login', { email, password })); }
    catch (x) { setError(x instanceof ApiError ? x.message : 'Could not reach the server.'); }
    finally { setBusy(false); }
  };

  return (
    <main className="login">
      <form onSubmit={submit} className="login-card">
        <div className="brand big">
            <p className="font-display text-2xl text-gilt">biggi</p>
            <p className="mt-1 text-xs tracking-widest uppercase">Content studio</p>
        </div>
        <h1>Sign in</h1>
        {error && <Notice kind="error">{error}</Notice>}
        <Field label="Email"><input type="email" value={email} onChange={e => setEmail(e.target.value)} autoComplete="username" required autoFocus /></Field>
        <Field label="Password"><input type="password" value={password} onChange={e => setPassword(e.target.value)} autoComplete="current-password" required /></Field>
        <button className="btn primary block" disabled={busy}>{busy ? 'Signing in…' : 'Sign in'}</button>
      </form>
    </main>
  );
}

// ================================================================= layout

const PAGES = [
  { to: '/content/hero', label: 'Homepage hero' },
  { to: '/content/about', label: 'About us' },
  { to: '/content/leadership', label: 'Leadership' },
  { to: '/content/history', label: 'History' },
  { to: '/content/socialwall', label: 'Social wall' },
  { to: '/content/activities', label: 'Activities & facilities' },
  { to: '/content/contact', label: 'Get in touch' },
  { to: '/content/tuition', label: 'Tuition & fees' },
  { to: '/content/privacy', label: 'Privacy & cookies' },
];
const LAYOUT = [
  { to: '/content/menu', label: 'Main menu' },
  { to: '/content/footer', label: 'Footer' },
  { to: '/content/bottomstrip', label: 'Bottom strip' },
];

function Layout() {
  const { me, setMe, meta, refreshKey } = useApp();
  const [unread, setUnread] = useState(0);
  const [navOpen, setNavOpen] = useState(false);
  const loc = useLocation();

  useEffect(() => { setNavOpen(false); }, [loc.pathname]);
  useEffect(() => {
    api.get<{ stats: { enquiriesUnread: number } }>('/api/admin/dashboard').then(d => setUnread(d.stats.enquiriesUnread)).catch(() => {});
  }, [refreshKey, loc.pathname]);

  if (me.mustChangePassword && loc.pathname !== '/account') return <Navigate to="/account" replace />;

  const signOut = async () => { try { await api.post('/api/auth/logout'); } finally { setMe(null); } };
  const link = (to: string, label: string, badge?: number) => (
    <NavLink key={to} to={to} end={to === '/'} className={({ isActive }) => (isActive ? 'on' : '')}>
      {label}{!!badge && <span className="nav-badge">{badge}</span>}
    </NavLink>
  );

  return (
    <div className="shell">
      <header className="topbar">
        <button className="icon-btn" aria-label="Menu" aria-expanded={navOpen} onClick={() => setNavOpen(o => !o)}>☰</button>
        <strong>Biggi CMS</strong>
      </header>
      <aside className={`sidebar${navOpen ? ' open' : ''}`}>
        <div className="brand">
            <p className="font-display text-2xl text-gilt">biggi</p>
            <p className="mt-1 text-xs tracking-widest uppercase">Content studio</p>
        </div>
        <nav aria-label="Main" className="lettered-container">
          {link('/', 'Dashboard')}
          <h3>Pages</h3>
          {PAGES.map(p => link(p.to, p.label))}
          <h3>Layout</h3>
          {LAYOUT.map(p => link(p.to, p.label))}
          <h3>Site</h3>
          {link('/media', 'Media library')}
          {link('/enquiries', 'Enquiries', unread)}
          {link('/seo', 'Meta data overview')}
          {me.role === 'Admin' && <>
            <h3>Settings</h3>
            {link('/settings/general', 'General settings')}
            {link('/users', 'Users')}
          </>}
        </nav>
        <div className="sidebar-foot">
          {meta.siteUrl && <a href={meta.siteUrl} target="_blank" rel="noreferrer">Open website ↗</a>}
          <NavLink to="/account" className="who"><span>{me.name}</span><small>{me.role}</small></NavLink>
          <button className="link-btn" onClick={signOut}>Sign out</button>
        </div>
      </aside>
      <main className="main"><Outlet /></main>
      {navOpen && <div className="scrim" onClick={() => setNavOpen(false)} />}
    </div>
  );
}

const router = createBrowserRouter([{
  path: '/',
  element: <Layout />,
  children: [
    { index: true, element: <DashboardPage /> },
    { path: 'content/hero', element: <HeroPage /> },
    { path: 'content/about', element: <AboutPage /> },
    { path: 'content/leadership', element: <LeadershipPage /> },
    { path: 'content/history', element: <TextPage sectionKey="history" title="History" route="/history" description="The school’s story." /> },
    { path: 'content/socialwall', element: <GalleryPage sectionKey="socialwall" title="Social wall" route="/gallery" description="Photo collections shown in the gallery section, grouped by type." ctaAllowed /> },
    { path: 'content/activities', element: <GalleryPage sectionKey="activities" title="Activities & facilities" route="/activities" description="Clubs, sports and facilities, each with its own photo collection." ctaAllowed /> },
    { path: 'content/contact', element: <ContactPage /> },
    { path: 'content/tuition', element: <TuitionPage /> },
    { path: 'content/privacy', element: <TextPage sectionKey="privacy" title="Privacy & cookies" route="/privacy" description="The privacy and cookie policy." /> },
    { path: 'content/menu', element: <MenuPage /> },
    { path: 'content/footer', element: <FooterPage /> },
    { path: 'content/bottomstrip', element: <BottomStripPage /> },
    { path: 'media', element: <MediaPage /> },
    { path: 'enquiries', element: <EnquiriesPage /> },
    { path: 'seo', element: <SeoOverviewPage /> },
    { path: 'settings/general', element: <GeneralPage /> },
    { path: 'users', element: <UsersPage /> },
    { path: 'account', element: <AccountPage /> },
    { path: '*', element: <Navigate to="/" replace /> },
  ],
}], { basename: '/admin' });

// ================================================================= root

export function App() {
  const [me, setMe] = useState<Me | null | undefined>(undefined);
  const [meta, setMeta] = useState<Meta | null>(null);

  useEffect(() => {
    setUnauthorizedHandler(() => setMe(null));
    api.get<Me>('/api/auth/me').then(setMe).catch(() => setMe(null));
  }, []);

  useEffect(() => {
    if (me) api.get<Meta>('/api/admin/meta').then(setMeta).catch(() => {});
  }, [me?.id]);

  const stable = useMemo(() => router, []);

  if (me === undefined) return <div className="boot"><Spinner /></div>;
  if (me === null) return <Login onLogin={setMe} />;
  if (!meta) return <div className="boot"><Spinner /></div>;
  return <AppProvider me={me} setMe={setMe} meta={meta}><RouterProvider router={stable} /></AppProvider>;
}
