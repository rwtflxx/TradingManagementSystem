/* StockFlow: three selectable themes; saved locally per browser. */
(function () {
  'use strict';
  const key = 'stockflow.theme.v1';
  const allowed = ['mixed', 'light', 'dark'];
  function getTheme() {
    const value = document.documentElement.getAttribute('data-sf-theme');
    return allowed.includes(value) ? value : 'mixed';
  }
  function applyTheme(next) {
    const theme = allowed.includes(next) ? next : 'mixed';
    document.documentElement.setAttribute('data-sf-theme', theme);
    document.querySelectorAll('[data-sf-theme-choice]').forEach(function (button) {
      const selected = button.getAttribute('data-sf-theme-choice') === theme;
      button.setAttribute('aria-pressed', selected ? 'true' : 'false');
    });
    const names = {mixed: 'Комбінована', light: 'Світла', dark: 'Темна'};
    document.querySelectorAll('[data-sf-theme-label]').forEach(function (node) {node.textContent = names[theme];});
    try { localStorage.setItem(key, theme); } catch (_) { /* storage may be disabled */ }
  }
  document.addEventListener('click', function (event) {
    const button = event.target.closest('[data-sf-theme-choice]');
    if (button) applyTheme(button.getAttribute('data-sf-theme-choice'));
  });
  function updateNavigation() {
    const current = window.location.pathname.replace(/\/+$/, '').toLowerCase() || '/';
    const links = Array.from(document.querySelectorAll('.side-nav a[href]'));
    links.forEach(function (link) {
      const path = new URL(link.href, window.location.origin).pathname.replace(/\/+$/, '').toLowerCase() || '/';
      const active = path === current;
      link.classList.toggle('is-active', active);
      if (active) link.setAttribute('aria-current', 'page');
      else link.removeAttribute('aria-current');
    });
    if (!links.some(function (link) {return link.classList.contains('is-active');}) && current === '/') {
      const first = links[0];
      if (first) { first.classList.add('is-active'); first.setAttribute('aria-current', 'page'); }
    }
  }
  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', function () { applyTheme(getTheme()); updateNavigation(); });
  } else { applyTheme(getTheme()); updateNavigation(); }
})();