/* Document editor 1.1.0. Redeems recipient links and copies owner invitations. */

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
