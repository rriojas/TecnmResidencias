(() => {
  'use strict';

  const USER_KEY = 'authUser';
  const STORAGE_KEY = 'authToken';

  const userStr = sessionStorage.getItem(USER_KEY);
  let user = {};
  try {
    user = userStr ? JSON.parse(userStr) : {};
  } catch {
    user = {};
  }

  const roleLower = (user.role || '').toString().toLowerCase();

  const welcomeTitle = document.getElementById('welcomeTitle');
  const welcomeDescription = document.getElementById('welcomeDescription');
  const statsSection = document.getElementById('statsSection');
  const contentCard = document.getElementById('contentCard');
  const actionCards = document.getElementById('actionCards');
  const roleActions = document.getElementById('roleActions');
  const sidePanel = document.getElementById('sidePanel');

  const TOTAL_WEEKS = 26;

  const CAREERS = {
    1: 'Ing. Informática',
    2: 'Ing. Industrial',
    3: 'Ing. Mecatrónica',
    4: 'Ing. en Sistemas Computacionales'
  };

  // Iconos (heroicons outline) para KPIs y Action Cards
  const ICON_PATHS = {
    users: 'M15 19.128a9.38 9.38 0 0 0 2.625.372 9.337 9.337 0 0 0 4.121-.952 4.125 4.125 0 0 0-7.533-2.493M15 19.128v-.003c0-1.113-.285-2.16-.786-3.07M15 19.128v.106A12.318 12.318 0 0 1 8.624 21c-2.331 0-4.512-.645-6.374-1.766l-.001-.109a6.375 6.375 0 0 1 11.964-3.07M12 6.375a3.375 3.375 0 1 1-6.75 0 3.375 3.375 0 0 1 6.75 0Zm8.25 2.25a2.625 2.625 0 1 1-5.25 0 2.625 2.625 0 0 1 5.25 0Z',
    userGroup: 'M18 18.72a9.094 9.094 0 0 0 3.741-.479 3 3 0 0 0-4.682-2.72m.94 3.198.001.031c0 .225-.012.447-.037.666A11.944 11.944 0 0 1 12 21c-2.17 0-4.207-.576-5.963-1.584A6.062 6.062 0 0 1 6 18.719m12 0a5.971 5.971 0 0 0-.941-3.197m0 0A5.995 5.995 0 0 0 12 12.75a5.995 5.995 0 0 0-5.058 2.772m0 0a3 3 0 0 0-4.681 2.72 8.986 8.986 0 0 0 3.74.477m.94-3.197a5.971 5.971 0 0 0-.94 3.197M15 6.75a3 3 0 1 1-6 0 3 3 0 0 1 6 0Zm6 3a2.25 2.25 0 1 1-4.5 0 2.25 2.25 0 0 1 4.5 0Zm-13.5 0a2.25 2.25 0 1 1-4.5 0 2.25 2.25 0 0 1 4.5 0Z',
    document: 'M19.5 14.25v-2.625a3.375 3.375 0 0 0-3.375-3.375h-1.5A1.125 1.125 0 0 1 13.5 7.125v-1.5a3.375 3.375 0 0 0-3.375-3.375H8.25m0 12.75h7.5m-7.5 3H12M10.5 2.25H5.625c-.621 0-1.125.504-1.125 1.125v17.25c0 .621.504 1.125 1.125 1.125h12.75c.621 0 1.125-.504 1.125-1.125V11.25a9 9 0 0 0-9-9Z',
    checkBadge: 'M9 12.75 11.25 15 15 9.75M21 12c0 1.268-.63 2.39-1.593 3.068a3.745 3.745 0 0 1-1.043 3.296 3.745 3.745 0 0 1-3.296 1.043A3.745 3.745 0 0 1 12 21c-1.268 0-2.39-.63-3.068-1.593a3.746 3.746 0 0 1-3.296-1.043 3.746 3.746 0 0 1-1.043-3.296A3.745 3.745 0 0 1 3 12c0-1.268.63-2.39 1.593-3.068a3.745 3.745 0 0 1 1.043-3.296 3.746 3.746 0 0 1 3.296-1.043A3.746 3.746 0 0 1 12 3c1.268 0 2.39.63 3.068 1.593a3.746 3.746 0 0 1 3.296 1.043 3.746 3.746 0 0 1 1.043 3.296A3.745 3.745 0 0 1 21 12Z',
    checkCircle: 'M9 12.75 11.25 15 15 9.75M21 12a9 9 0 1 1-18 0 9 9 0 0 1 18 0Z',
    clock: 'M12 6v6h4.5m4.5 0a9 9 0 1 1-18 0 9 9 0 0 1 18 0Z',
    calendar: 'M6.75 3v2.25M17.25 3v2.25M3 18.75V7.5a2.25 2.25 0 0 1 2.25-2.25h13.5A2.25 2.25 0 0 1 21 7.5v11.25m-18 0A2.25 2.25 0 0 0 5.25 21h13.5A2.25 2.25 0 0 0 21 18.75m-18 0v-7.5A2.25 2.25 0 0 1 5.25 9h13.5A2.25 2.25 0 0 1 21 11.25v7.5',
    folder: 'M2.25 12.75V12A2.25 2.25 0 0 1 4.5 9.75h15A2.25 2.25 0 0 1 21.75 12v.75m-8.69-6.44-2.12-2.12a1.5 1.5 0 0 0-1.061-.44H4.5A2.25 2.25 0 0 0 2.25 6v12a2.25 2.25 0 0 0 2.25 2.25h15A2.25 2.25 0 0 0 21.75 18V9a2.25 2.25 0 0 0-2.25-2.25h-5.379a1.5 1.5 0 0 1-1.06-.44Z',
    star: 'M11.48 3.499a.562.562 0 0 1 1.04 0l2.125 5.111a.563.563 0 0 0 .475.345l5.518.442c.499.04.701.663.321.988l-4.204 3.602a.563.563 0 0 0-.182.557l1.285 5.385a.562.562 0 0 1-.84.61l-4.725-2.885a.562.562 0 0 0-.586 0L6.982 20.54a.562.562 0 0 1-.84-.61l1.285-5.386a.562.562 0 0 0-.182-.557l-4.204-3.602a.562.562 0 0 1 .321-.988l5.518-.442a.563.563 0 0 0 .475-.345L11.48 3.5Z',
    book: 'M12 6.042A8.967 8.967 0 0 0 6 3.75c-1.052 0-2.062.18-3 .512v14.25A8.987 8.987 0 0 1 6 18c2.305 0 4.408.867 6 2.292m0-14.25a8.966 8.966 0 0 1 6-2.292c1.052 0 2.062.18 3 .512v14.25A8.987 8.987 0 0 0 18 18a8.967 8.967 0 0 0-6 2.292m0-14.25v14.25',
    clipboard: 'M9 12h3.75M9 15h3.75M9 18h3.75m3 .75H18a2.25 2.25 0 0 0 2.25-2.25V6.108c0-1.135-.845-2.098-1.976-2.192a48.424 48.424 0 0 0-1.123-.08m-5.801 0c-.065.21-.1.433-.1.664 0 .414.336.75.75.75h4.5a.75.75 0 0 0 .75-.75 2.25 2.25 0 0 0-.1-.664m-5.8 0A2.251 2.251 0 0 1 13.5 2.25H15c1.012 0 1.867.668 2.15 1.586m-5.8 0c-.376.023-.75.05-1.124.08C9.095 4.01 8.25 4.973 8.25 6.108V8.25m0 0H4.875c-.621 0-1.125.504-1.125 1.125v11.25c0 .621.504 1.125 1.125 1.125h9.75c.621 0 1.125-.504 1.125-1.125V9.375c0-.621-.504-1.125-1.125-1.125H8.25ZM6.75 12h.008v.008H12v-.008ZM6.75 15h.008v.008H12V15Zm0 3h.008v.008H12V18Z',
    chart: 'M3 13.125C3 12.504 3.504 12 4.125 12h2.25c.621 0 1.125.504 1.125 1.125v6.75C7.5 20.496 6.996 21 6.375 21h-2.25A1.125 1.125 0 0 1 3 19.875v-6.75ZM9.75 8.625c0-.621.504-1.125 1.125-1.125h2.25c.621 0 1.125.504 1.125 1.125v11.25c0 .621.504 1.125 1.125 1.125h-2.25a1.125 1.125 0 0 1-1.125-1.125V8.625ZM16.5 4.125c0-.621.504-1.125 1.125-1.125h2.25C20.496 3 21 4.125 21 4.125v15.75c0 .621-.504 1.125-1.125 1.125h-2.25a1.125 1.125 0 0 1-1.125-1.125V4.125Z',
    pencil: 'm16.862 4.487 1.687-1.688a1.875 1.875 0 1 1 2.652 2.652L10.582 16.07a4.5 4.5 0 0 1-1.897 1.13L6 18l.8-2.685a4.5 4.5 0 0 1 1.13-1.897l8.932-8.931Zm0 0L19.5 7.125M18 14v4.75A2.25 2.25 0 0 1 15.75 21H5.25A2.25 2.25 0 0 1 3 18.75V8.25A2.25 2.25 0 0 1 5.25 6H10'
  };

  function svgIcon(name) {
    const d = ICON_PATHS[name] || ICON_PATHS.document;
    return `<svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="1.5" stroke="currentColor" aria-hidden="true"><path stroke-linecap="round" stroke-linejoin="round" d="${d}" /></svg>`;
  }

  function getHeaders() {
    const headers = { 'Content-Type': 'application/json' };
    const token = sessionStorage.getItem(STORAGE_KEY);
    if (token) headers['Authorization'] = `Bearer ${token}`;
    return headers;
  }

  async function getJson(url) {
    const res = await fetch(url, { headers: getHeaders() });
    if (!res.ok) throw new Error(`HTTP ${res.status}`);
    return res.json();
  }

  function escapeHtml(text) {
    if (!text) return '';
    return String(text)
      .replace(/&/g, '&amp;')
      .replace(/</g, '&lt;')
      .replace(/>/g, '&gt;')
      .replace(/"/g, '&quot;')
      .replace(/'/g, '&#039;');
  }

  function statusBadge(status) {
    const s = (status || '').toLowerCase();
    if (s === 'draft' || s === 'borrador') {
      return '<span class="tecnm-badge tecnm-badge-neutral">Borrador</span>';
    }
    if (s === 'approved' || s === 'aprobado') {
      return '<span class="tecnm-badge tecnm-badge-approved">Aprobado</span>';
    }
    if (s === 'in_progress' || s === 'inprogress' || s === 'en_progreso') {
      return '<span class="tecnm-badge tecnm-badge-approved">En Progreso</span>';
    }
    if (s === 'completed' || s === 'completado') {
      return '<span class="tecnm-badge tecnm-badge-approved">Completado</span>';
    }
    if (s === 'rejected' || s === 'rechazado') {
      return '<span class="tecnm-badge tecnm-badge-rejected">Correcciones</span>';
    }
    if (s === 'cancelled' || s === 'cancelado') {
      return '<span class="tecnm-badge tecnm-badge-rejected">Cancelado</span>';
    }
    return '<span class="tecnm-badge tecnm-badge-pending">En Revisión</span>';
  }

  function isPending(status) {
    const s = (status || '').toLowerCase();
    return s === 'pending' || s === 'under_review' || s === 'underreview' || s === 'pendiente' || s === 'en revisión' || s === 'en revisio';
  }

  function fmtDate(iso) {
    return window.formatTecNMDate ? window.formatTecNMDate(iso) : iso;
  }

  function kpiCard(title, valueHtml, iconName, tone) {
    const toneClass = tone ? ` kpi-card--${tone}` : '';
    return `
      <div class="kpi-card${toneClass}">
        <span class="kpi-icon">${svgIcon(iconName)}</span>
        <span class="kpi-body">
          <span class="kpi-label">${escapeHtml(title)}</span>
          <span class="kpi-value">${valueHtml}</span>
        </span>
      </div>
    `;
  }

  function actionCard(href, title, sub, iconName) {
    return `
      <a href="${href}" class="action-card">
        <span class="action-card-icon">${svgIcon(iconName)}</span>
        <span class="action-card-body">
          <span class="action-card-title">${escapeHtml(title)}</span>
          <span class="action-card-sub">${escapeHtml(sub)}</span>
        </span>
      </a>
    `;
  }

  // ========================================================
  // ADMIN / DEPARTMENT HEAD
  // ========================================================
  async function loadAdminDashboard(isDeptHead) {
    if (isDeptHead) {
      welcomeTitle.textContent = 'Panel de la División Académica';
      welcomeDescription.textContent = 'Revisión de anteproyectos, dictámenes y avance general de residencias.';
    } else {
      welcomeTitle.textContent = 'Panel de Administración General';
      welcomeDescription.textContent = 'Gestión institucional de alumnos, asesores, anteproyectos y reportes de residencia.';
    }

    statsSection.innerHTML =
      kpiCard('Estudiantes Registrados', '<span id="statTotalStudents">...</span>', 'users') +
      kpiCard('Asesores Activos', '<span id="statActiveAdvisors">...</span>', 'userGroup') +
      kpiCard('Proyectos Registrados', '<span id="statTotalProjects">...</span>', 'document') +
      kpiCard('Proyectos Aprobados', '<span id="statApprovedProjects">...</span>', 'checkBadge', 'green') +
      kpiCard('Por Dictaminar', '<span id="statPendingProjects">...</span>', 'clock', 'gold') +
      kpiCard('Residencias Completadas', '<span id="statCompletedResidencies">...</span>', 'checkCircle', 'green');
    statsSection.dataset.kpiCount = '6';

    contentCard.innerHTML = `
      <div class="tecnm-card-header">
        <h3 class="tecnm-card-title">Anteproyectos Recientes</h3>
      </div>
      <div class="tecnm-card-body">
        <div class="tecnm-table-responsive">
          <table class="tecnm-table">
            <thead>
              <tr>
                <th>Título del Proyecto</th>
                <th>Estudiante</th>
                <th>Fecha</th>
                <th>Estado</th>
              </tr>
            </thead>
            <tbody id="recentProjectsBody">
              <tr><td colspan="4" class="tecnm-table-empty">Cargando anteproyectos...</td></tr>
            </tbody>
          </table>
        </div>
      </div>
    `;

    actionCards.innerHTML = '';
    if (roleActions) roleActions.hidden = true;

    sidePanel.innerHTML = `
      <div class="panel-card">
        <h3 class="panel-title">Cola de Dictamen</h3>
        <ul id="dictamenQueue" class="list-panel">
          <li class="list-panel-empty">Cargando anteproyectos pendientes...</li>
        </ul>
        <div class="tecnm-d-flex tecnm-flex-wrap tecnm-gap-2 tecnm-mt-2">
          <a href="/projects/review" class="tecnm-btn tecnm-btn-outline tecnm-btn-sm">Ir al Dictamen</a>
          <a href="/admin/reports" class="tecnm-btn tecnm-btn-outline tecnm-btn-sm">Reportes y Liberación</a>
        </div>
      </div>
    `;

    try {
      const metrics = await getJson('/api/v1/admin/dashboard');
      document.getElementById('statTotalStudents').textContent = metrics.totalStudents ?? 0;
      document.getElementById('statActiveAdvisors').textContent = metrics.activeAdvisors ?? 0;
      document.getElementById('statTotalProjects').textContent = metrics.totalProjects ?? 0;
      document.getElementById('statApprovedProjects').textContent = metrics.approvedProjects ?? 0;
      document.getElementById('statPendingProjects').textContent = metrics.pendingProjects ?? 0;
      document.getElementById('statCompletedResidencies').textContent = metrics.completedResidencies ?? 0;
    } catch (err) {
      console.error('Error al cargar métricas del dashboard:', err);
    }

    try {
      const recentData = await getJson('/api/v1/projects?pageNumber=1&pageSize=5');
      const recent = (Array.isArray(recentData.items) ? recentData.items : []).filter(p => p.isActive !== false);

      const pendingData = await getJson('/api/v1/projects?status=pending&pageNumber=1&pageSize=5');
      const allPending = Array.isArray(pendingData.items) ? pendingData.items : [];
      const pendingQueue = allPending.slice(0, 5);
      const remainingCount = Math.max(0, (pendingData.totalCount || allPending.length) - 5);

      let queueHtml = pendingQueue.length
        ? pendingQueue.map(p => `
            <li class="list-panel-item">
              <div>
                <div class="list-panel-item-title">${escapeHtml(p.title)}</div>
                <div class="list-panel-item-sub">${escapeHtml(p.studentName || 'Estudiante')}</div>
              </div>
            </li>
          `).join('')
        : '<li class="list-panel-empty">Sin anteproyectos por dictaminar. ¡Al día!</li>';

      if (remainingCount > 0) {
        queueHtml += `
          <li class="list-panel-more">
            + ${remainingCount} dictámen${remainingCount > 1 ? 'es' : ''} más por revisar
          </li>
        `;
      }

      document.getElementById('recentProjectsBody').innerHTML = recent.length
        ? recent.map(p => `
            <tr>
              <td>${escapeHtml(p.title)}</td>
              <td>${escapeHtml(p.studentName || 'Estudiante')}</td>
              <td>${fmtDate(p.createdAt)}</td>
              <td>${statusBadge(p.status)}</td>
            </tr>
          `).join('')
        : '<tr><td colspan="4" class="tecnm-table-empty">No hay anteproyectos registrados.</td></tr>';

      document.getElementById('dictamenQueue').innerHTML = queueHtml;
    } catch (err) {
      console.error('Error al cargar anteproyectos del dashboard:', err);
    }
  }

  // ========================================================
  // STUDENT
  // ========================================================
  async function loadStudentDashboard() {
    welcomeTitle.textContent = 'Portal del Estudiante Residente';
    welcomeDescription.textContent = 'Seguimiento de tu anteproyecto, avance semanal y expediente digital.';

    statsSection.innerHTML =
      kpiCard('Anteproyectos', '<span id="stProposals">...</span>', 'document') +
      kpiCard('Semanas Completadas', '<span id="stWeeks">...</span>', 'calendar') +
      kpiCard('Documentos Aprobados', '<span id="stDocs">...</span>', 'folder', 'green') +
      kpiCard('Promedio General', '<span id="stGpa">...</span>', 'star', 'gold');
    statsSection.dataset.kpiCount = '4';

    contentCard.innerHTML = `
      <div class="tecnm-card-header">
        <h3 class="tecnm-card-title">Mi Residencia</h3>
      </div>
      <div class="tecnm-card-body tecnm-card-body--loose">
        <div id="studentIdentity" class="tecnm-text-secondary">Cargando perfil...</div>
        <div id="studentProjectInfo" class="tecnm-mt-3"></div>
        <div class="tecnm-form-group tecnm-mt-3">
          <span class="tecnm-label">Avance Semanal</span>
          <div class="progress-track"><div id="progressFill" class="progress-fill"></div></div>
          <span id="progressLabel" class="progress-label">0% del avance semanal registrado</span>
        </div>
      </div>
    `;

    if (roleActions) roleActions.hidden = false;
    actionCards.innerHTML =
      actionCard('/projects/proposal', 'Anteproyecto', 'Registrar o dar seguimiento', 'pencil') +
      actionCard('/activities/schedule', 'Cronograma', 'Avance de tus 26 semanas', 'calendar') +
      actionCard('/documents', 'Expediente Digital', 'Documentos y evidencias', 'folder');

    sidePanel.innerHTML = `
      <div class="panel-card panel-card--loose">
        <h3 class="panel-title">Tareas Pendientes</h3>
        <ul id="studentTasks" class="list-panel"></ul>
      </div>
    `;

    let student = null;
    try {
      student = await getJson('/api/v1/students/me');
    } catch {
      student = null;
    }

    if (!student) {
      document.getElementById('studentIdentity').innerHTML =
        '<div class="tecnm-alert tecnm-alert-warning">No se encontró un perfil de estudiante asociado a tu cuenta. Contacta a tu división académica.</div>';
      document.getElementById('stProposals').textContent = '—';
      document.getElementById('stWeeks').textContent = '—';
      document.getElementById('stDocs').textContent = '—';
      document.getElementById('stGpa').textContent = '—';
      document.getElementById('studentTasks').innerHTML = '<li class="list-panel-empty">Sin tareas disponibles.</li>';
      return;
    }

    document.getElementById('studentIdentity').innerHTML = `
      <div class="tecnm-profile-grid">
        <div>
          <span class="tecnm-field-label">Nombre</span>
          <span class="tecnm-field-value tecnm-field-value-emphasis">${escapeHtml(student.firstName)} ${escapeHtml(student.lastName)}</span>
        </div>
        <div>
          <span class="tecnm-field-label">No. Control</span>
          <span class="tecnm-field-value">${escapeHtml(student.controlNumber)}</span>
        </div>
        <div>
          <span class="tecnm-field-label">Carrera</span>
          <span class="tecnm-field-value">${escapeHtml(CAREERS[student.careerId] || '—')}</span>
        </div>
      </div>
    `;

    let projects = [];
    try {
      const res = await getJson('/api/v1/projects/me?pageNumber=1&pageSize=10');
      projects = Array.isArray(res.items) ? res.items : [];
    } catch {
      projects = [];
    }

    document.getElementById('stProposals').textContent = projects.length;

    const latest = projects.reduce((best, p) => {
      return !best || new Date(p.createdAt) > new Date(best.createdAt) ? p : best;
    }, null);

    let docs = [];
    let activities = [];
    if (latest) {
      try {
        const docsRes = await getJson(`/api/v1/documents/project/${latest.id}?pageNumber=1&pageSize=50`);
        docs = Array.isArray(docsRes.items) ? docsRes.items : [];
      } catch { docs = []; }
      try { activities = await getJson(`/api/v1/projects/${latest.id}/activities`); } catch { activities = []; }
    }

    const completedWeeks = new Set();
    (Array.isArray(activities) ? activities : []).forEach(act => {
      (act.progresses || []).forEach(pr => {
        if (String(pr.status).toLowerCase() === 'completed') completedWeeks.add(pr.weekNumber);
      });
    });

    const pct = Math.min(100, Math.round((completedWeeks.size / TOTAL_WEEKS) * 100));
    document.getElementById('stWeeks').textContent = `${completedWeeks.size} / ${TOTAL_WEEKS}`;
    document.getElementById('progressFill').style.width = `${pct}%`;
    document.getElementById('progressLabel').textContent = `${pct}% del avance semanal registrado`;

    const docsArr = Array.isArray(docs) ? docs : [];
    const approvedDocs = docsArr.filter(d => String(d.status).toLowerCase() === 'approved').length;
    document.getElementById('stDocs').textContent = approvedDocs;
    document.getElementById('stGpa').textContent = student.gpa ? Number(student.gpa).toFixed(1) : '—';

    const tasks = [];

    if (latest) {
      document.getElementById('studentProjectInfo').innerHTML = `
        <div class="tecnm-d-flex tecnm-justify-between tecnm-align-center tecnm-flex-wrap tecnm-gap-2">
          <div>
            <span class="tecnm-field-label">Anteproyecto</span>
            <span class="tecnm-field-value tecnm-field-value-emphasis">${escapeHtml(latest.title)}</span>
          </div>
          ${statusBadge(latest.status)}
        </div>
      `;
    } else {
      document.getElementById('studentProjectInfo').innerHTML =
        '<div class="tecnm-alert tecnm-alert-warning">Aún no has registrado tu anteproyecto de residencia.</div>';
      tasks.push({ text: 'Registrar tu anteproyecto', href: '/projects/proposal' });
    }

    const docsByType = {};
    docsArr.forEach(d => { docsByType[d.documentType] = d; });

    if (!docsByType['solicitud']) tasks.push({ text: 'Subir tu Solicitud de Residencia', href: '/documents' });
    if (!docsByType['carta_aceptacion']) tasks.push({ text: 'Subir tu Carta de Aceptación', href: '/documents' });
    if (latest && isPending(latest.status)) tasks.push({ text: 'Tu anteproyecto está en revisión', href: '/projects/proposal' });
    if (completedWeeks.size === 0 && activities.length > 0) tasks.push({ text: 'Registrar avance en tu cronograma', href: '/activities/schedule' });

    document.getElementById('studentTasks').innerHTML = tasks.length
      ? tasks.map(t => `
          <li class="list-panel-item">
            <a class="list-panel-link" href="${t.href}">${escapeHtml(t.text)}</a>
          </li>
        `).join('')
      : '<li class="list-panel-empty">Sin tareas pendientes. ¡Vas al día!</li>';
  }

  // ========================================================
  // ADVISOR
  // ========================================================
  async function loadAdvisorDashboard() {
    welcomeTitle.textContent = 'Portal de Asesoría de Residencias';
    welcomeDescription.textContent = 'Seguimiento de los residentes a tu cargo, dictámenes pendientes y evaluaciones.';

    statsSection.innerHTML =
      kpiCard('Residentes Asignados', '<span id="adResidents">...</span>', 'users') +
      kpiCard('Anteproyectos por Revisar', '<span id="adPending">...</span>', 'clock', 'gold') +
      kpiCard('Proyectos Aprobados', '<span id="adApproved">...</span>', 'checkBadge', 'green');
    statsSection.dataset.kpiCount = '3';

    contentCard.innerHTML = `
      <div class="tecnm-card-header">
        <h3 class="tecnm-card-title">Proyectos Asignados</h3>
      </div>
      <div class="tecnm-card-body">
        <div class="tecnm-table-responsive">
          <table class="tecnm-table">
            <thead>
              <tr>
                <th>Título del Proyecto</th>
                <th>Estudiante</th>
                <th>Estado</th>
              </tr>
            </thead>
            <tbody id="assignedProjectsBody">
              <tr><td colspan="3" class="tecnm-table-empty">Cargando proyectos asignados...</td></tr>
            </tbody>
          </table>
        </div>
      </div>
    `;

    if (roleActions) roleActions.hidden = false;
    actionCards.innerHTML =
      actionCard('/projects/review', 'Dictamen de División', 'Revisar anteproyectos asignados', 'clipboard') +
      actionCard('/evaluations/grading', 'Evaluar Reportes', 'Calificar reportes parciales', 'star') +
      actionCard('/evaluations', 'Bitácora de Asesorías', 'Registrar sesiones de asesoría', 'book') +
      actionCard('/activities/schedule', 'Cronograma', 'Avance semanal de residentes', 'calendar');

    sidePanel.innerHTML = `
      <div class="panel-card">
        <h3 class="panel-title">Perfil</h3>
        <div class="tecnm-field-value tecnm-field-value-emphasis" id="adName">Cargando...</div>
        <div class="tecnm-field-value" id="adTitle"></div>
      </div>
      <div class="panel-card">
        <h3 class="panel-title">Pendientes por Revisar</h3>
        <ul id="advisorQueue" class="list-panel">
          <li class="list-panel-empty">Cargando...</li>
        </ul>
        <a href="/projects/review" class="tecnm-btn tecnm-btn-outline tecnm-btn-sm tecnm-mt-2">Ir al Dictamen</a>
      </div>
    `;

    let advisor = null;
    try {
      advisor = await getJson('/api/v1/advisors/me');
    } catch {
      advisor = null;
    }

    if (advisor) {
      document.getElementById('adName').textContent = advisor.fullName;
      document.getElementById('adTitle').textContent = advisor.title || 'Asesor de residencias';
    } else {
      document.getElementById('adName').textContent = 'Perfil no vinculado';
    }

    try {
      const projectsData = await getJson('/api/v1/projects/advisor/me?pageNumber=1&pageSize=50');
      const assigned = Array.isArray(projectsData.items) ? projectsData.items : [];
      const pending = assigned.filter(p => isPending(p.status));
      const approved = assigned.filter(p => String(p.status).toLowerCase() === 'approved');

      document.getElementById('adResidents').textContent = assigned.length;
      document.getElementById('adPending').textContent = pending.length;
      document.getElementById('adApproved').textContent = approved.length;

      document.getElementById('assignedProjectsBody').innerHTML = assigned.length
        ? assigned.map(p => `
            <tr>
              <td>${escapeHtml(p.title)}</td>
              <td>${escapeHtml(p.studentName || 'Estudiante')}</td>
              <td>${statusBadge(p.status)}</td>
            </tr>
          `).join('')
        : '<tr><td colspan="3" class="tecnm-table-empty">Aún no tienes proyectos asignados.</td></tr>';

      const allPendingAdvisor = assigned.filter(p => isPending(p.status));
      const pendingAdvisorQueue = allPendingAdvisor.slice(0, 5);
      const remainingAdvisorCount = allPendingAdvisor.length > 5 ? allPendingAdvisor.length - 5 : 0;

      let advisorQueueHtml = pendingAdvisorQueue.length
        ? pendingAdvisorQueue.map(p => `
            <li class="list-panel-item">
              <div>
                <div class="list-panel-item-title">${escapeHtml(p.title)}</div>
                <div class="list-panel-item-sub">${escapeHtml(p.studentName || 'Estudiante')}</div>
              </div>
            </li>
          `).join('')
        : '<li class="list-panel-empty">Sin anteproyectos por revisar.</li>';

      if (remainingAdvisorCount > 0) {
        advisorQueueHtml += `
          <li class="list-panel-more">
            + ${remainingAdvisorCount} dictámen${remainingAdvisorCount > 1 ? 'es' : ''} más por revisar
          </li>
        `;
      }

      document.getElementById('advisorQueue').innerHTML = advisorQueueHtml;
    } catch (err) {
      console.error('Error al cargar proyectos del asesor:', err);
    }
  }

  // ========================================================
  // Dispatch por rol
  // ========================================================
  if (roleLower === 'admin') {
    loadAdminDashboard(false);
  } else if (roleLower === 'departmenthead' || roleLower === 'department_head') {
    loadAdminDashboard(true);
  } else if (roleLower === 'student') {
    loadStudentDashboard();
  } else if (roleLower === 'advisor') {
    loadAdvisorDashboard();
  } else {
    loadAdminDashboard(false);
  }
})();