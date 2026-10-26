import { useState } from 'react';
import { Link } from 'react-router-dom';
import { EditorPage } from '../components/EditorPage';
import { ImageField, MediaPicker } from '../components/ImageField';
import { RichField } from '../components/RichText';
import { SeoFields } from '../components/SeoFields';
import {
  AreaField, Field, LinkTargetField, Notice, NumberField, Panel, Repeater, Row, SelectField, TextField, ToggleField,
} from '../components/ui';
import { useApp, useField, useForm } from '../state';
import { getPath, resolveMedia } from '../util';

const Thumb = ({ src }: { src?: string }) => {
  const { meta } = useApp();
  return src ? <img className="rep-thumb" src={resolveMedia(src, meta.siteUrl)} alt="" loading="lazy" /> : <span className="rep-thumb empty" />;
};

// ================================================================= 1. Homepage hero

export function HeroPage() {
  return (
    <EditorPage sectionKey="hero" title="Homepage hero" route="/" description="The slideshow at the top of the homepage and the text laid over it.">
      <Panel title="Slideshow images" description="Shown in this order. Wide, landscape photos work best (at least 1600 pixels across).">
        <Repeater path="slides" addLabel="Add slide" max={12} empty="No slides yet. Add at least one image."
          title={(s, i) => s.alt || `Slide ${i + 1}`} summary={s => <Thumb src={s.image} />}
          newItem={() => ({ image: '', alt: '' })}>
          {base => <ImageField path={`${base}.image`} altPath={`${base}.alt`} label="Image" optional={false} />}
        </Repeater>
      </Panel>
      <Panel title="Text over the images">
        <Row>
          <TextField path="headingLead" label="Heading — first part" max={100} hint="Shown in white, e.g. “Education for a”." />
          <TextField path="headingHighlight" label="Heading — highlighted part" max={100} hint="Shown in gold, e.g. “brighter future”." />
        </Row>
        <RichField path="text" label="Paragraph" />
        <Row>
          <ToggleField path="showSocialIcons" label="Show social icons under the text" hint="Uses the links in General settings." />
          <NumberField path="autoplaySeconds" label="Seconds per slide" min={2} max={30} suffix="seconds" />
        </Row>
      </Panel>
      <SeoFields />
    </EditorPage>
  );
}

// ================================================================= 2. About

export function AboutPage() {
  return (
    <EditorPage sectionKey="about" title="About us" route="/about" description="The introduction beside the image card, and the key facts underneath.">
      <Panel title="Image card" description="The tall card on the left. Without an image, the card shows the caption on a green background.">
        <ImageField path="imageCard.image" altPath="imageCard.alt" label="Image" shape="portrait" />
        <AreaField path="imageCard.caption" label="Caption on the green card" rows={3} max={80} hint="One word per line looks best. Shown only when there is no image." optional />
      </Panel>
      <Panel title="Introduction">
        <TextField path="eyebrow" label="Section eyebrow" max={80} hint="The small label above the heading." />
        <TextField path="heading" label="Heading" max={160} />
        <RichField path="text" label="Text" />
      </Panel>
      <Panel title="Key facts" description="The short figures shown under the text (year established, number of pupils…).">
        <Repeater path="stats" addLabel="Add fact" max={6} startOpen
          title={(s, i) => `${s.value || '—'} ${s.label || ''}`.trim() || `Fact ${i + 1}`}
          newItem={() => ({ value: '', label: '' })}>
          {base => <Row><TextField path={`${base}.value`} label="Figure" max={24} placeholder="200+" /><TextField path={`${base}.label`} label="Label" max={60} placeholder="Registered pupils" /></Row>}
        </Repeater>
      </Panel>
      <SeoFields />
    </EditorPage>
  );
}

// ================================================================= 3. Leadership

