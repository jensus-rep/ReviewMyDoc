/* Document editor 1.0.0. Native rich editing, safe Markdown serialization and
 * serialized conditional saves keep selections attached to the text actually saved. */

export interface TextNode { tag: string; text?: string; href?: string; children?: TextNode[] }

export function escapeMarkdown(text: string): string {
  return text.replace(/\\/g, '\\\\').replace(/([*_[\]`#>|])/g, '\\$1').replace(/\u00a0/g, ' ');
}

/** Converts the supported semantic tree; formatting and quotations share this path. */
export function toMarkdown(node: TextNode): string {
  const children = node.children ?? [];
  const body = children.map(toMarkdown).join('');
  switch (node.tag) {
    case '#text': return escapeMarkdown(node.text ?? '');
    case 'script': case 'style': case 'img': case 'iframe': return '';
    case 'br': return '\n';
    case 'strong': case 'b': return body.trim() ? `**${body}**` : body;
    case 'em': case 'i': return body.trim() ? `*${body}*` : body;
    case 'code': {
      const raw = (node.text ?? body).replace(/\n/g, ' ');
      const fence = '`'.repeat(Math.max(0, ...(raw.match(/`+/g) ?? []).map(s => s.length)) + 1);
      return `${fence} ${raw} ${fence}`;
    }
    case 'pre': {
      const raw = node.text ?? body;
      const fence = '`'.repeat(Math.max(2, ...(raw.match(/`+/g) ?? []).map(s => s.length)) + 1);
      return `\n\n${fence}\n${raw}\n${fence}\n\n`;
    }
    case 'h1': case 'h2': case 'h3': case 'h4': case 'h5': case 'h6':
      return `\n\n${'#'.repeat(Number(node.tag[1]))} ${body.trim()}\n\n`;
    case 'p': case 'div': return `\n\n${body.trim()}\n\n`;
    case 'blockquote': return '\n\n' + body.trim().split('\n').map(line => '> ' + line).join('\n') + '\n\n';
    case 'ul': case 'ol': return '\n\n' + children.filter(n => n.tag === 'li').map((n, index) => {
      const content = toMarkdown(n).trim().replace(/\n/g, '\n    ');
      return `${node.tag === 'ol' ? `${index + 1}.` : '-'} ${content}`;
    }).join('\n') + '\n\n';
    case 'a': return node.href && /^(https?:|mailto:)/i.test(node.href)
      ? `[${body}](${node.href.replace(/[()\s<>]/g, c => encodeURIComponent(c))})` : body;
    case 'table': {
      const rows: TextNode[] = [];
      const visit = (n: TextNode): void => { if (n.tag === 'tr') rows.push(n); else n.children?.forEach(visit); };
      children.forEach(visit);
      const lines = rows.map(row => '| ' + (row.children ?? []).filter(c => c.tag === 'td' || c.tag === 'th').map(c => toMarkdown(c).trim().replace(/\n+/g, ' ')).join(' | ') + ' |');
      if (lines.length) lines.splice(1, 0, '| ' + (rows[0]?.children ?? []).filter(c => c.tag === 'td' || c.tag === 'th').map(() => '---').join(' | ') + ' |');
      return '\n\n' + lines.join('\n') + '\n\n';
    }
    default: return body;
  }
}

function tree(node: Node): TextNode {
  if (node.nodeType === Node.TEXT_NODE) return { tag: '#text', text: node.textContent ?? '' };
  const tag = node instanceof Element ? node.tagName.toLowerCase() : 'root';
  return { tag, text: node.textContent ?? '', href: node instanceof HTMLAnchorElement ? node.getAttribute('href') ?? '' : '', children: [...node.childNodes].map(tree) };
}

function markdown(node: Node): string {
  return toMarkdown(tree(node)).replace(/\n{3,}/g, '\n\n').trim();
}

function safePaste(html: string): string {
  const source = new DOMParser().parseFromString(html, 'text/html');
  const allowed = new Set(['P', 'DIV', 'BR', 'H1', 'H2', 'H3', 'H4', 'H5', 'H6', 'STRONG', 'B', 'EM', 'I', 'UL', 'OL', 'LI', 'BLOCKQUOTE', 'PRE', 'CODE', 'A', 'TABLE', 'THEAD', 'TBODY', 'TR', 'TD', 'TH']);
  for (const el of [...source.body.querySelectorAll('*')]) {
    if (['SCRIPT', 'STYLE', 'IFRAME', 'OBJECT', 'IMG', 'SVG', 'MATH'].includes(el.tagName)) { el.remove(); continue; }
    if (!allowed.has(el.tagName)) { el.replaceWith(...el.childNodes); continue; }
    const href = el.getAttribute('href');
    for (const attribute of [...el.attributes]) el.removeAttribute(attribute.name);
    if (el.tagName === 'A' && href && /^(https?:|mailto:)/i.test(href)) el.setAttribute('href', href);
  }
  return source.body.innerHTML;
}

interface Editor { form: HTMLFormElement; surface: HTMLElement; saved: string; etag: string; id: string }
interface Collection { id: string; etag: string; passages: { id: string; heading: string; preview: string }[] }

function init(root: HTMLElement): void {
  const status = root.querySelector<HTMLElement>('[data-save-status]')!;
  const toolbar = root.querySelector<HTMLElement>('[data-editor-tools]')!;
  const saveButton = root.querySelector<HTMLButtonElement>('[data-save-all]')!;
  const editors: Editor[] = [...root.querySelectorAll<HTMLFormElement>('[data-editor-section]')].map(form => {
    const surface = form.querySelector<HTMLElement>('[data-editor-surface]')!;
    const result = { form, surface, saved: markdown(surface), etag: form.dataset.textEtag ?? '', id: form.dataset.sectionId ?? '' };
    surface.contentEditable = 'true';
    surface.hidden = false;
    surface.spellcheck = true;
    form.querySelector<HTMLElement>('[data-editor-fallback]')!.hidden = true;
    return result;
  });
  saveButton.hidden = false;
  let queue: Promise<void> = Promise.resolve();
  let timer: ReturnType<typeof setTimeout> | undefined;
  let selected: Range | null = null;
  let collecting = false;
  const positionTools = (): void => {
    if (!selected || toolbar.hidden) return;
    const rect = selected.getBoundingClientRect();
    const gap = parseFloat(getComputedStyle(root).getPropertyValue('--space-3')) || 12;
    const width = toolbar.getBoundingClientRect().width;
    const height = toolbar.getBoundingClientRect().height;
    toolbar.style.setProperty('--document-editor-menu-x', `${Math.max(gap, Math.min(rect.left, innerWidth - width - gap))}px`);
    toolbar.style.setProperty('--document-editor-menu-y', `${Math.max(gap, Math.min(rect.top > height + gap ? rect.top - height - gap : rect.bottom + gap, innerHeight - height - gap))}px`);
  };
  const dirty = (): boolean => editors.some(e => markdown(e.surface) !== e.saved);
  const report = (message: string): void => { status.textContent = message; };
  const token = root.querySelector<HTMLInputElement>('[name="__RequestVerificationToken"]')!.value;

  async function post<T>(handler: string, fields: Record<string, string>): Promise<T> {
    const body = new URLSearchParams({ ...fields, __RequestVerificationToken: token });
    const response = await fetch(`${location.pathname}?handler=${handler}`, { method: 'POST', body, headers: { 'X-Requested-With': 'fetch' } });
    if (!response.headers.get('content-type')?.includes('application/json')) throw new Error('Speichern nicht möglich. Prüfe deine Anmeldung; dein Text bleibt hier erhalten.');
    const result = await response.json() as T & { error?: string };
    if (!response.ok) throw new Error(result.error ?? 'Speichern fehlgeschlagen. Bitte erneut versuchen.');
    return result;
  }

  function saveAll(): Promise<void> {
    clearTimeout(timer);
    const operation = queue.catch(() => {}).then(async () => {
      for (const editor of editors) {
        const value = markdown(editor.surface);
        if (value === editor.saved) continue;
        report('Wird gespeichert …');
        const result = await post<{ sectionId: string; etag: string }>('Save', {
          sectionId: editor.id, markdown: value, etag: editor.etag, documentETag: root.dataset.documentEtag ?? '',
        });
        editor.saved = value;
        editor.id = result.sectionId;
        editor.etag = result.etag;
      }
      report(dirty() ? 'Noch ungespeicherte Änderungen' : 'Alle Änderungen gespeichert');
    });
    queue = operation;
    return operation;
  }
  const failure = (error: unknown): void => report(error instanceof Error ? error.message : 'Verbindung unterbrochen. Bitte erneut speichern.');
  const changed = (): void => {
    report('Ungespeicherte Änderungen');
    clearTimeout(timer);
    timer = setTimeout(() => { void saveAll().catch(failure); }, 1000);
  };
  saveButton.addEventListener('click', () => { void saveAll().catch(failure); });
  window.addEventListener('beforeunload', event => { if (dirty()) { event.preventDefault(); event.returnValue = ''; } });

  for (const editor of editors) {
    editor.surface.addEventListener('input', changed);
    editor.surface.addEventListener('paste', event => {
      event.preventDefault();
      const html = event.clipboardData?.getData('text/html');
      if (html) document.execCommand('insertHTML', false, safePaste(html));
      else document.execCommand('insertText', false, event.clipboardData?.getData('text/plain') ?? '');
      changed();
    });
    editor.surface.addEventListener('drop', event => { event.preventDefault(); });
    editor.surface.addEventListener('keydown', event => {
      if ((event.ctrlKey || event.metaKey) && event.key.toLowerCase() === 's') { event.preventDefault(); void saveAll().catch(failure); }
      if (event.key === 'Escape') { toolbar.hidden = true; }
    });
  }
  document.addEventListener('selectionchange', () => {
    const selection = window.getSelection();
    if (!selection?.rangeCount || selection.isCollapsed) { if (!toolbar.contains(document.activeElement)) toolbar.hidden = true; return; }
    const range = selection.getRangeAt(0);
    if (!editors.some(e => e.surface.contains(range.startContainer)) || !editors.some(e => e.surface.contains(range.endContainer))) return;
    selected = range.cloneRange();
    toolbar.hidden = false;
    positionTools();
  });
  window.addEventListener('scroll', positionTools, { passive: true });
  window.addEventListener('resize', positionTools);
  toolbar.addEventListener('mousedown', event => event.preventDefault());
  toolbar.querySelectorAll<HTMLButtonElement>('[data-format]').forEach(button => button.addEventListener('click', () => {
    if (!selected) return;
    const selection = window.getSelection()!;
    selection.removeAllRanges(); selection.addRange(selected);
    const command = button.dataset.format!;
    document.execCommand(command === 'h2' ? 'formatBlock' : command, false, command === 'h2' ? 'h2' : undefined);
    changed();
  }));

  function showCollection(result: Collection): void {
    root.dataset.draftId = result.id;
    root.dataset.draftEtag = result.etag;
    root.querySelector('[data-collection-count]')!.textContent = String(result.passages.length);
    root.querySelector('[data-collection-label]')!.textContent = result.passages.length === 1 ? 'Passage gesammelt' : 'Passagen gesammelt';
    const list = root.querySelector('[data-collection-list]')!;
    list.replaceChildren(...result.passages.map(p => { const li = document.createElement('li'); li.textContent = p.preview; return li; }));
    const link = root.querySelector<HTMLAnchorElement>('[data-collection-link]')!;
    link.href = `${location.pathname}/reviews/${result.id}`;
    link.hidden = false;
  }

  async function collect(assign: boolean): Promise<void> {
    if (!selected || collecting) return;
    const parts: { editor: Editor; text: string; source: string }[] = [];
    for (const editor of editors) {
      if (!selected.intersectsNode(editor.surface)) continue;
      const part = document.createRange();
      part.selectNodeContents(editor.surface);
      if (editor.surface.contains(selected.startContainer)) part.setStart(selected.startContainer, selected.startOffset);
      if (editor.surface.contains(selected.endContainer)) part.setEnd(selected.endContainer, selected.endOffset);
      const text = markdown(part.cloneContents());
      if (text) parts.push({ editor, text, source: markdown(editor.surface) });
    }
    if (!parts.length) return;
    collecting = true;
    toolbar.querySelectorAll<HTMLButtonElement>('button').forEach(b => b.disabled = true);
    try {
      await saveAll();
      for (const part of parts) {
        if (part.editor.saved !== part.source || markdown(part.editor.surface) !== part.source) {
          throw new Error('Der Text wurde während des Sammelns geändert. Bitte die Passage erneut markieren.');
        }
        const result = await post<Collection>('Collect', { sectionId: part.editor.id, etag: part.editor.etag, markdown: part.text,
          draftId: root.dataset.draftId ?? '', draftETag: root.dataset.draftEtag ?? '' });
        showCollection(result);
      }
      report('Passage gespeichert und für das Review gesammelt');
      if (assign) location.assign(`${location.pathname}/reviews/${root.dataset.draftId}`);
    } catch (error) { failure(error); }
    finally { collecting = false; toolbar.querySelectorAll<HTMLButtonElement>('button').forEach(b => b.disabled = false); }
  }
  toolbar.querySelector('[data-collect]')!.addEventListener('click', () => { void collect(false); });
  toolbar.querySelector('[data-assign]')!.addEventListener('click', () => { void collect(true); });
}

if (typeof document !== 'undefined') document.querySelectorAll<HTMLElement>('[data-document-editor]').forEach(init);

// A URL fragment is not transmitted in HTTP requests or referrers. Redeem it
// through the antiforgery-protected form, then remove it from browser history.
if (typeof document !== 'undefined') {
  const access = document.querySelector<HTMLFormElement>('[data-review-access]');
  const token = new URLSearchParams(location.hash.slice(1)).get('token');
  if (access && token && /^[a-f0-9]{64}$/.test(token)) {
    access.querySelector<HTMLInputElement>('[name="token"]')!.value = token;
    history.replaceState(null, '', location.pathname);
    access.requestSubmit();
  }
  const link = document.querySelector<HTMLInputElement>('[data-share-link]');
  if (link) {
    link.value = new URL(link.value, location.origin).href;
    document.querySelector('[data-copy-link]')?.addEventListener('click', () => {
      const status = document.querySelector('[data-copy-status]')!;
      if (!navigator.clipboard) { link.select(); status.textContent = 'Link markiert. Mit Strg+C kopieren.'; return; }
      void navigator.clipboard.writeText(link.value).then(() => { status.textContent = 'Link kopiert'; })
        .catch(() => { link.select(); status.textContent = 'Link markiert. Mit Strg+C kopieren.'; });
    });
  }
}
