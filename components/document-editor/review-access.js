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