export function LeadershipPage() {
  return (
    <EditorPage sectionKey="leadership" title="Leadership" route="/leadership" description="The people shown on the leadership cards. Clicking a card opens their biography.">
      <Panel title="Section heading">
        <TextField path="eyebrow" label="Section eyebrow" max={80} />
        <TextField path="heading" label="Heading" max={160} />
      </Panel>
      <Panel title="People" description="Cards appear in this order.">
        <Repeater path="people" addLabel="Add person" max={40} title={p => p.name} summary={p => <><Thumb src={p.image} /><span className="rep-role">{p.designation}</span></>}
          newItem={() => ({ id: '', image: '', name: '', designation: '', bio: { title: '', text: '' } })}>
          {base => (
            <>
              <ImageField path={`${base}.image`} label="Photo" shape="portrait" hint="A portrait works best; the card crops it to a tall rectangle." />
              <Row>
                <TextField path={`${base}.name`} label="Name" max={100} />
                <TextField path={`${base}.designation`} label="Designation" max={100} placeholder="Head Teacher" />
              </Row>
              <TextField path={`${base}.bio.title`} label="Biography title" max={120} hint="Heading of the panel that slides open. Usually the person’s name." />
              <RichField path={`${base}.bio.text`} label="Biography" />
            </>
          )}
        </Repeater>
      </Panel>
      <SeoFields />
    </EditorPage>
  );
}

// ================================================================= 4 + 9. History, Privacy

export function TextPage({ sectionKey, title, description, route }: { sectionKey: string; title: string; description: string; route: string }) {
  return (
    <EditorPage sectionKey={sectionKey} title={title} route={route} description={description}>
      <Panel title="Content">
        <TextField path="eyebrow" label="Section eyebrow" max={80} />
        <TextField path="heading" label="Heading" max={160} />
        <RichField path="text" label="Text" blocks="full" minHeight={320} hint="Use H3/H4 for sub-headings, and the list buttons for bullet points." />
      </Panel>
      <SeoFields />
    </EditorPage>
  );
}

// ================================================================= 5 + 6. Galleries

function TypeField({ path, listId }: { path: string; listId: string }) {
  const f = useField<string>(path);
  return (
    <Field label="Type" error={f.error} hint="Used by the filter buttons. Cards with the same type are grouped together.">
      <input type="text" list={listId} value={f.value ?? ''} maxLength={40} placeholder="e.g. Events" onChange={e => f.set(e.target.value)} />
    </Field>
  );
}

function GalleryImages({ path }: { path: string }) {
  const form = useForm();
  const { meta } = useApp();
  const [picking, setPicking] = useState(false);
  const images: { src: string; alt: string }[] = getPath(form.data, path) ?? [];
  const err = form.errors[path]?.[0];

  const move = (i: number, d: number) => form.update(draft => {
    const a = getPath(draft, path) as any[];
    const j = i + d;
    if (j >= 0 && j < a.length) [a[i], a[j]] = [a[j], a[i]];
  });

  return (
    <Field label={`Gallery images (${images.length})`} error={err} hint="Shown in this order when the card is opened. Add a short description to each for accessibility.">
      {images.length > 0 && (
        <ul className="gal-list">
          {images.map((img, i) => (
            <li key={i} className="gal-item">
              <img src={resolveMedia(img.src, meta.siteUrl)} alt="" loading="lazy" />
              <input type="text" value={img.alt} placeholder="Description (alt text)" aria-label={`Description of image ${i + 1}`}
                onChange={e => form.set(`${path}[${i}].alt`, e.target.value)} />
              <div className="rep-actions">
                <button type="button" className="icon-btn" disabled={i === 0} onClick={() => move(i, -1)} aria-label="Move up">↑</button>
                <button type="button" className="icon-btn" disabled={i === images.length - 1} onClick={() => move(i, 1)} aria-label="Move down">↓</button>
                <button type="button" className="icon-btn danger" onClick={() => form.update(d => { (getPath(d, path) as any[]).splice(i, 1); })} aria-label="Remove image">✕</button>
              </div>
            </li>
          ))}
        </ul>
      )}
      <button type="button" className="btn" onClick={() => setPicking(true)}>+ Add images</button>
      {picking && <MediaPicker multiple onClose={() => setPicking(false)} onPick={picked => {
        form.update(d => { (getPath(d, path) as any[]).push(...picked.map(p => ({ src: p.src, alt: p.alt }))); });
        setPicking(false);
      }} />}
    </Field>
  );
}

