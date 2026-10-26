import { useEffect, useId, useRef, useState } from 'react';
import { EditorContent, useEditor } from '@tiptap/react';
import StarterKit from '@tiptap/starter-kit';
import { Field } from './ui';
import { useField } from '../state';

/**
 * Rich text bound to a JSON path. Output is plain HTML limited to what the server will keep
 * (paragraphs, bold, italic, links, lists, and optionally headings and quotes).
 */
export function RichField({ path, label, hint, blocks = 'basic', minHeight = 140 }: {
  path: string; label: string; hint?: React.ReactNode; blocks?: 'basic' | 'full'; minHeight?: number;
}) {
  const id = useId();
  const f = useField<string>(path);
  const value = f.value ?? '';
  const last = useRef(value);
  const [linkOpen, setLinkOpen] = useState(false);
  const [linkUrl, setLinkUrl] = useState('');

  const editor = useEditor({
    extensions: [
      StarterKit.configure({
        heading: blocks === 'full' ? { levels: [2, 3, 4] } : false,
        blockquote: blocks === 'full' ? {} : false,
        code: false, codeBlock: false, strike: false, horizontalRule: false, underline: false,
        link: { openOnClick: false, autolink: false, HTMLAttributes: { rel: 'noopener noreferrer' } },
      }),
    ],
    content: value,
    shouldRerenderOnTransaction: true,
    editorProps: { attributes: { id, class: 'rt-content', role: 'textbox', 'aria-multiline': 'true', 'aria-label': label } },
    onUpdate: ({ editor }) => {
      const html = editor.isEmpty ? '' : editor.getHTML();
      last.current = html;
      f.set(html);
    },
  });

  // keep the editor in step when the value changes from outside (discard, reload, after save)
  useEffect(() => {
    if (!editor || value === last.current) return;
    last.current = value;
    editor.commands.setContent(value || '', { emitUpdate: false });
  }, [value, editor]);

  if (!editor) return null;

  const btn = (active: boolean, onClick: () => void, text: React.ReactNode, title: string) => (
    <button type="button" className={`rt-btn${active ? ' on' : ''}`} onMouseDown={e => e.preventDefault()} onClick={onClick} title={title} aria-label={title} aria-pressed={active}>{text}</button>
  );

  const openLink = () => {
    setLinkUrl(editor.getAttributes('link').href ?? '');
    setLinkOpen(true);
  };
  const applyLink = () => {
    const url = linkUrl.trim();
    if (!url) editor.chain().focus().extendMarkRange('link').unsetLink().run();
    else editor.chain().focus().extendMarkRange('link').setLink({ href: url }).run();
    setLinkOpen(false);
  };

  return (
    <Field label={label} hint={hint} error={f.error} htmlFor={id}>
      <div className={`rt${f.error ? ' has-error' : ''}`}>
        <div className="rt-toolbar" role="toolbar" aria-label="Text formatting">
          {btn(editor.isActive('bold'), () => editor.chain().focus().toggleBold().run(), <b>B</b>, 'Bold')}
          {btn(editor.isActive('italic'), () => editor.chain().focus().toggleItalic().run(), <i>I</i>, 'Italic')}
          {btn(editor.isActive('link'), openLink, 'Link', 'Add or edit a link')}
          <span className="rt-sep" />
          {btn(editor.isActive('bulletList'), () => editor.chain().focus().toggleBulletList().run(), '• List', 'Bulleted list')}
          {btn(editor.isActive('orderedList'), () => editor.chain().focus().toggleOrderedList().run(), '1. List', 'Numbered list')}
          {blocks === 'full' && <>
            <span className="rt-sep" />
            {btn(editor.isActive('heading', { level: 3 }), () => editor.chain().focus().toggleHeading({ level: 3 }).run(), 'H3', 'Large sub-heading')}
            {btn(editor.isActive('heading', { level: 4 }), () => editor.chain().focus().toggleHeading({ level: 4 }).run(), 'H4', 'Sub-heading')}
            {btn(editor.isActive('blockquote'), () => editor.chain().focus().toggleBlockquote().run(), '❝', 'Quote')}
          </>}
        </div>
        {linkOpen && (
          <div className="rt-link">
            <input type="text" autoFocus value={linkUrl} placeholder="https://example.com or /about" aria-label="Link address"
              onChange={e => setLinkUrl(e.target.value)}
              onKeyDown={e => { if (e.key === 'Enter') { e.preventDefault(); applyLink(); } if (e.key === 'Escape') setLinkOpen(false); }} />
            <button type="button" className="btn small primary" onClick={applyLink}>Apply</button>
            <button type="button" className="btn small" onClick={() => setLinkOpen(false)}>Cancel</button>
          </div>
        )}
        <div style={{ minHeight }} className="rt-body"><EditorContent editor={editor} /></div>
      </div>
    </Field>
  );
}
