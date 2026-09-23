/* Document editor 1.2.0. Owns named review sets, selection destinations and
 * progressive disclosure; collection completion is independent of assignment. */
export interface Collection {
  id: string;
  etag: string;
  name: string;
  closed: boolean;
  passages: { id: string; heading: string; preview: string }[];
}
type Post = <T>(handler: string, fields: Record<string, string>) => Promise<T>;

export function reviewCollection(root: HTMLElement, post: Post, onCollect: (targetId: string) => void) {
  const panel = root.querySelector<HTMLElement>('[data-review-panel]')!;
  const badges = root.querySelector<HTMLElement>('[data-review-badges]')!;
  const selector = root.querySelector<HTMLSelectElement>('[data-review-select]')!;
  const choices = root.querySelector<HTMLElement>('[data-review-choices]')!;
  const collectButton = root.querySelector<HTMLButtonElement>('[data-collect]')!;
  const list = root.querySelector<HTMLElement>('[data-collection-list]')!;
  const link = root.querySelector<HTMLAnchorElement>('[data-collection-link]')!;
  const create = root.querySelector<HTMLFormElement>('[data-create-set]')!;
  const close = root.querySelector<HTMLFormElement>('[data-close-set]')!;
  const createPanel = root.querySelector<HTMLDetailsElement>('[data-create-set-panel]')!;
  const details = root.querySelector<HTMLDetailsElement>('[data-set-details]')!;
  const status = root.querySelector<HTMLElement>('[data-collection-status]')!;
  const collections = new Map<string, Collection>();
  let active = root.dataset.draftId ?? '';
  let busy = false;
  for (const badge of badges.querySelectorAll<HTMLElement>('[data-review-draft]')) {
    const collection = JSON.parse(badge.dataset.reviewDraft!) as Collection;
    collections.set(collection.id, collection);
  }
  const current = (): Collection | undefined => collections.get(active);
  const title = (): string => current()?.name ?? 'Kein offenes Set';
  const reviewUrl = (id: string): string => `${location.pathname}/reviews/${id}`;
  function select(id: string): void {
    if (busy || !collections.has(id) || collections.get(id)!.closed) return;
    active = id;
    details.open = false;
    render();
    status.textContent = `${title()} ausgewählt.`;
  }
  function render(): void {
    const focused = document.activeElement instanceof HTMLElement ? document.activeElement.dataset.reviewTarget : undefined;
    const open = [...collections.values()].filter(c => !c.closed);
    const closed = [...collections.values()].filter(c => c.closed);
    if (!open.some(c => c.id === active)) active = open[0]?.id ?? '';
    const collection = current();
    root.dataset.draftId = collection?.id ?? '';
    root.dataset.draftEtag = collection?.etag ?? '';
    root.querySelector<HTMLElement>('[data-active-set]')!.hidden = !collection;
    root.querySelector<HTMLElement>('[data-sets-empty]')!.hidden = !!collection;
    root.querySelector('[data-collection-count]')!.textContent = String(collection?.passages.length ?? 0);
    root.querySelector('[data-collection-label]')!.textContent = collection?.passages.length === 1 ? 'Passage' : 'Passagen';
    root.querySelector('[data-collection-destination]')!.textContent = title();
    list.replaceChildren(...(collection?.passages ?? []).map(p => {
      const li = document.createElement('li'); li.textContent = p.preview; return li;
    }));
    link.hidden = !collection?.passages.length;
    if (collection) link.href = reviewUrl(collection.id);
    close.querySelector<HTMLInputElement>('[name="reviewId"]')!.value = collection?.id ?? '';
    close.querySelector<HTMLInputElement>('[name="etag"]')!.value = collection?.etag ?? '';
    badges.replaceChildren(...open.map(entry => {
      const button = document.createElement('button');
      button.type = 'button'; button.dataset.reviewTarget = entry.id;
      button.className = 'document-editor__review-badge';
      button.textContent = `${entry.name} · ${entry.passages.length}`;
      button.setAttribute('aria-pressed', String(entry.id === active));
      button.setAttribute('aria-controls', 'review-collection'); button.disabled = busy;
      button.addEventListener('click', () => select(entry.id)); return button;
    }));
    const primary = open.slice(0, 3);
    choices.replaceChildren(...primary.map(entry => {
      const button = document.createElement('button');
      button.type = 'button';
      button.className = 'document-editor__set-choice';
      button.dataset.reviewTarget = entry.id;
      button.setAttribute('aria-label', `Markierung zu ${entry.name} hinzufügen`);
      const dot = document.createElement('span'); dot.className = 'document-editor__set-choice-dot'; dot.setAttribute('aria-hidden', 'true');
      const name = document.createElement('span'); name.className = 'document-editor__set-choice-name'; name.textContent = entry.name;
      const count = document.createElement('span'); count.className = 'document-editor__set-choice-count';
      count.textContent = String(entry.passages.length);
      button.title = entry.passages.at(-1)?.preview ?? entry.name;
      button.append(dot, name, count);
      button.disabled = busy;
      button.addEventListener('click', () => onCollect(entry.id));
      return button;
    }));
    const additional = open.slice(3);
    selector.replaceChildren(new Option('Weitere Sets', ''), ...additional.map(entry => new Option(entry.name, entry.id)));
    selector.hidden = additional.length === 0;
    selector.value = additional.some(entry => entry.id === active) ? active : '';
    selector.disabled = busy || !additional.length;
    collectButton.disabled = busy || !open.length;
    collectButton.setAttribute('aria-label', collection ? `Markierung zu ${collection.name} hinzufügen` : 'Zuerst ein Review-Set anlegen');
    root.querySelector('[data-closed-count]')!.textContent = String(closed.length);
    root.querySelector<HTMLElement>('[data-closed-sets-panel]')!.hidden = closed.length === 0;
    root.querySelector('[data-closed-sets]')!.replaceChildren(...closed.map(entry => {
      const row = document.createElement('div'); row.className = 'document-editor__closed-set';
      const anchor = document.createElement('a'); anchor.href = reviewUrl(entry.id);
      anchor.textContent = `${entry.name} · ${entry.passages.length} Passagen`;
      const reopen = document.createElement('button'); reopen.type = 'button'; reopen.className = 'document-editor__new-review';
      reopen.textContent = 'Wieder öffnen'; reopen.setAttribute('aria-label', `${entry.name}: Sammlung wieder öffnen`); reopen.disabled = busy;
      reopen.addEventListener('click', () => { void changeClosed(entry, false); });
      row.append(anchor, reopen); return row;
    }));
    create.querySelectorAll<HTMLInputElement | HTMLButtonElement>('input, button').forEach(el => el.disabled = busy);
    close.querySelector<HTMLButtonElement>('button')!.disabled = busy || !collection;
    if (focused && !busy) [...badges.querySelectorAll<HTMLButtonElement>('button')].find(b => b.dataset.reviewTarget === focused)?.focus({ preventScroll: true });
  }
  function setBusy(value: boolean): void { busy = value; panel.setAttribute('aria-busy', String(value)); render(); }
  function update(collection: Collection): void {
    collections.set(collection.id, collection);
    if (!collection.closed) active = collection.id;
    render();
  }
  function failure(error: unknown): void { status.textContent = error instanceof Error ? error.message : 'Nicht gespeichert. Bitte erneut versuchen.'; }
  async function changeClosed(entry: Collection, closed: boolean): Promise<void> {
    if (busy) return;
    setBusy(true);
    try {
      update(await post<Collection>('CloseSet', {reviewId: entry.id, etag: entry.etag, closed: String(closed)}));
      details.open = false;
      status.textContent = closed ? `${entry.name}: Sammlung abgeschlossen. Du kannst sie später zuweisen.` : `${entry.name}: Sammlung wieder geöffnet.`;
    } catch (error) { failure(error); }
    finally { setBusy(false); }
  }
  selector.addEventListener('change', () => { if (selector.value) onCollect(selector.value); });
  create.addEventListener('submit', event => {
    event.preventDefault();
    if (busy || !create.reportValidity()) return;
    const input = create.querySelector<HTMLInputElement>('[name="setName"]')!;
    const name = input.value;
    setBusy(true);
    void post<Collection>('CreateSet', {setName:name}).then(entry => {
      update(entry); input.value = ''; createPanel.open = false;
      status.textContent = `${entry.name} angelegt. Markiere jetzt eine Passage im Dokument.`;
    }).catch(failure).finally(() => setBusy(false));
  });
  close.addEventListener('submit', event => { event.preventDefault(); const entry = current(); if (entry) void changeClosed(entry, true); });
  render();
  return { current, title, select, setBusy, update, isBusy: (): boolean => busy,
    open: (): void => { if (!current()) createPanel.open = true; },
  };
}
