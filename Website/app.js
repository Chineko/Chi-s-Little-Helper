(() => {
  const field = document.getElementById('repo-url');
  const addButton = document.getElementById('add-vcc');
  const copyButton = document.getElementById('copy-url');

  const repoUrl = () => (field?.value || '').trim();

  addButton?.addEventListener('click', () => {
    const url = repoUrl();
    if (!url) return;
    window.location.href = `vcc://vpm/addRepo?url=${encodeURIComponent(url)}`;
  });

  copyButton?.addEventListener('click', async () => {
    const url = repoUrl();
    if (!url) return;

    try {
      await navigator.clipboard.writeText(url);
      const original = copyButton.textContent;
      copyButton.textContent = 'Copied';
      window.setTimeout(() => { copyButton.textContent = original; }, 1200);
    } catch {
      field?.select();
      document.execCommand('copy');
    }
  });
})();
