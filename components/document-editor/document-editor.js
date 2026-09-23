// components/document-editor/review-access.js
if (typeof document !== "undefined") {
  const access = document.querySelector("[data-review-access]");
  const token = new URLSearchParams(location.hash.slice(1)).get("token");
  if (access && token && /^[a-f0-9]{64}$/.test(token)) {
    access.querySelector('[name="token"]').value = token;
    history.replaceState(null, "", location.pathname);
    access.requestSubmit();
  }
  const link = document.querySelector("[data-share-link]");
  if (link) {
    link.value = new URL(link.value, location.origin).href;
    document.querySelector("[data-copy-link]")?.addEventListener("click", () => {
      const status = document.querySelector("[data-copy-status]");
      if (!navigator.clipboard) {
        link.select();
        status.textContent = "Link markiert. Mit Strg+C kopieren.";
        return;
      }
      void navigator.clipboard.writeText(link.value).then(() => {
        status.textContent = "Link kopiert";
      }).catch(() => {
        link.select();
        status.textContent = "Link markiert. Mit Strg+C kopieren.";
      });
    });
  }
}

// components/document-editor/markdown.js
function escapeMarkdown(text) {
  return text.replace(/\\/g, "\\\\").replace(/([*_[\]`#>|])/g, "\\$1").replace(/\u00a0/g, " ");
}
function toMarkdown(node) {
  const children = node.children ?? [];
  const body = children.map(toMarkdown).join("");
  switch (node.tag) {
    case "#text":
      return escapeMarkdown(node.text ?? "");
    case "script":
    case "style":
    case "img":
    case "iframe":
      return "";
    case "br":
      return "\n";
    case "strong":
    case "b":
      return body.trim() ? `**${body}**` : body;
    case "em":
    case "i":
      return body.trim() ? `*${body}*` : body;
    case "code": {
      const raw = (node.text ?? body).replace(/\n/g, " ");
      const fence = "`".repeat(Math.max(0, ...(raw.match(/`+/g) ?? []).map((s) => s.length)) + 1);
      return `${fence} ${raw} ${fence}`;
    }
    case "pre": {
      const raw = node.text ?? body;
      const fence = "`".repeat(Math.max(2, ...(raw.match(/`+/g) ?? []).map((s) => s.length)) + 1);
      return `

${fence}
${raw}
${fence}

`;
    }
    case "h1":
    case "h2":
    case "h3":
    case "h4":
    case "h5":
    case "h6":
      return `

${"#".repeat(Number(node.tag[1]))} ${body.trim()}

`;
    case "p":
    case "div":
      return `

${body.trim()}

`;
    case "blockquote":
      return "\n\n" + body.trim().split("\n").map((line) => "> " + line).join("\n") + "\n\n";
    case "ul":
    case "ol":
      return "\n\n" + children.filter((n) => n.tag === "li").map((n, index) => {
        const content = toMarkdown(n).trim().replace(/\n/g, "\n    ");
        return `${node.tag === "ol" ? `${index + 1}.` : "-"} ${content}`;
      }).join("\n") + "\n\n";
    case "a":
      return node.href && /^(https?:|mailto:)/i.test(node.href) ? `[${body}](${node.href.replace(/[()\s<>]/g, (c) => encodeURIComponent(c))})` : body;
    case "table": {
      const rows = [];
      const visit = (n) => {
        if (n.tag === "tr") rows.push(n);
        else n.children?.forEach(visit);
      };
      children.forEach(visit);
      const lines = rows.map((row) => "| " + (row.children ?? []).filter((c) => c.tag === "td" || c.tag === "th").map((c) => toMarkdown(c).trim().replace(/\n+/g, " ")).join(" | ") + " |");
      if (lines.length) lines.splice(1, 0, "| " + (rows[0]?.children ?? []).filter((c) => c.tag === "td" || c.tag === "th").map(() => "---").join(" | ") + " |");
      return "\n\n" + lines.join("\n") + "\n\n";
    }
    default:
      return body;
  }
}
function tree(node) {
  if (node.nodeType === Node.TEXT_NODE) return { tag: "#text", text: node.textContent ?? "" };
  const tag = node instanceof Element ? node.tagName.toLowerCase() : "root";
  return { tag, text: node.textContent ?? "", href: node instanceof HTMLAnchorElement ? node.getAttribute("href") ?? "" : "", children: [...node.childNodes].map(tree) };
}
function markdown(node) {
  return toMarkdown(tree(node)).replace(/\n{3,}/g, "\n\n").trim();
}

// components/document-editor/review-collection.js
function reviewCollection(root, post, onCollect) {
  const panel = root.querySelector("[data-review-panel]");
  const badges = root.querySelector("[data-review-badges]");
  const selector = root.querySelector("[data-review-select]");
  const choices = root.querySelector("[data-review-choices]");
  const collectButton = root.querySelector("[data-collect]");
  const list = root.querySelector("[data-collection-list]");
  const link = root.querySelector("[data-collection-link]");
  const create = root.querySelector("[data-create-set]");
  const close = root.querySelector("[data-close-set]");
  const createPanel = root.querySelector("[data-create-set-panel]");
  const details = root.querySelector("[data-set-details]");
  const status = root.querySelector("[data-collection-status]");
  const collections = /* @__PURE__ */ new Map();
  let active = root.dataset.draftId ?? "";
  let busy = false;
  for (const badge of badges.querySelectorAll("[data-review-draft]")) {
    const collection = JSON.parse(badge.dataset.reviewDraft);
    collections.set(collection.id, collection);
  }
  const current = () => collections.get(active);
  const title = () => current()?.name ?? "Kein offenes Set";
  const reviewUrl = (id) => `${location.pathname}/reviews/${id}`;
  function select(id) {
    if (busy || !collections.has(id) || collections.get(id).closed) return;
    active = id;
    details.open = false;
    render();
    status.textContent = `${title()} ausgew\xE4hlt.`;
  }
  function render() {
    const focused = document.activeElement instanceof HTMLElement ? document.activeElement.dataset.reviewTarget : void 0;
    const open = [...collections.values()].filter((c) => !c.closed);
    const closed = [...collections.values()].filter((c) => c.closed);
    if (!open.some((c) => c.id === active)) active = open[0]?.id ?? "";
    const collection = current();
    root.dataset.draftId = collection?.id ?? "";
    root.dataset.draftEtag = collection?.etag ?? "";
    root.querySelector("[data-active-set]").hidden = !collection;
    root.querySelector("[data-sets-empty]").hidden = !!collection;
    root.querySelector("[data-collection-count]").textContent = String(collection?.passages.length ?? 0);
    root.querySelector("[data-collection-label]").textContent = collection?.passages.length === 1 ? "Passage" : "Passagen";
    root.querySelector("[data-collection-destination]").textContent = title();
    list.replaceChildren(...(collection?.passages ?? []).map((p) => {
      const li = document.createElement("li");
      li.textContent = p.preview;
      return li;
    }));
    link.hidden = !collection?.passages.length;
    if (collection) link.href = reviewUrl(collection.id);
    close.querySelector('[name="reviewId"]').value = collection?.id ?? "";
    close.querySelector('[name="etag"]').value = collection?.etag ?? "";
    badges.replaceChildren(...open.map((entry) => {
      const button = document.createElement("button");
      button.type = "button";
      button.dataset.reviewTarget = entry.id;
      button.className = "document-editor__review-badge";
      button.textContent = `${entry.name} \xB7 ${entry.passages.length}`;
      button.setAttribute("aria-pressed", String(entry.id === active));
      button.setAttribute("aria-controls", "review-collection");
      button.disabled = busy;
      button.addEventListener("click", () => select(entry.id));
      return button;
    }));
    const primary = open.slice(0, 3);
    choices.replaceChildren(...primary.map((entry) => {
      const button = document.createElement("button");
      button.type = "button";
      button.className = "document-editor__set-choice";
      button.dataset.reviewTarget = entry.id;
      button.setAttribute("aria-label", `Markierung zu ${entry.name} hinzuf\xFCgen`);
      const dot = document.createElement("span");
      dot.className = "document-editor__set-choice-dot";
      dot.setAttribute("aria-hidden", "true");
      const name = document.createElement("span");
      name.className = "document-editor__set-choice-name";
      name.textContent = entry.name;
      const count = document.createElement("span");
      count.className = "document-editor__set-choice-count";
      count.textContent = String(entry.passages.length);
      button.title = entry.passages.at(-1)?.preview ?? entry.name;
      button.append(dot, name, count);
      button.disabled = busy;
      button.addEventListener("click", () => onCollect(entry.id));
      return button;
    }));
    const additional = open.slice(3);
    selector.replaceChildren(new Option("Weitere Sets", ""), ...additional.map((entry) => new Option(entry.name, entry.id)));
    selector.hidden = additional.length === 0;
    selector.value = additional.some((entry) => entry.id === active) ? active : "";
    selector.disabled = busy || !additional.length;
    collectButton.disabled = busy || !open.length;
    collectButton.setAttribute("aria-label", collection ? `Markierung zu ${collection.name} hinzuf\xFCgen` : "Zuerst ein Review-Set anlegen");
    root.querySelector("[data-closed-count]").textContent = String(closed.length);
    root.querySelector("[data-closed-sets-panel]").hidden = closed.length === 0;
    root.querySelector("[data-closed-sets]").replaceChildren(...closed.map((entry) => {
      const row = document.createElement("div");
      row.className = "document-editor__closed-set";
      const anchor = document.createElement("a");
      anchor.href = reviewUrl(entry.id);
      anchor.textContent = `${entry.name} \xB7 ${entry.passages.length} Passagen`;
      const reopen = document.createElement("button");
      reopen.type = "button";
      reopen.className = "document-editor__new-review";
      reopen.textContent = "Wieder \xF6ffnen";
      reopen.setAttribute("aria-label", `${entry.name}: Sammlung wieder \xF6ffnen`);
      reopen.disabled = busy;
      reopen.addEventListener("click", () => {
        void changeClosed(entry, false);
      });
      row.append(anchor, reopen);
      return row;
    }));
    create.querySelectorAll("input, button").forEach((el) => el.disabled = busy);
    close.querySelector("button").disabled = busy || !collection;
    if (focused && !busy) [...badges.querySelectorAll("button")].find((b) => b.dataset.reviewTarget === focused)?.focus({ preventScroll: true });
  }
  function setBusy(value) {
    busy = value;
    panel.setAttribute("aria-busy", String(value));
    render();
  }
  function update(collection) {
    collections.set(collection.id, collection);
    if (!collection.closed) active = collection.id;
    render();
  }
  function failure(error) {
    status.textContent = error instanceof Error ? error.message : "Nicht gespeichert. Bitte erneut versuchen.";
  }
  async function changeClosed(entry, closed) {
    if (busy) return;
    setBusy(true);
    try {
      update(await post("CloseSet", { reviewId: entry.id, etag: entry.etag, closed: String(closed) }));
      details.open = false;
      status.textContent = closed ? `${entry.name}: Sammlung abgeschlossen. Du kannst sie sp\xE4ter zuweisen.` : `${entry.name}: Sammlung wieder ge\xF6ffnet.`;
    } catch (error) {
      failure(error);
    } finally {
      setBusy(false);
    }
  }
  selector.addEventListener("change", () => {
    if (selector.value) onCollect(selector.value);
  });
  create.addEventListener("submit", (event) => {
    event.preventDefault();
    if (busy || !create.reportValidity()) return;
    const input = create.querySelector('[name="setName"]');
    const name = input.value;
    setBusy(true);
    void post("CreateSet", { setName: name }).then((entry) => {
      update(entry);
      input.value = "";
      createPanel.open = false;
      status.textContent = `${entry.name} angelegt. Markiere jetzt eine Passage im Dokument.`;
    }).catch(failure).finally(() => setBusy(false));
  });
  close.addEventListener("submit", (event) => {
    event.preventDefault();
    const entry = current();
    if (entry) void changeClosed(entry, true);
  });
  render();
  return {
    current,
    title,
    select,
    setBusy,
    update,
    isBusy: () => busy,
    open: () => {
      if (!current()) createPanel.open = true;
    }
  };
}

// components/document-editor/document-editor.ts
function safePaste(html) {
  const source = new DOMParser().parseFromString(html, "text/html");
  const allowed = /* @__PURE__ */ new Set(["P", "DIV", "BR", "H1", "H2", "H3", "H4", "H5", "H6", "STRONG", "B", "EM", "I", "UL", "OL", "LI", "BLOCKQUOTE", "PRE", "CODE", "A", "TABLE", "THEAD", "TBODY", "TR", "TD", "TH"]);
  for (const el of [...source.body.querySelectorAll("*")]) {
    if (["SCRIPT", "STYLE", "IFRAME", "OBJECT", "IMG", "SVG", "MATH"].includes(el.tagName)) {
      el.remove();
      continue;
    }
    if (!allowed.has(el.tagName)) {
      el.replaceWith(...el.childNodes);
      continue;
    }
    const href = el.getAttribute("href");
    for (const attribute of [...el.attributes]) el.removeAttribute(attribute.name);
    if (el.tagName === "A" && href && /^(https?:|mailto:)/i.test(href)) el.setAttribute("href", href);
  }
  return source.body.innerHTML;
}
function init(root) {
  const collection = reviewCollection(root, post, (targetId) => {
    void collect(targetId);
  });
  const status = root.querySelector("[data-save-status]");
  const toolbar = root.querySelector("[data-editor-tools]");
  const saveButton = root.querySelector("[data-save-all]");
  const editors = [...root.querySelectorAll("[data-editor-section]")].map((form) => {
    const surface = form.querySelector("[data-editor-surface]");
    const result = { form, surface, saved: markdown(surface), etag: form.dataset.textEtag ?? "", id: form.dataset.sectionId ?? "" };
    surface.contentEditable = "true";
    surface.setAttribute("role", "textbox");
    surface.setAttribute("aria-multiline", "true");
    surface.hidden = false;
    surface.spellcheck = true;
    form.querySelector("[data-editor-fallback]").hidden = true;
    return result;
  });
  saveButton.hidden = false;
  root.querySelector("[data-open-reviews]").hidden = false;
  let queue = Promise.resolve();
  let timer;
  let selected = null;
  let collecting = false;
  let draggingSelection = false;
  const positionTools = () => {
    if (!selected || toolbar.hidden) return;
    const rect = selected.getBoundingClientRect();
    const gap = parseFloat(getComputedStyle(root).getPropertyValue("--space-3")) || 12;
    const width = toolbar.getBoundingClientRect().width;
    const height = toolbar.getBoundingClientRect().height;
    toolbar.style.setProperty("--document-editor-menu-x", `${Math.max(gap, Math.min(rect.left, innerWidth - width - gap))}px`);
    toolbar.style.setProperty("--document-editor-menu-y", `${Math.max(gap, Math.min(rect.top > height + gap ? rect.top - height - gap : rect.bottom + gap, innerHeight - height - gap))}px`);
  };
  const dirty = () => editors.some((e) => markdown(e.surface) !== e.saved);
  const report = (message) => {
    status.textContent = message;
  };
  const token = root.querySelector('[name="__RequestVerificationToken"]').value;
  async function post(handler, fields) {
    const body = new URLSearchParams({ ...fields, __RequestVerificationToken: token });
    const response = await fetch(`${location.pathname}?handler=${handler}`, { method: "POST", body, headers: { "X-Requested-With": "fetch" } });
    if (!response.headers.get("content-type")?.includes("application/json")) throw new Error("Speichern nicht m\xF6glich. Pr\xFCfe deine Anmeldung; dein Text bleibt hier erhalten.");
    const result = await response.json();
    if (!response.ok) throw new Error(result.error ?? "Speichern fehlgeschlagen. Bitte erneut versuchen.");
    return result;
  }
  function saveAll() {
    clearTimeout(timer);
    const operation = queue.catch(() => {
    }).then(async () => {
      for (const editor of editors) {
        const value = markdown(editor.surface);
        if (value === editor.saved) continue;
        report("Wird gespeichert \u2026");
        const result = await post("Save", {
          sectionId: editor.id,
          markdown: value,
          etag: editor.etag,
          documentETag: root.dataset.documentEtag ?? ""
        });
        editor.saved = value;
        editor.id = result.sectionId;
        editor.etag = result.etag;
      }
      report(dirty() ? "Noch ungespeicherte \xC4nderungen" : "Alle \xC4nderungen gespeichert");
    });
    queue = operation;
    return operation;
  }
  const failure = (error) => report(error instanceof Error ? error.message : "Verbindung unterbrochen. Bitte erneut speichern.");
  const changed = () => {
    report("Ungespeicherte \xC4nderungen");
    clearTimeout(timer);
    timer = setTimeout(() => {
      void saveAll().catch(failure);
    }, 1e3);
  };
  saveButton.addEventListener("click", () => {
    void saveAll().catch(failure);
  });
  root.querySelector("[data-open-reviews]")?.addEventListener("click", () => {
    collection.open();
    const heading = root.querySelector("[data-review-panel] h2");
    heading.tabIndex = -1;
    heading.scrollIntoView({ block: "nearest" });
    heading.focus({ preventScroll: true });
  });
  root.querySelector("[data-collection-link]").addEventListener("click", (event) => {
    event.preventDefault();
    if (collecting) return;
    const href = event.currentTarget.href;
    void saveAll().then(() => {
      if (!dirty()) location.assign(href);
    }).catch(failure);
  });
  window.addEventListener("beforeunload", (event) => {
    if (dirty()) {
      event.preventDefault();
      event.returnValue = "";
    }
  });
  for (const editor of editors) {
    editor.surface.addEventListener("input", changed);
    editor.surface.addEventListener("paste", (event) => {
      event.preventDefault();
      const html = event.clipboardData?.getData("text/html");
      if (html) document.execCommand("insertHTML", false, safePaste(html));
      else document.execCommand("insertText", false, event.clipboardData?.getData("text/plain") ?? "");
      changed();
    });
    editor.surface.addEventListener("drop", (event) => {
      event.preventDefault();
    });
    editor.surface.addEventListener("dragstart", (event) => {
      const selection = window.getSelection();
      if (collecting || !selection?.rangeCount || selection.isCollapsed || !editor.surface.contains(selection.anchorNode)) {
        event.preventDefault();
        return;
      }
      selected = selection.getRangeAt(0).cloneRange();
      draggingSelection = true;
      toolbar.hidden = true;
      collection.open();
      if (event.dataTransfer) {
        event.dataTransfer.effectAllowed = "copy";
      }
    });
    editor.surface.addEventListener("keydown", (event) => {
      if ((event.ctrlKey || event.metaKey) && event.key.toLowerCase() === "s") {
        event.preventDefault();
        void saveAll().catch(failure);
      }
      if (event.key === "Escape") {
        toolbar.hidden = true;
        selected = null;
      }
    });
  }
  document.addEventListener("selectionchange", () => {
    if (draggingSelection || collecting) return;
    const selection = window.getSelection();
    if (!selection?.rangeCount || selection.isCollapsed) {
      if (!toolbar.contains(document.activeElement)) toolbar.hidden = true;
      return;
    }
    const range = selection.getRangeAt(0);
    if (!editors.some((e) => e.surface.contains(range.startContainer)) || !editors.some((e) => e.surface.contains(range.endContainer))) {
      toolbar.hidden = true;
      selected = null;
      return;
    }
    selected = range.cloneRange();
    toolbar.hidden = false;
    positionTools();
  });
  window.addEventListener("scroll", positionTools, { passive: true });
  window.addEventListener("resize", positionTools);
  toolbar.addEventListener("mousedown", (event) => {
    if (event.target instanceof Element && event.target.closest("button")) event.preventDefault();
  });
  toolbar.querySelectorAll("[data-format]").forEach((button) => button.addEventListener("click", () => {
    if (!selected) return;
    const selection = window.getSelection();
    selection.removeAllRanges();
    selection.addRange(selected);
    const command = button.dataset.format;
    document.execCommand(command === "h2" ? "formatBlock" : command, false, command === "h2" ? "h2" : void 0);
    changed();
  }));
  async function collect(targetId) {
    if (!selected || collecting || collection.isBusy()) return;
    if (targetId) collection.select(targetId);
    const selectedTarget = collection.current();
    if (!selectedTarget || targetId && selectedTarget.id !== targetId) return;
    let target = selectedTarget;
    const parts = [];
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
    toolbar.querySelectorAll("button").forEach((b) => b.disabled = true);
    try {
      await saveAll();
      for (const part of parts) {
        if (part.editor.saved !== part.source || markdown(part.editor.surface) !== part.source) {
          throw new Error("Der Text wurde w\xE4hrend des Sammelns ge\xE4ndert. Bitte die Passage erneut markieren.");
        }
        const result = await post("Collect", {
          sectionId: part.editor.id,
          etag: part.editor.etag,
          markdown: part.text,
          draftId: target.id,
          draftETag: target.etag
        });
        target = result;
        collection.update(result);
      }
      const message = `${parts.length === 1 ? "Passage" : "Passagen"} in ${target.name} gesammelt.`;
      report(message);
      root.querySelector("[data-collection-status]").textContent = message;
      toolbar.hidden = true;
      selected = null;
    } catch (error) {
      failure(error);
    } finally {
      collecting = false;
      toolbar.querySelectorAll("button").forEach((b) => b.disabled = false);
      collection.setBusy(false);
    }
  }
  toolbar.querySelector("[data-collect]").addEventListener("click", () => {
    void collect();
  });
  const dropTarget = (event) => event.target instanceof Element ? event.target.closest("[data-review-dropzone], [data-review-target], [data-review-panel] summary") : null;
  const clearDrop = () => root.querySelectorAll("[data-drop-active]").forEach((el) => el.removeAttribute("data-drop-active"));
  root.addEventListener("dragover", (event) => {
    if (!draggingSelection || collecting) return;
    clearDrop();
    const target = dropTarget(event);
    if (!target) return;
    event.preventDefault();
    if (event.dataTransfer) event.dataTransfer.dropEffect = "copy";
    target.setAttribute("data-drop-active", "");
  });
  root.addEventListener("drop", (event) => {
    const target = dropTarget(event);
    if (!draggingSelection || !target) return;
    event.preventDefault();
    draggingSelection = false;
    clearDrop();
    if (target.dataset.reviewTarget) collection.select(target.dataset.reviewTarget);
    void collect();
  });
  document.addEventListener("dragend", () => {
    draggingSelection = false;
    clearDrop();
  });
}
if (typeof document !== "undefined") document.querySelectorAll("[data-document-editor]").forEach(init);