export function GalleryPage({ sectionKey, title, description, route, ctaAllowed }: {
  sectionKey: string; title: string; description: string; route: string; ctaAllowed: boolean;
}) {
  const { meta } = useApp();
  return (
    <EditorPage sectionKey={sectionKey} title={title} route={route} description={description}>
      {(form) => {
        const types = Array.from(new Set<string>((form.data.cards ?? []).map((c: any) => c.type).filter(Boolean)));
        const listId = `types-${sectionKey}`;
        return (
          <>
            <Panel title="Section heading">
              <TextField path="eyebrow" label="Section eyebrow" max={80} />
              <TextField path="heading" label="Section heading" max={160} />
              <RichField path="intro" label="Intro text" hint="Optional short paragraph under the heading." />
              <TextField path="allLabel" label="Label of the “show everything” filter" max={30} placeholder="All" />
            </Panel>

            <Panel title="Cards" description="Each card opens a photo gallery. Cards are grouped by their type, and the filter buttons are built from the types you use.">
              <datalist id={listId}>{types.map(t => <option key={t} value={t} />)}</datalist>
              <Repeater path="cards" addLabel="Add card" max={80} title={c => c.title}
                summary={c => <><Thumb src={c.images?.[0]?.src} /><span className="rep-role">{c.type} · {c.images?.length ?? 0} photos</span></>}
                newItem={() => ({ id: '', title: '', type: types[0] ?? '', icon: 'home', images: [] })}>
                {base => (
                  <>
                    <Row>
                      <TextField path={`${base}.title`} label="Card title" max={80} />
                      <TypeField path={`${base}.type`} listId={listId} />
                    </Row>
                    <SelectField path={`${base}.icon`} label="Icon on the card" options={meta.icons.map(i => ({ value: i, label: i[0].toUpperCase() + i.slice(1) }))} />
                    <GalleryImages path={`${base}.images`} />
                  </>
                )}
              </Repeater>
            </Panel>

            {ctaAllowed && (
              <Panel title="Button under the cards" description="Optional. Leave the label empty to hide the button.">
                <Row>
                  <TextField path="cta.label" label="Button label" max={80} optional placeholder="Visit our YouTube channel" />
                  <LinkTargetField path="cta.url" label="Button link" />
                </Row>
                <ToggleField path="cta.newTab" label="Open in a new tab" />
              </Panel>
            )}
            <SeoFields />
          </>
        );
      }}
    </EditorPage>
  );
}

// ================================================================= 7. Get in touch

function StringList({ path, label, addLabel, placeholder, max }: { path: string; label: string; addLabel: string; placeholder?: string; max: number }) {
  const form = useForm();
  const items: string[] = getPath(form.data, path) ?? [];
  return (
    <Field label={label} error={form.errors[path]?.[0]}>
      {items.map((v, i) => (
        <div key={i} className="string-row">
          <input type="text" value={v} placeholder={placeholder} aria-label={`${label} ${i + 1}`} onChange={e => form.set(`${path}[${i}]`, e.target.value)} />
          <button type="button" className="icon-btn danger" aria-label="Remove" onClick={() => form.update(d => { (getPath(d, path) as string[]).splice(i, 1); })}>✕</button>
        </div>
      ))}
      <button type="button" className="btn" disabled={items.length >= max} onClick={() => form.update(d => { (getPath(d, path) as string[]).push(''); })}>+ {addLabel}</button>
    </Field>
  );
}

