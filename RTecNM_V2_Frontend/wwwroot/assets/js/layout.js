(() => {
  'use strict';

  const STORAGE_KEY = 'authToken';
  const USER_KEY = 'authUser';

  const token = sessionStorage.getItem(STORAGE_KEY);
  const userStr = sessionStorage.getItem(USER_KEY);

  if (!token || !userStr) {
    window.location.href = '/auth/login';
    return;
  }

  let user = {};
  try {
    user = JSON.parse(userStr);
  } catch {
    sessionStorage.clear();
    window.location.href = '/auth/login';
    return;
  }

  const currentRole = (user.role || '').toLowerCase();

  // ---------- Global fetch wrapper: inyecta el token en llamadas a /api/ ----------
  (() => {
    const originalFetch = window.fetch.bind(window);
    window.fetch = function (url, options) {
      options = options || {};
      if (typeof url === 'string' && url.indexOf('/api/') !== -1) {
        const headers = new Headers(options.headers || {});
        if (!headers.has('Authorization') && token) {
          headers.set('Authorization', `Bearer ${token}`);
        }
        options.headers = headers;
      }
      return originalFetch(url, options).then(response => {
        if (response.status === 401 && typeof url === 'string' && url.indexOf('/api/') !== -1 && url.indexOf('/api/v1/auth/') === -1) {
          sessionStorage.clear();
          window.location.href = '/auth/login';
        }
        return response;
      });
    };
  })();

  const userPermissions = Array.isArray(user.permissions) ? user.permissions : [];
  const userIsAdmin = user.isAdmin === true || user.is_admin === true || currentRole === 'admin';

  window.getUserPermissions = () => userPermissions;
  window.isSuperAdmin = () => userIsAdmin;

  window.hasPermission = (permission) => {
    if (!permission) return true;
    if (userIsAdmin) return true;
    return userPermissions.some(p => p === permission || permission.startsWith(p + '.') || p.startsWith(permission + '.'));
  };

  // ---------- Role-based helpers (available to all page scripts) ----------
  window.getCurrentRole = () => currentRole;
  window.isReadOnlyUser = () => currentRole === 'director';
  window.hasRole = (...roles) => {
    const normCurrent = (currentRole || '').replace('_', '');
    return roles.some(r => (r || '').replace('_', '') === normCurrent);
  };
  window.canSeeAudit = () => !window.isReadOnlyUser() && (userIsAdmin || window.hasPermission('admin.reports') || window.hasRole('admin', 'departmenthead'));
  window.canManageRegistry = () => !window.isReadOnlyUser() && (userIsAdmin || window.hasPermission('projects.delete') || window.hasPermission('students.manage') || window.hasRole('admin', 'departmenthead'));
  window.canGrade = () => !window.isReadOnlyUser() && (userIsAdmin || window.hasPermission('evaluations.grading') || window.hasRole('admin', 'advisor'));
  window.canCreateProposal = () => !window.isReadOnlyUser() && (userIsAdmin || currentRole === 'student');

  // ---------- Custom Modal Confirmation (Spec 08 UI Compliance) ----------
  window.tecnmConfirm = (message, title = 'Confirmación') => {
    return new Promise((resolve) => {
      let modal = document.getElementById('tecnmConfirmModal');
      if (!modal) {
        document.body.insertAdjacentHTML('beforeend', `
          <div class="modal-backdrop" id="tecnmConfirmModal" aria-hidden="true">
            <div class="modal-card">
              <div class="tecnm-modal-header">
                <h3 class="tecnm-modal-title" id="tecnmConfirmTitle">Confirmación</h3>
                <button type="button" class="tecnm-modal-close" id="tecnmConfirmClose" aria-label="Cerrar">&times;</button>
              </div>
              <div class="tecnm-card-body" style="padding: 1rem 0;">
                <p id="tecnmConfirmMessage" style="margin:0; color: var(--tecnm-text-primary); font-size: 1rem; line-height: 1.5;"></p>
              </div>
              <div class="tecnm-modal-footer">
                <button type="button" class="tecnm-btn tecnm-btn-secondary" id="tecnmConfirmCancel">Cancelar</button>
                <button type="button" class="tecnm-btn tecnm-btn-primary" id="tecnmConfirmOk">Aceptar</button>
              </div>
            </div>
          </div>
        `);
        modal = document.getElementById('tecnmConfirmModal');
      }

      const titleEl = document.getElementById('tecnmConfirmTitle');
      const msgEl = document.getElementById('tecnmConfirmMessage');
      const okBtn = document.getElementById('tecnmConfirmOk');
      const cancelBtn = document.getElementById('tecnmConfirmCancel');
      const closeBtn = document.getElementById('tecnmConfirmClose');

      if (titleEl) titleEl.textContent = title;
      if (msgEl) msgEl.textContent = message;

      const cleanup = (result) => {
        modal.classList.remove('active');
        modal.setAttribute('aria-hidden', 'true');
        okBtn.onclick = null;
        cancelBtn.onclick = null;
        closeBtn.onclick = null;
        resolve(result);
      };

      okBtn.onclick = () => cleanup(true);
      cancelBtn.onclick = () => cleanup(false);
      closeBtn.onclick = () => cleanup(false);

      modal.classList.add('active');
      modal.setAttribute('aria-hidden', 'false');
    });
  };

  const MONTH_NAMES_ES = ['Enero', 'Febrero', 'Marzo', 'Abril', 'Mayo', 'Junio', 'Julio', 'Agosto', 'Septiembre', 'Octubre', 'Noviembre', 'Diciembre'];

  window.formatTecNMDate = (iso) => {
    if (!iso) return '—';
    const d = new Date(iso);
    if (isNaN(d.getTime())) return '—';
    const day = String(d.getDate()).padStart(2, '0');
    const monthName = MONTH_NAMES_ES[d.getMonth()];
    const year = d.getFullYear();
    return `${day}/${monthName}/${year}`;
  };

  window.formatAuditDate = (iso) => window.formatTecNMDate(iso);

  // ---------- Nombres de usuarios para el modal de auditoría ----------
  window.auditUserNames = {};

  window.collectAuditUserIds = (items) => {
    const ids = new Set();
    (items || []).forEach(item => {
      [item.createdBy, item.updatedBy, item.deletedBy].forEach(v => {
        if (v) ids.add(v);
      });
    });
    return Array.from(ids);
  };

  window.loadAuditUserNames = async (ids) => {
    const missing = (ids || []).filter(id => !(id in window.auditUserNames));
    if (!missing.length) return;

    try {
      const res = await fetch(`/api/v1/auth/users/names?ids=${missing.join(',')}`);
      if (!res.ok) return;
      const names = await res.json();
      Object.entries(names || {}).forEach(([id, name]) => {
        window.auditUserNames[id] = name;
      });
    } catch {
      // Si falla, el modal mostrará "Usuario #id" como respaldo.
    }
  };

  window.formatAuditUser = (id) => {
    if (!id) return 'Sistema (semilla)';

    const value = window.auditUserNames[id];
    if (!value) return `Usuario #${id}`;

    if (value.includes('@')) {
      const local = value.split('@')[0] || '';
      const words = local.split(/[._\-\d]+/).filter(Boolean);
      return words.length
        ? words.map(w => w.charAt(0).toUpperCase() + w.slice(1)).join(' ')
        : local;
    }

    return value;
  };

  // ---------- Modal de Auditoría (campos del registro, solo roles autorizados) ----------
  window.showAuditModal = (title, rows) => {
    const escapeValue = (value) => String(value)
      .replace(/&/g, '&amp;')
      .replace(/</g, '&lt;')
      .replace(/>/g, '&gt;')
      .replace(/"/g, '&quot;')
      .replace(/'/g, '&#039;');

    const safeRows = (rows || []).map(r => ({
      label: escapeValue(r.label || ''),
      value: escapeValue(r.value === null || r.value === undefined || r.value === '' ? '—' : r.value)
    }));

    let modal = document.getElementById('auditModal');
    if (!modal) {
      document.body.insertAdjacentHTML('beforeend', `
        <div class="modal-backdrop" id="auditModal" aria-hidden="true">
          <div class="modal-card">
            <div class="tecnm-modal-header">
              <h3 class="tecnm-modal-title" id="auditModalTitle">Auditoría del Registro</h3>
              <button type="button" class="tecnm-modal-close" id="auditModalClose" aria-label="Cerrar">&times;</button>
            </div>
            <dl class="tecnm-audit-list" id="auditModalList"></dl>
            <div class="tecnm-modal-footer">
              <button type="button" class="tecnm-btn tecnm-btn-secondary" id="auditModalOk">Cerrar</button>
            </div>
          </div>
        </div>
      `);
      modal = document.getElementById('auditModal');
    }

    const titleEl = document.getElementById('auditModalTitle');
    const listEl = document.getElementById('auditModalList');
    const okBtn = document.getElementById('auditModalOk');
    const closeBtn = document.getElementById('auditModalClose');

    if (titleEl) titleEl.textContent = title || 'Auditoría del Registro';
    if (listEl) {
      listEl.innerHTML = safeRows.map(r => `
        <div class="tecnm-audit-row">
          <dt>${r.label}</dt>
          <dd>${r.value}</dd>
        </div>
      `).join('');
    }

    const cleanup = () => {
      modal.classList.remove('active');
      modal.setAttribute('aria-hidden', 'true');
      okBtn.onclick = null;
      closeBtn.onclick = null;
    };

    okBtn.onclick = cleanup;
    closeBtn.onclick = cleanup;

    modal.classList.add('active');
    modal.setAttribute('aria-hidden', 'false');
  };

  // ---------- Paginación compartida (meta = PaginatedResult del API) ----------
  function buildPageList(current, total) {
    const delta = 1;
    const pages = [];
    const range = Array.from(new Set([1, total, current - delta, current, current + delta]))
      .filter(p => p >= 1 && p <= total)
      .sort((a, b) => a - b);

    let prev = 0;
    for (const p of range) {
      if (prev && p - prev > 1) pages.push('...');
      pages.push(p);
      prev = p;
    }
    return pages;
  }

  window.renderPagination = (container, meta, onPage) => {
    if (!container || !meta || !meta.totalCount) {
      if (container) container.innerHTML = '';
      return;
    }

    const totalPages = meta.totalPages;
    const current = meta.pageNumber;
    const start = (current - 1) * meta.pageSize + 1;
    const end = Math.min(current * meta.pageSize, meta.totalCount);
    const prevDisabled = !meta.hasPreviousPage;
    const nextDisabled = !meta.hasNextPage;
    const pageList = buildPageList(current, totalPages);

    container.innerHTML = `
      <div class="tecnm-pagination">
        <span class="tecnm-pagination-info">Mostrando ${start}–${end} de ${meta.totalCount} registro(s)</span>
        <div class="tecnm-pagination-pages">
          <button type="button" class="tecnm-pagination-btn" data-page="${current - 1}" ${prevDisabled ? 'disabled' : ''} aria-label="Página anterior">&laquo;</button>
          ${pageList.map(p => p === '...'
            ? `<span class="tecnm-pagination-ellipsis">…</span>`
            : `<button type="button" class="tecnm-pagination-btn ${p === current ? 'active' : ''}" data-page="${p}" ${p === current ? 'aria-current="page"' : ''}>${p}</button>`
          ).join('')}
          <button type="button" class="tecnm-pagination-btn" data-page="${current + 1}" ${nextDisabled ? 'disabled' : ''} aria-label="Página siguiente">&raquo;</button>
        </div>
      </div>
    `;

    container.querySelectorAll('button[data-page]').forEach(btn => {
      btn.addEventListener('click', () => {
        const page = parseInt(btn.dataset.page, 10);
        if (!isNaN(page) && page !== current && onPage) onPage(page);
      });
    });
  };

  // ---------- Controles de tabla compartidos: búsqueda, ordenamiento y exportación PDF ----------
  window.bindTableSearch = (inputId, onSearch, delay = 300) => {
    const input = document.getElementById(inputId);
    if (!input) return;
    let timer = null;
    input.addEventListener('input', () => {
      clearTimeout(timer);
      timer = setTimeout(() => { if (onSearch) onSearch(input.value.trim()); }, delay);
    });
  };

  window.initSortableHeaders = (tableId, onSort) => {
    const table = document.getElementById(tableId);
    if (!table) return;
    table.querySelectorAll('th[data-sort]').forEach(th => {
      th.classList.add('tecnm-sort-th');
      th.addEventListener('click', () => {
        const field = th.dataset.sort;
        const newDir = th.dataset.dir === 'desc' ? 'asc' : 'desc';
        th.dataset.dir = newDir;
        table.querySelectorAll('th[data-sort]').forEach(other => {
          other.classList.remove('tecnm-sort-asc', 'tecnm-sort-desc');
        });
        th.classList.add(newDir === 'asc' ? 'tecnm-sort-asc' : 'tecnm-sort-desc');
        if (onSort) onSort(field, newDir);
      });
    });
  };

  window.getSortState = (th) => ({ field: th.dataset.sort || '', dir: th.dataset.dir || 'asc' });

  window.downloadPdf = async (url, filename) => {
    try {
      const res = await fetch(url);
      if (!res.ok) {
        let message = 'Error al generar el PDF.';
        try {
          const errData = await res.json();
          if (errData && errData.message) message = errData.message;
          else if (errData && errData.title) message = errData.title;
        } catch {
          const text = await res.text().catch(() => '');
          if (text && text.length < 300) message = text;
        }
        if (typeof showAlert === 'function') showAlert(message, 'danger');
        else alert(message);
        return;
      }
      const blob = await res.blob();
      const objectUrl = URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = objectUrl;
      a.download = filename || 'reporte_tecnm.pdf';
      document.body.appendChild(a);
      a.click();
      a.remove();
      URL.revokeObjectURL(objectUrl);
    } catch {
      const message = 'Error de conexión al exportar el PDF.';
      if (typeof showAlert === 'function') showAlert(message, 'danger');
      else alert(message);
    }
  };

  // ---------- Navbar & DOM: show only items allowed for the current role / permissions ----------
  document.querySelectorAll('[data-permission]').forEach(item => {
    const requiredPerm = (item.dataset.permission || '').trim();
    if (requiredPerm && !window.hasPermission(requiredPerm)) item.remove();
  });

  document.querySelectorAll('[data-roles]').forEach(item => {
    const allowed = (item.dataset.roles || '').split(',').map(r => r.trim());
    if (!window.hasRole(...allowed) && !userIsAdmin) item.remove();
  });

  // ---------- Route guard: redirect to dashboard if the page is not allowed ----------
  const PERMISSION_GUARDS = {
    '/students': 'students.manage',
    '/advisors': 'advisors.manage',
    '/advisors/assign': 'projects.advisor.assign',
    '/projects/review': 'projects.review',
    '/evaluations/grading': 'evaluations.grading',
    '/admin/reports': 'admin.reports'
  };

  const ROLE_GUARDS = {
    '/students': ['admin', 'departmenthead'],
    '/advisors': ['admin', 'departmenthead'],
    '/advisors/assign': ['admin', 'departmenthead'],
    '/projects/review': ['admin', 'vinculacion', 'departmenthead', 'advisor'],
    '/evaluations/grading': ['admin', 'departmenthead', 'advisor'],
    '/admin/reports': ['admin', 'departmenthead']
  };

  const path = location.pathname.replace(/\/+$/, '');
  for (const [prefix, perm] of Object.entries(PERMISSION_GUARDS)) {
    if (path === prefix || path.startsWith(prefix + '/')) {
      const allowedRoles = ROLE_GUARDS[prefix] || [];
      if (!window.hasPermission(perm) && !window.hasRole(...allowedRoles)) {
        window.location.href = '/dashboard';
      }
      break;
    }
  }

  // ---------- Navbar: hamburger toggle (accessible) ----------
  const navToggle = document.getElementById('navToggle');
  const navList = document.getElementById('navbarNav');

  if (navToggle && navList) {
    navToggle.addEventListener('click', () => {
      const isOpen = navList.classList.toggle('open');
      navToggle.setAttribute('aria-expanded', String(isOpen));
      navToggle.setAttribute('aria-label', isOpen ? 'Cerrar menú' : 'Abrir menú');
    });
  }

  // ---------- Nav groups: acordeón / mega-menú por categorías ----------
  const NAV_ICONS = {
    home: 'M2.25 12 12 2.25 21.75 12M4.5 9.75v10.5a.75.75 0 0 0 .75.75h4.5a.75.75 0 0 0 .75-.75v-6a.75.75 0 0 1 .75-.75h3a.75.75 0 0 1 .75.75v6a.75.75 0 0 0 .75.75h4.5a.75.75 0 0 0 .75-.75V9.75M3.75 9.75 12 3l8.25 6.75',
    users: 'M15 19.128a9.38 9.38 0 0 0 2.625.372 9.337 9.337 0 0 0 4.121-.952 4.125 4.125 0 0 0-7.533-2.493M15 19.128v-.003c0-1.113-.285-2.16-.786-3.07M15 19.128v.106A12.318 12.318 0 0 1 8.624 21c-2.331 0-4.512-.645-6.374-1.766l-.001-.109a6.375 6.375 0 0 1 11.964-3.07M12 6.375a3.375 3.375 0 1 1-6.75 0 3.375 3.375 0 0 1 6.75 0Zm8.25 2.25a2.625 2.625 0 1 1-5.25 0 2.625 2.625 0 0 1 5.25 0Z',
    'user-group': 'M18 18.72a9.094 9.094 0 0 0 3.741-.479 3 3 0 0 0-4.682-2.72m.94 3.198.001.031c0 .225-.012.447-.037.666A11.944 11.944 0 0 1 12 21c-2.17 0-4.207-.576-5.963-1.584A6.062 6.062 0 0 1 6 18.719m12 0a5.971 5.971 0 0 0-.941-3.197m0 0A5.995 5.995 0 0 0 12 12.75a5.995 5.995 0 0 0-5.058 2.772m0 0a3 3 0 0 0-4.681 2.72 8.986 8.986 0 0 0 3.74.477m.94-3.197a5.971 5.971 0 0 0-.94 3.197M15 6.75a3 3 0 1 1-6 0 3 3 0 0 1 6 0Zm6 3a2.25 2.25 0 1 1-4.5 0 2.25 2.25 0 0 1 4.5 0Zm-13.5 0a2.25 2.25 0 1 1-4.5 0 2.25 2.25 0 0 1 4.5 0Z',
    document: 'M19.5 14.25v-2.625a3.375 3.375 0 0 0-3.375-3.375h-1.5A1.125 1.125 0 0 1 13.5 7.125v-1.5a3.375 3.375 0 0 0-3.375-3.375H8.25m0 12.75h7.5m-7.5 3H12M10.5 2.25H5.625c-.621 0-1.125.504-1.125 1.125v17.25c0 .621.504 1.125 1.125 1.125h12.75c.621 0 1.125-.504 1.125-1.125V11.25a9 9 0 0 0-9-9Z',
    pencil: 'm16.862 4.487 1.687-1.688a1.875 1.875 0 1 1 2.652 2.652L10.582 16.07a4.5 4.5 0 0 1-1.897 1.13L6 18l.8-2.685a4.5 4.5 0 0 1 1.13-1.897l8.932-8.931Zm0 0L19.5 7.125M18 14v4.75A2.25 2.25 0 0 1 15.75 21H5.25A2.25 2.25 0 0 1 3 18.75V8.25A2.25 2.25 0 0 1 5.25 6H10',
    clipboard: 'M9 12h3.75M9 15h3.75M9 18h3.75m3 .75H18a2.25 2.25 0 0 0 2.25-2.25V6.108c0-1.135-.845-2.098-1.976-2.192a48.424 48.424 0 0 0-1.123-.08m-5.801 0c-.065.21-.1.433-.1.664 0 .414.336.75.75.75h4.5a.75.75 0 0 0 .75-.75 2.25 2.25 0 0 0-.1-.664m-5.8 0A2.251 2.251 0 0 1 13.5 2.25H15c1.012 0 1.867.668 2.15 1.586m-5.8 0c-.376.023-.75.05-1.124.08C9.095 4.01 8.25 4.973 8.25 6.108V8.25m0 0H4.875c-.621 0-1.125.504-1.125 1.125v11.25c0 .621.504 1.125 1.125 1.125h9.75c.621 0 1.125-.504 1.125-1.125V9.375c0-.621-.504-1.125-1.125-1.125H8.25Z',
    calendar: 'M6.75 3v2.25M17.25 3v2.25M3 18.75V7.5a2.25 2.25 0 0 1 2.25-2.25h13.5A2.25 2.25 0 0 1 21 7.5v11.25m-18 0A2.25 2.25 0 0 0 5.25 21h13.5A2.25 2.25 0 0 0 21 18.75m-18 0v-7.5A2.25 2.25 0 0 1 5.25 9h13.5A2.25 2.25 0 0 1 21 11.25v7.5',
    book: 'M12 6.042A8.967 8.967 0 0 0 6 3.75c-1.052 0-2.062.18-3 .512v14.25A8.987 8.987 0 0 1 6 18c2.305 0 4.408.867 6 2.292m0-14.25a8.966 8.966 0 0 1 6-2.292c1.052 0 2.062.18 3 .512v14.25A8.987 8.987 0 0 0 18 18a8.967 8.967 0 0 0-6 2.292m0-14.25v14.25',
    star: 'M11.48 3.499a.562.562 0 0 1 1.04 0l2.125 5.111a.563.563 0 0 0 .475.345l5.518.442c.499.04.701.663.321.988l-4.204 3.602a.563.563 0 0 0-.182.557l1.285 5.385a.562.562 0 0 1-.84.61l-4.725-2.885a.562.562 0 0 0-.586 0L6.982 20.54a.562.562 0 0 1-.84-.61l1.285-5.386a.562.562 0 0 0-.182-.557l-4.204-3.602a.562.562 0 0 1 .321-.988l5.518-.442a.563.563 0 0 0 .475-.345L11.48 3.5Z',
    folder: 'M2.25 12.75V12A2.25 2.25 0 0 1 4.5 9.75h15A2.25 2.25 0 0 1 21.75 12v.75m-8.69-6.44-2.12-2.12a1.5 1.5 0 0 0-1.061-.44H4.5A2.25 2.25 0 0 0 2.25 6v12a2.25 2.25 0 0 0 2.25 2.25h15A2.25 2.25 0 0 0 21.75 18V9a2.25 2.25 0 0 0-2.25-2.25h-5.379a1.5 1.5 0 0 1-1.06-.44Z',
    chart: 'M3 13.125C3 12.504 3.504 12 4.125 12h2.25c.621 0 1.125.504 1.125 1.125v6.75C7.5 20.496 6.996 21 6.375 21h-2.25A1.125 1.125 0 0 1 3 19.875v-6.75ZM9.75 8.625c0-.621.504-1.125 1.125-1.125h2.25c.621 0 1.125.504 1.125 1.125v11.25c0 .621-.504 1.125-1.125 1.125h-2.25a1.125 1.125 0 0 1-1.125-1.125V8.625ZM16.5 4.125c0-.621.504-1.125 1.125-1.125h2.25C20.496 3 21 4.125 21 4.125v15.75c0 .621-.504 1.125-1.125 1.125h-2.25a1.125 1.125 0 0 1-1.125-1.125V4.125Z',
    chevron: 'M19.5 8.25 12 15.75 4.5 8.25'
  };

  function navSvg(name) {
    const d = NAV_ICONS[name] || NAV_ICONS.document;
    return `<svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="1.5" stroke="currentColor" aria-hidden="true"><path stroke-linecap="round" stroke-linejoin="round" d="${d}" /></svg>`;
  }

  document.querySelectorAll('[data-nav-group-icon]').forEach(el => {
    el.innerHTML = navSvg(el.dataset.navGroupIcon);
  });
  document.querySelectorAll('.tecnm-nav-group-chevron').forEach(el => {
    el.innerHTML = navSvg('chevron');
  });
  document.querySelectorAll('[data-nav-icon]').forEach(el => {
    el.insertAdjacentHTML('afterbegin', `<span class="tecnm-nav-item-icon" aria-hidden="true">${navSvg(el.dataset.navIcon)}</span>`);
  });

  // Ocultar categorías cuyo sublist quedó vacío tras el filtrado por rol
  document.querySelectorAll('.tecnm-nav-group').forEach(group => {
    const sublist = group.querySelector('.tecnm-nav-sublist');
    if (sublist && !sublist.querySelector('li')) group.remove();
  });

  const navGroups = Array.from(document.querySelectorAll('.tecnm-nav-group'));

  function closeAllNavGroups(except) {
    navGroups.forEach(g => {
      if (g === except) return;
      g.classList.remove('open');
      const b = g.querySelector('.tecnm-nav-group-btn');
      if (b) b.setAttribute('aria-expanded', 'false');
    });
  }

  navGroups.forEach(group => {
    const btn = group.querySelector('.tecnm-nav-group-btn');
    if (!btn) return;
    btn.addEventListener('click', (e) => {
      e.stopPropagation();
      const willOpen = !group.classList.contains('open');
      if (willOpen && window.matchMedia('(min-width: 1024px)').matches) {
        closeAllNavGroups(group);
      }
      group.classList.toggle('open', willOpen);
      btn.setAttribute('aria-expanded', String(willOpen));
    });
  });

  // Cerrar dropdowns al hacer click fuera o al presionar Escape
  document.addEventListener('click', (e) => {
    if (!e.target.closest('.tecnm-nav-group')) closeAllNavGroups(null);
  });
  document.addEventListener('keydown', (e) => {
    if (e.key === 'Escape') closeAllNavGroups(null);
  });

  // Auto-resaltar la categoría que contiene el item de la ruta actual
  // (sin abrir el dropdown; se abre sólo por interacción del usuario)
  const activeNavItem = document.querySelector('.tecnm-nav-item.active');
  if (activeNavItem) {
    const group = activeNavItem.closest('.tecnm-nav-group');
    if (group) {
      group.classList.add('is-active');
    }
  }

  // ---------- Audit fields: visible only to Admin / Department Head ----------
  document.querySelectorAll('.audit-col').forEach(el => {
    el.style.display = window.canSeeAudit() ? '' : 'none';
  });

  // ---------- User profile menu (avatar, real name, role label, dropdown) ----------
  const ROLE_LABELS = {
    admin: 'Administrador',
    departmenthead: 'Jefe de División',
    department_head: 'Jefe de División',
    advisor: 'Asesor',
    student: 'Estudiante'
  };

  function prettifyEmailLocal(local) {
    const words = (local || '').split(/[._\-\d]+/).filter(Boolean);
    if (words.length === 0) return local || 'Usuario';
    return words.map(w => w.charAt(0).toUpperCase() + w.slice(1)).join(' ');
  }

  function initialsFromEmail(email) {
    const local = (email || '').split('@')[0] || 'U';
    const words = local.split(/[._\-\d]+/).filter(Boolean);
    if (words.length >= 2) return (words[0][0] + words[1][0]).toUpperCase();
    if (words.length === 1) return (words[0][0] + (words[0][1] || '')).toUpperCase();
    return 'U';
  }

  const avatarEl = document.getElementById('userAvatar');
  const displayNameEl = document.getElementById('userDisplayName');
  const roleEl = document.getElementById('userRoleDisplay');

  if (avatarEl) avatarEl.textContent = initialsFromEmail(user.email);
  if (roleEl) roleEl.textContent = ROLE_LABELS[currentRole] || currentRole;
  if (displayNameEl) displayNameEl.textContent = prettifyEmailLocal((user.email || '').split('@')[0]);

  (async () => {
    try {
      if (currentRole === 'student') {
        const res = await fetch('/api/v1/students/me', { headers: { Authorization: `Bearer ${token}` } });
        if (!res.ok) return;
        const profile = await res.json();
        if (profile && displayNameEl) {
          displayNameEl.textContent = `${profile.firstName} ${profile.lastName}`.trim();
        }
      } else if (currentRole === 'advisor') {
        const res = await fetch('/api/v1/advisors/me', { headers: { Authorization: `Bearer ${token}` } });
        if (!res.ok) return;
        const profile = await res.json();
        if (profile && displayNameEl) {
          displayNameEl.textContent = profile.fullName || displayNameEl.textContent;
        }
      }
    } catch {
      // Keep the email-derived fallback name.
    }
  })();

  const logoutBtn = document.getElementById('logoutBtn');
  if (logoutBtn) {
    logoutBtn.addEventListener('click', () => {
      sessionStorage.clear();
      window.location.href = '/auth/login';
    });
  }
})();
