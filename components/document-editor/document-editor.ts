/* Document editor 1.2.0. Native rich editing, safe Markdown serialization and
 * serialized conditional saves keep selections attached to the text actually saved. */

import './review-access.js';
import { markdown } from './markdown.js';
import { reviewCollection, type Collection } from './review-collection.js';

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

function init(root: HTMLElement): void {
  const collection = reviewCollection(root, post, targetId => { void collect(targetId); });
  const status = root.querySelector<HTMLElement>('[data-save-status]')!;
  const toolbar = root.querySelector<HTMLElement>('[data-editor-tools]')!;
  const saveButton = root.querySelector<HTMLButtonElement>('[data-save-all]')!;
  const editors: Editor[] = [...root.querySelectorAll<HTMLFormElement>('[data-editor-section]')].map(form => {
    const surface = form.querySelector<HTMLElement>('[data-editor-surface]')!;
    const result = { form, surface, saved: markdown(surface), etag: form.dataset.textEtag ?? '', id: form.dataset.sectionId ?? '' };
    surface.contentEditable = 'true';
    surface.setAttribute('role', 'textbox');
    surface.setAttribute('aria-multiline', 'true');
    surface.hidden = false;
    surface.spellcheck = true;
    form.querySelector<HTMLElement>('[data-editor-fallback]')!.hidden = true;
    return result;
  });
  saveButton.hidden = false;
  root.querySelector<HTMLButtonElement>('[data-open-reviews]')!.hidden = false;
  let queue: Promise<void> = Promise.resolve();
  let timer: ReturnType<typeof setTimeout> | undefined;
  let selected: Range | null = null;
  let collecting = false;
  let draggingSelection = false;
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
  root.querySelector('[data-open-reviews]')?.addEventListener('click', () => {
    collection.open();
    const heading = root.querySelector<HTMLElement>('[data-review-panel] h2')!;
    heading.tabIndex = -1;
    heading.scrollIntoView({ block: 'nearest' });
    heading.focus({ preventScroll: true });
  });
  root.querySelector<HTMLAnchorElement>('[data-collection-link]')!.addEventListener('click', event => {
    event.preventDefault();
    if (collecting) return;
    const href = (event.currentTarget as HTMLAnchorElement).href;
    void saveAll().then(() => { if (!dirty()) location.assign(href); }).catch(failure);
  });
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
    editor.surface.addEventListener('dragstart', event => {
      const selection = window.getSelection();
      if (collecting || !selection?.rangeCount || selection.isCollapsed || !editor.surface.contains(selection.anchorNode)) {
        event.preventDefault(); return;
      }
      selected = selection.getRangeAt(0).cloneRange();
      draggingSelection = true;
      toolbar.hidden = true;
      collection.open();
      if (event.dataTransfer) { event.dataTransfer.effectAllowed = 'copy'; }
    });
    editor.surface.addEventListener('keydown', event => {
      if ((event.ctrlKey || event.metaKey) && event.key.toLowerCase() === 's') { event.preventDefault(); void saveAll().catch(failure); }
      if (event.key === 'Escape') { toolbar.hidden = true; selected = null; }
    });
  }
  document.addEventListener('selectionchange', () => {
    if (draggingSelection || collecting) return;
    const selection = window.getSelection();
    if (!selection?.rangeCount || selection.isCollapsed) { if (!toolbar.contains(document.activeElement)) toolbar.hidden = true; return; }
    const range = selection.getRangeAt(0);
    if (!editors.some(e => e.surface.contains(range.startContainer)) || !editors.some(e => e.surface.contains(range.endContainer))) {
      toolbar.hidden = true; selected = null; return;
    }
    selected = range.cloneRange();
    toolbar.hidden = false;
    positionTools();
  });
  window.addEventListener('scroll', positionTools, { passive: true });
  window.addEventListener('resize', positionTools);
  toolbar.addEventListener('mousedown', event => { if (event.target instanceof Element && event.target.closest('button')) event.preventDefault(); });
  toolbar.querySelectorAll<HTMLButtonElement>('[data-format]').forEach(button => button.addEventListener('click', () => {
    if (!selected) return;
    const selection = window.getSelection()!;
    selection.removeAllRanges(); selection.addRange(selected);
    const command = button.dataset.format!;
    document.execCommand(command === 'h2' ? 'formatBlock' : command, false, command === 'h2' ? 'h2' : undefined);
    changed();
  }));

  async function collect(targetId?: string): Promise<void> {
    if (!selected || collecting || collection.isBusy()) return;
    if (targetId) collection.select(targetId);
    const selectedTarget = collection.current();
    if (!selectedTarget || (targetId && selectedTarget.id !== targetId)) return;
    let target: Collection = selectedTarget;
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
    collection.setBusy(true);
    toolbar.querySelectorAll<HTMLButtonElement>('button').forEach(b => b.disabled = true);
    try {
      await saveAll();
      for (const part of parts) {
        if (part.editor.saved !== part.source || markdown(part.editor.surface) !== part.source) {
          throw new Error('Der Text wurde während des Sammelns geändert. Bitte die Passage erneut markieren.');
        }
        const result = await post<Collection>('Collect', { sectionId: part.editor.id, etag: part.editor.etag, markdown: part.text,
          draftId: target.id, draftETag: target.etag });
        target = result;
        collection.update(result);
      }
      const message = `${parts.length === 1 ? 'Passage' : 'Passagen'} in ${target.name} gesammelt.`;
      report(message);
      root.querySelector<HTMLElement>('[data-collection-status]')!.textContent = message;
      toolbar.hidden = true;
      selected = null;
    } catch (error) { failure(error); }
    finally { collecting = false; toolbar.querySelectorAll<HTMLButtonElement>('button').forEach(b => b.disabled = false); collection.setBusy(false); }
  }
  toolbar.querySelector('[data-collect]')!.addEventListener('click', () => { void collect(); });
  const dropTarget = (event: DragEvent): HTMLElement | null => event.target instanceof Element
    ? event.target.closest<HTMLElement>('[data-review-dropzone], [data-review-target], [data-review-panel] summary') : null;
  const clearDrop = (): void => root.querySelectorAll('[data-drop-active]').forEach(el => el.removeAttribute('data-drop-active'));
  root.addEventListener('dragover', event => {
    if (!draggingSelection || collecting) return;
    clearDrop();
    const target = dropTarget(event);
    if (!target) return;
    event.preventDefault();
    if (event.dataTransfer) event.dataTransfer.dropEffect = 'copy';
    target.setAttribute('data-drop-active', '');
  });
  root.addEventListener('drop', event => {
    const target = dropTarget(event);
    if (!draggingSelection || !target) return;
    event.preventDefault();
    draggingSelection = false;
    clearDrop();
    if (target.dataset.reviewTarget) collection.select(target.dataset.reviewTarget);
    void collect();
  });
  document.addEventListener('dragend', () => { draggingSelection = false; clearDrop(); });
}

if (typeof document !== 'undefined') document.querySelectorAll<HTMLElement>('[data-document-editor]').forEach(init);