function MapPreview() {
  const { value } = useField<string>('mapEmbedUrl');
  let src = value ?? '';
  const m = src.match(/<iframe[^>]*\ssrc\s*=\s*["']([^"']+)["']/i);
  if (m) src = m[1].replace(/&amp;/g, '&');
  if (!/^https:\/\/(www\.)?google\.com\/maps\//i.test(src)) return null;
  return <iframe className="map-preview" title="Map preview" src={src} loading="lazy" referrerPolicy="no-referrer-when-downgrade" />;
}

export function ContactPage() {
  return (
    <EditorPage sectionKey="contact" title="Get in touch" route="/contact" description="The map, the contact details and their social links.">
      <Panel title="Section heading">
        <TextField path="eyebrow" label="Section eyebrow" max={80} />
        <TextField path="heading" label="Heading" max={160} />
      </Panel>
      <Panel title="Map">
        <AreaField path="mapEmbedUrl" label="Map embed link" rows={3}
          hint={<>In Google Maps choose <b>Share → Embed a map</b>, copy what it gives you and paste it here. Either the whole <code>&lt;iframe…&gt;</code> snippet or just the link works.</>} />
        <MapPreview />
      </Panel>
      <Panel title="Contact details">
        <TextField path="details.title" label="Title" max={120} placeholder="Biggi Junior School" />
        <AreaField path="details.address" label="Address" rows={3} max={400} hint="Press Enter for a new line." />
        <StringList path="details.phones" label="Telephone numbers" addLabel="Add number" placeholder="+256 700 000 000" max={4} />
        <TextField path="details.email" label="Email" type="email" max={200} />
        <Row>
          <TextField path="details.youTube.label" label="YouTube — text shown" max={120} placeholder="@BiggiJuniorSchool" />
          <TextField path="details.youTube.url" label="YouTube — link" placeholder="https://www.youtube.com/…" />
        </Row>
        <Row>
          <TextField path="details.facebook.label" label="Facebook — text shown" max={120} placeholder="@BiggiJuniorSchool" />
          <TextField path="details.facebook.url" label="Facebook — link" placeholder="https://www.facebook.com/…" />
        </Row>
        <Row>
          <TextField path="details.whatsApp.label" label="WhatsApp — text shown" max={120} placeholder="Chat with us" />
          <TextField path="details.whatsApp.url" label="WhatsApp — link" placeholder="https://wa.me/256700000000" hint="A wa.me link is the simplest: https://wa.me/ followed by the number with no + or spaces." />
        </Row>
      </Panel>
      <SeoFields />
    </EditorPage>
  );
}

// ================================================================= 8. Tuition & fees

function TuitionButtonFields() {
  const action = useField<string>('button.action').value;
  return (
    <>
      <Row>
        <TextField path="button.label" label="Button label" max={80} optional placeholder="Support a bursary" hint="Leave empty to hide the button." />
        <SelectField path="button.action" label="When clicked" options={[
          { value: 'panel', label: 'Slide open the details panel (below)' },
          { value: 'link', label: 'Go to a link' },
        ]} />
      </Row>
      {action === 'link' && (
        <>
          <LinkTargetField path="button.url" label="Button link" />
          <ToggleField path="button.newTab" label="Open in a new tab" />
        </>
      )}
    </>
  );
}

export function TuitionPage() {
  return (
    <EditorPage sectionKey="tuition" title="Tuition & fees" route="/tuition" description="Fees information and the support button.">
      <Panel title="Content">
        <TextField path="eyebrow" label="Section eyebrow" max={80} />
        <TextField path="heading" label="Heading" max={160} />
        <RichField path="text" label="Text" minHeight={220} />
      </Panel>
      <Panel title="Button">
        <TuitionButtonFields />
      </Panel>
      <TuitionPanel />
      <SeoFields />
    </EditorPage>
  );
}

function TuitionPanel() {
  const action = useField<string>('button.action').value;
  return (
    <Panel title="Details panel" description={action === 'panel' ? 'Opens when visitors click the button, for example bank payment details.' : 'Not used while the button goes to a link. Switch the button to “Slide open the details panel” to show it.'}>
      <TextField path="panel.title" label="Panel title" max={120} />
      <RichField path="panel.text" label="Panel text" hint="Put each detail on its own line, e.g. “Bank: …”, “Account name: …”." />
    </Panel>
  );
}

// ================================================================= 12. Main menu

export function MenuPage() {
  return (
    <EditorPage sectionKey="menu" title="Main menu" description="The links in the slide-out navigation, in this order. Link to a page on the site or to any other address.">
      <Panel title="Menu links">
        <Repeater path="items" addLabel="Add link" max={20} startOpen empty="The menu is empty."
          title={m => m.label} summary={m => <span className="badge">{m.url.startsWith('http') ? 'External' : m.url.startsWith('mailto:') || m.url.startsWith('tel:') ? 'Contact' : 'Internal'}</span>}
          newItem={() => ({ label: '', url: '', newTab: false })}>
          {base => (
            <>
              <TextField path={`${base}.label`} label="Label" max={60} />
              <LinkTargetField path={`${base}.url`} />
              <ToggleField path={`${base}.newTab`} label="Open in a new tab" hint="Recommended for links that leave the website." />
            </>
          )}
        </Repeater>
      </Panel>
    </EditorPage>
  );
}

// ================================================================= 10. Footer

function LinksEditor({ path }: { path: string }) {
  return (
    <Repeater path={path} addLabel="Add link" max={20} startOpen empty="No links yet." title={l => l.label}
      newItem={() => ({ label: '', url: '', newTab: false })}>
      {base => (
        <>
          <TextField path={`${base}.label`} label="Label" max={80} />
          <LinkTargetField path={`${base}.url`} />
          <ToggleField path={`${base}.newTab`} label="Open in a new tab" />
        </>
      )}
    </Repeater>
  );
}

export function FooterPage() {
  return (
    <EditorPage sectionKey="footer" title="Footer" description="The logo, the line beneath it, and the two link lists.">
      <Panel title="Logo and tagline">
        <ImageField path="logo" label="Footer logo" hint="Optional. If empty, the site logo from General settings is used. A light logo on a transparent background works best." />
        <AreaField path="tagline" label="Text below the logo" rows={3} max={400} />
      </Panel>
      <Panel title="First link list">
        <TextField path="explore.title" label="List title" max={60} placeholder="Explore" />
        <LinksEditor path="explore.links" />
      </Panel>
      <Panel title="Second link list">
        <TextField path="connect.title" label="List title" max={60} placeholder="Connect" />
        <LinksEditor path="connect.links" />
      </Panel>
    </EditorPage>
  );
}

// ================================================================= 11. Bottom strip

export function BottomStripPage() {
  return (
    <EditorPage sectionKey="bottomstrip" title="Bottom strip" description="The thin line at the very bottom of every page.">
      {(form) => (
        <Panel title="Text">
          <Row>
            <TextField path="left" label="Left text" max={200} hint={<>Type <code>{'{year}'}</code> to insert the current year automatically.</>} />
            <TextField path="right" label="Right text" max={200} />
          </Row>
          <div className="strip-preview" aria-label="Preview">
            <span>{String(form.data.left ?? '').replace('{year}', String(new Date().getFullYear()))}</span>
            <span>{form.data.right}</span>
          </div>
        </Panel>
      )}
    </EditorPage>
  );
}

// ================================================================= General settings

export function GeneralPage() {
  const { meta } = useApp();
  return (
    <EditorPage sectionKey="general" title="General settings" description="Settings used across the whole site.">
      <Panel title="Identity">
        <Row>
          <TextField path="siteName" label="Site name" max={100} />
          <TextField path="tagline" label="Tagline" max={200} />
        </Row>
        <Row>
          <ImageField path="logo" label="Site logo (light version)" hint="Used on dark backgrounds: top bar, hero and footer. Transparent PNG or WebP." shape="square" />
          <ImageField path="logoDark" label="Logo for light backgrounds" shape="square" />
        </Row>
        <ImageField path="defaultShareImage" label="Default share image" hint="Shown when a page is shared on WhatsApp or Facebook and has no image of its own. 1200 × 630 pixels is ideal." />
      </Panel>

      <Panel title="Social links" description="Shown as icons in the homepage hero.">
        <Repeater path="social" addLabel="Add social link" max={10} startOpen title={s => s.platform ? s.platform[0].toUpperCase() + s.platform.slice(1) : 'New link'}
          newItem={() => ({ platform: 'facebook', url: '' })}>
          {base => (
            <Row>
              <SelectField path={`${base}.platform`} label="Platform" options={meta.platforms.map(p => ({ value: p, label: p[0].toUpperCase() + p.slice(1) }))} />
              <TextField path={`${base}.url`} label="Link" placeholder="https://…" />
            </Row>
          )}
        </Repeater>
        <TextField path="publicEmail" label="Public email address" type="email" max={200} optional hint="Used for the mail icon in the hero." />
      </Panel>

      <Panel title="Contact form" description="What happens when someone sends a message from the Get in touch section.">
        <TextField path="notificationEmail" label="Send messages to" type="email" max={200} hint="The inbox that receives contact form messages. Visitors never see this address." />
        <TextField path="notificationSubject" label="Email subject" max={120} />
        {meta.mail.configured
          ? <Notice>Email delivery is set up on the server ({meta.mail.provider}).</Notice>
          : <Notice kind="warn">Email delivery is not set up on the server yet, so messages are saved in <Link to="/enquiries">Enquiries</Link> but not emailed. Ask your developer to configure <code>Mail</code> in appsettings.json.</Notice>}
      </Panel>

      <Panel title="Cookie notice">
        <ToggleField path="cookieNoticeEnabled" label="Show the cookie notice to visitors" />
        <TextField path="cookieNoticeText" label="Notice text" max={400} />
        <TextField path="cookieLinkLabel" label="Link text" max={40} hint="Links to the Privacy & cookies section." />
      </Panel>

      <Panel title="Analytics">
        <TextField path="gtmId" label="Google Tag Manager ID" max={20} optional placeholder="GTM-XXXXXXX" hint="Leave empty to switch tracking off." />
      </Panel>
    </EditorPage>
  );
}
