import { useApp, useField } from '../state';
import { ImageField } from './ImageField';
import { Panel, TextField, AreaField } from './ui';

/** Meta title + description (+ share image) with a preview of how the page may appear in Google. */
export function SeoFields({ path = 'seo' }: { path?: string }) {
  const { meta } = useApp();
  const title = useField<string>(`${path}.title`).value ?? '';
  const desc = useField<string>(`${path}.description`).value ?? '';
  const host = (meta.siteUrl || 'https://www.example.com').replace(/^https?:\/\//, '');

  return (
    <Panel title="Meta data for search engines and sharing" description="What Google shows in results, and what appears when this page is shared on WhatsApp or Facebook.">
      <TextField path={`${path}.title`} label="Meta title" max={70} hint="Aim for 50–60 characters. Put the most important words first." />
      <AreaField path={`${path}.description`} label="Meta description" max={200} rows={3} hint="Aim for 120–160 characters. A short, plain sentence that makes people want to click." />
      <div className="serp" aria-label="Search result preview">
        <span className="serp-url">{host}</span>
        <span className="serp-title">{title || 'Meta title appears here'}</span>
        <span className="serp-desc">{desc || 'The meta description appears here.'}</span>
      </div>
      <ImageField path={`${path}.image`} label="Share image" hint="Optional. If empty, the default share image from General settings is used." />
    </Panel>
  );
}
