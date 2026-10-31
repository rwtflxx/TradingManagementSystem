/* StockFlow: single accessible button cycles mixed -> light -> dark. */
(function () {
  'use strict';
  const key = 'stockflow.theme.v1';
  const modes = ['mixed', 'light', 'dark'];
  const labels = { mixed: 'Комбінована', light: 'Світла', dark: 'Темна' };
  const icons = { mixed: '◐', light: '☀', dark: '☾' };
  function getMode() {
    const m = document.documentElement.getAttribute('data-sf-theme');
    return modes.includes(m) ? m : 'mixed';
  }
  function applyMode(mode) {
    const current = modes.includes(mode) ? mode : 'mixed';
    document.documentElement.setAttribute('data-sf-theme', current);
    document.querySelectorAll('[data-sf-theme-cycle]').forEach(function (button) {
      const next = modes[(modes.indexOf(current) + 1) % modes.length];
      button.setAttribute('title', 'Зараз: ' + labels[current] + '. Натисни, щоб увімкнути: ' + labels[next]);
      button.setAttribute('aria-label', 'Тема: ' + labels[current] + '. Перемкнути на: ' + labels[next]);
      const icon = button.querySelector('[data-sf-theme-icon]');
      const label = button.querySelector('[data-sf-theme-label]');
      if (icon) icon.textContent = icons[current];
      if (label) label.textContent = labels[current];
    });
    try { localStorage.setItem(key, current); } catch (_) { }
  }
  document.addEventListener('click', function (ev) {
    const b = ev.target.closest('[data-sf-theme-cycle]');
    if (!b) return;
    applyMode(modes[(modes.indexOf(getMode()) + 1) % modes.length]);
  });
  function activeNav() {
    const current = (location.pathname.replace(/\/+$/, '') || '/').toLowerCase();
    const links = Array.from(document.querySelectorAll('.side-nav a[href], .side-bottom a[href]'));
    links.forEach(function (a) {
      const url = new URL(a.href, location.origin);
      const path = (url.pathname.replace(/\/+$/, '') || '/').toLowerCase();
      const active = path === current || (current === '/' && path === '/home');
      a.classList.toggle('is-active', active);
      if (active) a.setAttribute('aria-current', 'page');
      else a.removeAttribute('aria-current');
    });
    if (current === '/' && links.length) {
      links[0].classList.add('is-active');
      links[0].setAttribute('aria-current', 'page');
    }
  }
  function init() { applyMode(getMode()); activeNav(); }
  if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', init);
  else init();
})();
