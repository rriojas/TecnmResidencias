const API_EVAL_BASE = '/api/v1/evaluations';

// -------------------------------------------------------------
// Advisory Sessions Page Logic
// -------------------------------------------------------------
function getCurrentUserRole() {
  try {
    const userStr = sessionStorage.getItem('authUser');
    if (!userStr) return '';
    const user = JSON.parse(userStr);
    return (user.role || '').toLowerCase();
  } catch {
    return '';
  }
}

let selectedAdvisoryProjectId = null;
let selectedGradingProjectId = null;

function selectProjectForSessions(project) {
  if (!project || !project.id) return;
  selectedAdvisoryProjectId = project.id;

  const title = project.title || project.titleText || project.name || 'Anteproyecto';
  const student = project.studentName ? ` (Alumno: ${escapeHtml(project.studentName)}${project.studentControlNumber ? ' - ' + escapeHtml(project.studentControlNumber) : ''})` : '';

  const badge = document.getElementById('selectedProjectBadge');
  if (badge) {
    badge.innerHTML = `#${project.id} — ${escapeHtml(title)}${student}`;
  }

  const modalBadge = document.getElementById('modalSelectedProjectBadge');
  if (modalBadge) {
    modalBadge.innerHTML = `#${project.id} — ${escapeHtml(title)}${student}`;
  }

  const modalProjectId = document.getElementById('modalProjectId');
  if (modalProjectId) {
    modalProjectId.value = selectedAdvisoryProjectId;
  }

  sessionsPageNumber = 1;
  loadAdvisorySessions();
}

function selectProjectForGrading(project) {
  if (!project || !project.id) return;
  selectedGradingProjectId = project.id;

  const badge = document.getElementById('selectedProjectBadge');
  if (badge) {
    const title = project.title || project.titleText || project.name || 'Anteproyecto';
    const student = project.studentName ? ` (Alumno: ${escapeHtml(project.studentName)}${project.studentControlNumber ? ' - ' + escapeHtml(project.studentControlNumber) : ''})` : '';
    badge.innerHTML = `#${project.id} — ${escapeHtml(title)}${student}`;
  }

  const modalGradeProjectId = document.getElementById('modalGradeProjectId');
  if (modalGradeProjectId) {
    modalGradeProjectId.value = selectedGradingProjectId;
  }

  evaluationsPageNumber = 1;
  loadEvaluations();
}

function initAdvisorySessionsPage() {
  const dateInput = document.getElementById('sessionDate');
  if (dateInput && !dateInput.value) {
    dateInput.value = new Date().toISOString().split('T')[0];
  }

  const role = getCurrentUserRole();
  const openModalBtn = document.getElementById('openAdvisoryModalBtn');
  if (openModalBtn) {
    const isReadOnly = window.isReadOnlyUser ? window.isReadOnlyUser() : false;
    const canRecord = !isReadOnly && window.hasPermission && (window.hasPermission('advisories.session.record') || window.hasRole('admin', 'departmenthead', 'advisor'));
    if (role === 'student' || isReadOnly || !canRecord) {
      openModalBtn.classList.add('tecnm-hidden');
    } else {
      const modal = document.getElementById('createAdvisoryModal');
      if (modal) {
        openModalBtn.addEventListener('click', () => modal.classList.add('active'));
      }
    }
  }

  const modal = document.getElementById('createAdvisoryModal');
  const closeModalBtn = document.getElementById('closeAdvisoryModalBtn');
  const cancelModalBtn = document.getElementById('cancelAdvisoryModalBtn');

  const hideModal = () => {
    if (modal) modal.classList.remove('active');
  };
  if (closeModalBtn) closeModalBtn.addEventListener('click', hideModal);
  if (cancelModalBtn) cancelModalBtn.addEventListener('click', hideModal);

  const isStaff = role === 'admin' || role === 'departmenthead';

  const exportSessionsBtn = document.getElementById('exportSessionsBtn');
  if (exportSessionsBtn && !isStaff) exportSessionsBtn.classList.add('tecnm-hidden');

  if (window.bindTableSearch) {
    window.bindTableSearch('sessionsSearchInput', (term) => {
      sessionsSearch = term;
      sessionsPageNumber = 1;
      loadAdvisorySessions();
    });
  }

  if (window.initSortableHeaders) {
    window.initSortableHeaders('sessionsTable', (field, dir) => {
      sessionsSortBy = field;
      sessionsSortDir = dir;
      sessionsPageNumber = 1;
      loadAdvisorySessions();
    });
  }

  const inactiveToggle = document.getElementById('sessionsIncludeInactiveToggle');
  if (inactiveToggle) {
    inactiveToggle.addEventListener('change', () => {
      sessionsIncludeInactive = inactiveToggle.checked;
      sessionsPageNumber = 1;
      loadAdvisorySessions();
    });
  }

  if (exportSessionsBtn && window.downloadPdf) {
    exportSessionsBtn.addEventListener('click', () => {
      const params = new URLSearchParams({
        search: sessionsSearch,
        sortBy: sessionsSortBy,
        sortDir: sessionsSortDir,
        includeInactive: sessionsIncludeInactive
      });
      if (selectedAdvisoryProjectId) params.set('projectId', selectedAdvisoryProjectId);
      window.downloadPdf(`${API_EVAL_BASE}/sessions/export?${params}`, 'asesorias_tecnm.pdf');
    });
  }

  const isStudent = window.hasRole && window.hasRole('student') && !window.hasRole('admin', 'departmenthead', 'advisor');
  const isAdvisor = window.hasRole && window.hasRole('advisor') && !window.hasRole('admin', 'departmenthead');
  const searchBtn = document.getElementById('searchProjectBtn');

  if (isStudent) {
    if (searchBtn) searchBtn.style.display = 'none';
    resolveCurrentStudentProjectForSessions();
  } else {
    if (searchBtn) {
      searchBtn.style.display = 'inline-flex';
      searchBtn.addEventListener('click', () => {
        if (window.openGlobalSearch) {
          window.openGlobalSearch({
            initialSource: 'PROJECTS',
            onSelect: (item) => {
              if (item && item.id) {
                selectProjectForSessions(item);
              }
            }
          });
        }
      });
    }
    loadInitialProjectForSessions(isAdvisor);
  }

  const advisorSelect = document.getElementById('advisorId');
  if (advisorSelect) {
    const group = advisorSelect.closest('.tecnm-form-group');
    if (isAdvisor) {
      if (group) group.classList.add('tecnm-hidden');
      advisorSelect.removeAttribute('required');
    } else {
      if (group) group.classList.remove('tecnm-hidden');
      advisorSelect.setAttribute('required', 'required');
      loadAdvisorsDropdown('advisorId');
    }
  }

  const selectProjModalBtn = document.getElementById('selectProjectForAdvisoryBtn');
  if (selectProjModalBtn) {
    selectProjModalBtn.addEventListener('click', () => {
      if (window.openGlobalSearch) {
        window.openGlobalSearch({
          initialSource: 'PROJECTS',
          onSelect: (item) => {
            if (item && item.id) {
              selectProjectForSessions(item);
            }
          }
        });
      }
    });
  }

  const form = document.getElementById('advisoryForm');
  if (form) {
    form.addEventListener('submit', handleAdvisorySubmit);
  }
}

async function resolveCurrentStudentProjectForSessions() {
  try {
    const res = await fetch('/api/v1/projects/me/current', { headers: getAuthHeaders() });
    if (!res.ok) throw new Error();
    const project = await res.json();
    if (project && project.id) {
      populateModalProjectOptions('modalProjectId', [project]);
      selectProjectForSessions(project);
    } else {
      loadAdvisorySessions();
    }
  } catch {
    loadAdvisorySessions();
  }
}

async function loadInitialProjectForSessions(isAdvisor) {
  const badge = document.getElementById('selectedProjectBadge');
  const tbody = document.getElementById('sessionsTableBody');

  try {
    const endpoint = isAdvisor ? '/api/v1/projects/advisor/me?pageSize=50' : '/api/v1/projects?pageSize=50';
    const res = await fetch(endpoint, { headers: getAuthHeaders() });
    if (!res.ok) throw new Error();

    const rawData = await res.json();
    let projects = Array.isArray(rawData) ? rawData : (rawData && Array.isArray(rawData.items) ? rawData.items : []);
    projects = projects.filter(p => (p.status || '').toLowerCase() !== 'draft');

    if (projects.length === 0) {
      if (badge) badge.innerText = 'Sin anteproyectos asignados';
      if (tbody) tbody.innerHTML = `<tr><td colspan="6" class="tecnm-table-empty">No se encontraron anteproyectos asignados. Utilice el botón "Buscar Anteproyecto" para seleccionar uno.</td></tr>`;
      selectedAdvisoryProjectId = null;
      return;
    }

    populateModalProjectOptions('modalProjectId', projects);
    selectProjectForSessions(projects[0]);
  } catch {
    if (badge) badge.innerText = 'Seleccione un anteproyecto';
    if (tbody) tbody.innerHTML = `<tr><td colspan="6" class="tecnm-table-empty">Haga clic en "Buscar Anteproyecto" para cargar la bitácora.</td></tr>`;
    selectedAdvisoryProjectId = null;
  }
}

function populateModalProjectOptions(elementId, projects) {
  const el = document.getElementById(elementId);
  if (!el || el.tagName !== 'SELECT') return;
  el.innerHTML = projects.map(p => `<option value="${p.id}">#${p.id} - ${escapeHtml(p.title)}</option>`).join('');
}

function getAuthHeaders() {
  const headers = { 'Content-Type': 'application/json' };
  const token = sessionStorage.getItem('authToken');
  if (token) headers['Authorization'] = `Bearer ${token}`;
  return headers;
}

async function loadProjectsDropdown(...selectIds) {
  try {
    const res = await fetch('/api/v1/projects/options', { headers: getAuthHeaders() });
    if (!res.ok) throw new Error();
    const projects = await res.json();

    const role = getCurrentUserRole();
    const isStaff = role === 'admin' || role === 'departmenthead';

    selectIds.forEach(id => {
      const el = document.getElementById(id);
      if (!el) return;

      if (id === 'projectId' && isStaff) {
        el.innerHTML = `<option value="__all__">Todos los proyectos</option>` +
          (Array.isArray(projects) ? projects : [])
            .map(p => `<option value="${p.id}">#${p.id} - ${escapeHtml(p.title)}</option>`).join('');
      } else if (!Array.isArray(projects) || projects.length === 0) {
        el.innerHTML = `<option value="">-- No hay proyectos registrados --</option>`;
      } else {
        el.innerHTML = projects.map(p => `<option value="${p.id}">#${p.id} - ${escapeHtml(p.title)}</option>`).join('');
      }
    });

    if (document.getElementById('sessionsTableBody')) {
      loadAdvisorySessions();
    }
    if (document.getElementById('evaluationsTableBody')) {
      loadEvaluations();
    }
  } catch {
    selectIds.forEach(id => {
      const el = document.getElementById(id);
      if (el) el.innerHTML = `<option value="">-- No hay proyectos registrados --</option>`;
    });
    if (document.getElementById('sessionsTableBody')) {
      loadAdvisorySessions();
    }
    if (document.getElementById('evaluationsTableBody')) {
      loadEvaluations();
    }
  }
}

async function loadAdvisorsDropdown(...selectIds) {
  try {
    const res = await fetch('/api/v1/advisors/options', { headers: getAuthHeaders() });
    if (!res.ok) throw new Error();
    const advisors = await res.json();

    selectIds.forEach(id => {
      const el = document.getElementById(id);
      if (!el) return;

      if (!Array.isArray(advisors) || advisors.length === 0) {
        el.innerHTML = `<option value="">-- No hay asesores registrados --</option>`;
      } else {
        el.innerHTML = advisors.map(a => `<option value="${a.id}">#${a.id} - ${escapeHtml(a.fullName)}</option>`).join('');
      }
    });
  } catch {
    selectIds.forEach(id => {
      const el = document.getElementById(id);
      if (el) el.innerHTML = `<option value="">-- No hay asesores registrados --</option>`;
    });
  }
}

let sessionsPageNumber = 1;
let sessionsSearch = '';
let sessionsSortBy = 'SessionDate';
let sessionsSortDir = 'desc';
let sessionsIncludeInactive = false;
let sessionsCache = [];
let evaluationsCache = [];

window.openSessionAuditModal = (id) => {
  const s = sessionsCache.find(item => item.id === id);
  if (!s || !window.showAuditModal) return;

  window.showAuditModal(`Auditoría — Asesoría #${s.id}`, [
    { label: 'ID', value: s.id },
    { label: 'Proyecto', value: s.projectTitle || `#${s.projectId}` },
    { label: 'Estado', value: s.isActive ? 'Activo' : 'Inactivo' },
    { label: 'Visible', value: s.isVisible ? 'Sí' : 'No' },
    { label: 'Orden', value: s.displayOrder },
    { label: 'Creado el', value: window.formatAuditDate(s.createdAt) },
    { label: 'Creado por', value: window.formatAuditUser(s.createdBy) },
    { label: 'Actualizado el', value: s.updatedBy ? window.formatAuditDate(s.updatedAt) : '—' },
    { label: 'Actualizado por', value: s.updatedBy ? window.formatAuditUser(s.updatedBy) : '—' },
    { label: 'Eliminado el', value: s.deletedAt ? window.formatAuditDate(s.deletedAt) : '—' },
    { label: 'Eliminado por', value: s.deletedBy ? window.formatAuditUser(s.deletedBy) : '—' }
  ]);
};

window.openEvaluationAuditModal = (id) => {
  const e = evaluationsCache.find(item => item.id === id);
  if (!e || !window.showAuditModal) return;

  window.showAuditModal(`Auditoría — Evaluación #${e.id}`, [
    { label: 'ID', value: e.id },
    { label: 'Proyecto', value: e.projectTitle || `#${e.projectId}` },
    { label: 'Estado', value: e.isActive ? 'Activo' : 'Inactivo' },
    { label: 'Visible', value: e.isVisible ? 'Sí' : 'No' },
    { label: 'Orden', value: e.displayOrder },
    { label: 'Creado el', value: window.formatAuditDate(e.createdAt) },
    { label: 'Creado por', value: window.formatAuditUser(e.createdBy) },
    { label: 'Actualizado el', value: e.updatedBy ? window.formatAuditDate(e.updatedAt) : '—' },
    { label: 'Actualizado por', value: e.updatedBy ? window.formatAuditUser(e.updatedBy) : '—' },
    { label: 'Eliminado el', value: e.deletedAt ? window.formatAuditDate(e.deletedAt) : '—' },
    { label: 'Eliminado por', value: e.deletedBy ? window.formatAuditUser(e.deletedBy) : '—' }
  ]);
};

async function loadAdvisorySessions() {
  const tbody = document.getElementById('sessionsTableBody');
  const paginationContainer = document.getElementById('sessionsPagination');
  const projectId = selectedAdvisoryProjectId;
  if (!tbody) return;

  if (!projectId) {
    tbody.innerHTML = `<tr><td colspan="6" class="tecnm-table-empty">Haga clic en "Buscar Anteproyecto" para consultar la bitácora de asesorías.</td></tr>`;
    if (paginationContainer) paginationContainer.innerHTML = '';
    return;
  }

  tbody.innerHTML = `<tr><td colspan="6" class="tecnm-table-empty">Cargando sesiones de asesoría...</td></tr>`;

  const isAllProjects = projectId === '__all__';
  const endpoint = isAllProjects
    ? `${API_EVAL_BASE}/sessions`
    : `${API_EVAL_BASE}/sessions/project/${projectId}`;

  try {
    const params = new URLSearchParams({
      pageNumber: sessionsPageNumber,
      pageSize: 10,
      search: sessionsSearch,
      sortBy: sessionsSortBy,
      sortDir: sessionsSortDir,
      includeInactive: sessionsIncludeInactive
    });
    const res = await fetch(`${endpoint}?${params}`, { headers: getAuthHeaders() });
    if (!res.ok) {
      const errData = await res.json().catch(() => ({}));
      throw new Error(errData.message || errData.detail || errData.title || 'Error al obtener la bitácora de asesorías.');
    }

    const data = await res.json();
    const sessions = (data && data.items) || [];
    sessionsCache = sessions;

    if (window.canSeeAudit && window.canSeeAudit()) {
      await window.loadAuditUserNames(window.collectAuditUserIds(sessions));
    }

    if (sessions.length === 0 && sessionsPageNumber > 1 && data.totalPages > 0) {
      sessionsPageNumber = data.totalPages;
      return loadAdvisorySessions();
    }

    if (sessionsIncludeInactive) {
      const hasInactive = sessions.some(s => s.isActive === false || s.is_active === false);
      if (!hasInactive) {
        showAlert('No existen registros inactivos.', 'info');
        const inactiveToggle = document.getElementById('sessionsIncludeInactiveToggle');
        if (inactiveToggle) inactiveToggle.checked = false;
        sessionsIncludeInactive = false;
      }
    }

    const canSeeAudit = window.canSeeAudit ? window.canSeeAudit() : false;

    if (sessions.length === 0) {
      const emptyMessage = isAllProjects
        ? 'No hay sesiones de asesoría registradas en la base de datos.'
        : 'No hay sesiones de asesoría registradas en la base de datos para este proyecto.';
      tbody.innerHTML = `<tr><td colspan="6" class="tecnm-table-empty">${emptyMessage}</td></tr>`;
    } else {
      tbody.innerHTML = sessions.map(s => `
        <tr>
          <td>${window.formatTecNMDate(s.sessionDate)}</td>
          <td>${escapeHtml(s.studentName || `Estudiante #${s.projectId}`)}</td>
          <td>${escapeHtml(s.advisorName || `Asesor #${s.advisorId}`)}</td>
          <td>${escapeHtml(s.topicsCovered)}</td>
          <td>${escapeHtml(s.studentAgreements || 'N/A')}</td>
          <td>${canSeeAudit ? `<button class="tecnm-btn tecnm-btn-secondary tecnm-btn-sm" onclick="openSessionAuditModal(${s.id})">Auditoría</button>` : '—'}</td>
        </tr>
      `).join('');
    }

    if (window.renderPagination) {
      window.renderPagination(paginationContainer, data, (page) => {
        sessionsPageNumber = page;
        loadAdvisorySessions();
      });
    }
  } catch (err) {
    tbody.innerHTML = `<tr><td colspan="6" class="tecnm-table-empty tecnm-text-danger">${escapeHtml(err.message || 'Error al obtener la bitácora de asesorías.')}</td></tr>`;
    if (paginationContainer) paginationContainer.innerHTML = '';
  }
}

async function handleAdvisorySubmit(e) {
  e.preventDefault();
  const projectIdSelect = document.getElementById('modalProjectId') || document.getElementById('projectId');
  const projectIdVal = projectIdSelect?.value;
  const advisorIdVal = document.getElementById('advisorId')?.value;
  const isStaff = window.hasRole && window.hasRole('admin', 'departmenthead');

  if (!projectIdVal) {
    showAlert('Debe seleccionar un proyecto.', 'warning');
    return;
  }
  if (isStaff && !advisorIdVal) {
    showAlert('Debe registrar al menos un asesor en el sistema.', 'warning');
    return;
  }

  const projectId = parseInt(projectIdVal, 10);
  const advisorId = advisorIdVal ? parseInt(advisorIdVal, 10) : 0;
  const sessionDate = document.getElementById('sessionDate').value;
  const topicsCovered = document.getElementById('topicsCovered').value.trim();
  const studentAgreements = document.getElementById('studentAgreements').value.trim();

  try {
    const res = await fetch(`${API_EVAL_BASE}/sessions`, {
      method: 'POST',
      headers: getAuthHeaders(),
      body: JSON.stringify({
        projectId,
        advisorId,
        sessionDate: sessionDate ? new Date(sessionDate).toISOString() : null,
        topicsCovered,
        studentAgreements
      })
    });

    if (res.ok) {
      showAlert('¡Sesión de asesoría registrada correctamente en PostgreSQL!', 'success');
      const modal = document.getElementById('createAdvisoryModal');
      if (modal) modal.classList.remove('active');
      document.getElementById('topicsCovered').value = '';
      document.getElementById('studentAgreements').value = '';
      loadAdvisorySessions();
    } else {
      const err = await res.json();
      showAlert(err.message || 'Error al guardar la asesoría.', 'danger');
    }
  } catch (err) {
    showAlert('Error de conexión con el servidor.', 'danger');
  }
}

// -------------------------------------------------------------
// Grading Page Logic
// -------------------------------------------------------------
function initGradingPage() {
  const isAdvisorRole = window.hasRole && window.hasRole('advisor') && !window.hasRole('admin', 'departmenthead');
  const evaluatorSelect = document.getElementById('evaluatorId');
  if (evaluatorSelect) {
    const group = evaluatorSelect.closest('.tecnm-form-group');
    if (isAdvisorRole) {
      if (group) group.classList.add('tecnm-hidden');
      evaluatorSelect.removeAttribute('required');
    } else {
      if (group) group.classList.remove('tecnm-hidden');
      evaluatorSelect.setAttribute('required', 'required');
      loadAdvisorsDropdown('evaluatorId');
    }
  }

  const openModalBtn = document.getElementById('openGradingModalBtn');
  const modal = document.getElementById('createGradingModal');
  const closeModalBtn = document.getElementById('closeGradingModalBtn');
  const cancelModalBtn = document.getElementById('cancelGradingModalBtn');

  if (openModalBtn && modal) {
    if (window.canGrade && !window.canGrade()) {
      openModalBtn.classList.add('tecnm-hidden');
    } else {
      openModalBtn.addEventListener('click', () => modal.classList.add('active'));
    }
  }
  const hideModal = () => {
    if (modal) modal.classList.remove('active');
  };
  if (closeModalBtn) closeModalBtn.addEventListener('click', hideModal);
  if (cancelModalBtn) cancelModalBtn.addEventListener('click', hideModal);

  const isStudent = window.hasRole && window.hasRole('student') && !window.hasRole('admin', 'departmenthead', 'advisor');
  const isAdvisor = window.hasRole && window.hasRole('advisor') && !window.hasRole('admin', 'departmenthead');
  const searchBtn = document.getElementById('searchProjectBtn');

  if (isStudent) {
    if (searchBtn) searchBtn.style.display = 'none';
    resolveCurrentStudentProjectForGrading();
  } else {
    if (searchBtn) {
      searchBtn.style.display = 'inline-flex';
      searchBtn.addEventListener('click', () => {
        if (window.openGlobalSearch) {
          window.openGlobalSearch({
            initialSource: 'PROJECTS',
            onSelect: (item) => {
              if (item && item.id) {
                selectProjectForGrading(item);
              }
            }
          });
        }
      });
    }
    loadInitialProjectForGrading(isAdvisor);
  }

  const form = document.getElementById('gradingForm');
  if (form) {
    form.addEventListener('submit', handleGradingSubmit);
  }
}

async function resolveCurrentStudentProjectForGrading() {
  try {
    const res = await fetch('/api/v1/projects/me/current', { headers: getAuthHeaders() });
    if (!res.ok) throw new Error();
    const project = await res.json();
    if (project && project.id) {
      populateModalProjectOptions('modalGradeProjectId', [project]);
      selectProjectForGrading(project);
    } else {
      loadEvaluations();
    }
  } catch {
    loadEvaluations();
  }
}

async function loadInitialProjectForGrading(isAdvisor) {
  const badge = document.getElementById('selectedProjectBadge');
  const tbody = document.getElementById('evaluationsTableBody');

  try {
    const endpoint = isAdvisor ? '/api/v1/projects/advisor/me?pageSize=50' : '/api/v1/projects?pageSize=50';
    const res = await fetch(endpoint, { headers: getAuthHeaders() });
    if (!res.ok) throw new Error();

    const rawData = await res.json();
    let projects = Array.isArray(rawData) ? rawData : (rawData && Array.isArray(rawData.items) ? rawData.items : []);
    projects = projects.filter(p => (p.status || '').toLowerCase() !== 'draft');

    if (projects.length === 0) {
      if (badge) badge.innerText = 'Sin anteproyectos asignados';
      if (tbody) tbody.innerHTML = `<tr><td colspan="6" class="tecnm-table-empty">No se encontraron anteproyectos asignados. Utilice el botón "Buscar Anteproyecto" para seleccionar uno.</td></tr>`;
      selectedGradingProjectId = null;
      return;
    }

    populateModalProjectOptions('modalGradeProjectId', projects);
    selectProjectForGrading(projects[0]);
  } catch {
    if (badge) badge.innerText = 'Seleccione un anteproyecto';
    if (tbody) tbody.innerHTML = `<tr><td colspan="6" class="tecnm-table-empty">Haga clic en "Buscar Anteproyecto" para cargar calificaciones.</td></tr>`;
    selectedGradingProjectId = null;
  }
}

let evaluationsPageNumber = 1;

async function loadEvaluations() {
  const tbody = document.getElementById('evaluationsTableBody');
  const paginationContainer = document.getElementById('evaluationsPagination');
  const projectId = selectedGradingProjectId;
  if (!tbody) return;

  if (!projectId) {
    tbody.innerHTML = `<tr><td colspan="6" class="tecnm-table-empty">Haga clic en "Buscar Anteproyecto" para consultar calificaciones.</td></tr>`;
    if (paginationContainer) paginationContainer.innerHTML = '';
    return;
  }

  tbody.innerHTML = `<tr><td colspan="6" class="tecnm-table-empty">Cargando evaluaciones...</td></tr>`;

  try {
    const params = new URLSearchParams({ pageNumber: evaluationsPageNumber, pageSize: 10 });
    const res = await fetch(`${API_EVAL_BASE}/project/${projectId}?${params}`, { headers: getAuthHeaders() });
    if (!res.ok) {
      const errData = await res.json().catch(() => ({}));
      throw new Error(errData.message || errData.detail || errData.title || 'Error al obtener las calificaciones.');
    }

    const data = await res.json();
    const evaluations = (data && data.items) || [];
    evaluationsCache = evaluations;

    if (window.canSeeAudit && window.canSeeAudit()) {
      await window.loadAuditUserNames(window.collectAuditUserIds(evaluations));
    }

    if (evaluations.length === 0 && evaluationsPageNumber > 1 && data.totalPages > 0) {
      evaluationsPageNumber = data.totalPages;
      return loadEvaluations();
    }

    const canSeeAudit = window.canSeeAudit ? window.canSeeAudit() : false;

    if (evaluations.length === 0) {
      tbody.innerHTML = `<tr><td colspan="6" class="tecnm-table-empty">No hay calificaciones registradas en la base de datos para este proyecto.</td></tr>`;
    } else {
      tbody.innerHTML = evaluations.map(e => `
        <tr>
          <td><span class="tecnm-badge tecnm-badge-info">${escapeHtml(formatPeriod(e.evaluationPeriod))}</span></td>
          <td><strong>${e.score}</strong> / 100</td>
          <td>${escapeHtml(e.studentName || `Estudiante #${e.projectId}`)}</td>
          <td>${escapeHtml(e.feedback || 'Sin observaciones')}</td>
          <td>${window.formatTecNMDate(e.createdAt)}</td>
          <td>${canSeeAudit ? `<button class="tecnm-btn tecnm-btn-secondary tecnm-btn-sm" onclick="openEvaluationAuditModal(${e.id})">Auditoría</button>` : '—'}</td>
        </tr>
      `).join('');
    }

    if (window.renderPagination) {
      window.renderPagination(paginationContainer, data, (page) => {
        evaluationsPageNumber = page;
        loadEvaluations();
      });
    }
  } catch (err) {
    tbody.innerHTML = `<tr><td colspan="6" class="tecnm-table-empty tecnm-text-danger">${escapeHtml(err.message || 'Error al consultar la base de datos de calificaciones.')}</td></tr>`;
    if (paginationContainer) paginationContainer.innerHTML = '';
  }
}

function formatPeriod(p) {
  const period = (p || '').toLowerCase();
  if (period === 'partial_1') return 'Primer Reporte Parcial';
  if (period === 'partial_2') return 'Segundo Reporte Parcial';
  if (period === 'final') return 'Reporte Final';
  return p;
}

async function handleGradingSubmit(e) {
  e.preventDefault();
  const projectIdSelect = document.getElementById('modalGradeProjectId') || document.getElementById('gradeProjectId');
  const projectIdVal = projectIdSelect?.value;
  const evaluatorIdVal = document.getElementById('evaluatorId')?.value;
  const isStaff = window.hasRole && window.hasRole('admin', 'departmenthead');

  if (!projectIdVal) {
    showAlert('Debe seleccionar un proyecto.', 'warning');
    return;
  }
  if (isStaff && !evaluatorIdVal) {
    showAlert('Debe haber evaluadores registrados en el sistema.', 'warning');
    return;
  }

  const projectId = parseInt(projectIdVal, 10);
  const evaluatorId = evaluatorIdVal ? parseInt(evaluatorIdVal, 10) : 0;
  const evaluationPeriod = document.getElementById('evaluationPeriod').value;
  const score = parseFloat(document.getElementById('score').value);
  const feedback = document.getElementById('feedback').value.trim();

  if (isNaN(score) || score < 0 || score > 100) {
    showAlert('La calificación debe ser un valor entre 0 y 100.', 'warning');
    return;
  }

  try {
    const res = await fetch(API_EVAL_BASE, {
      method: 'POST',
      headers: getAuthHeaders(),
      body: JSON.stringify({
        projectId,
        evaluatorId,
        evaluationPeriod,
        score,
        feedback
      })
    });

    if (res.ok) {
      showAlert('¡Calificación guardada correctamente en la base de datos!', 'success');
      const modal = document.getElementById('createGradingModal');
      if (modal) modal.classList.remove('active');
      document.getElementById('score').value = '';
      document.getElementById('feedback').value = '';
      loadEvaluations();
    } else {
      const err = await res.json();
      showAlert(err.message || 'Error al guardar la calificación.', 'danger');
    }
  } catch (err) {
    showAlert('Error de conexión con el servidor.', 'danger');
  }
}

window.displaySingleEvaluation = (row) => {
  const clearBtn = document.getElementById('clearSearchFilterBtn');
  if (clearBtn) clearBtn.classList.remove('tecnm-hidden');

  const paginationContainer = document.getElementById('sessionsPagination');
  if (paginationContainer) paginationContainer.innerHTML = '';

  const id = row.id;
  const title = row.title || '-';
  const studentName = row.student_name || row.studentName || '-';

  const tbody = document.getElementById('sessionsTableBody');
  if (tbody) {
    tbody.innerHTML = `
      <tr>
        <td><strong>${escapeHtml(title)}</strong></td>
        <td>${escapeHtml(studentName)}</td>
        <td>—</td>
        <td>—</td>
        <td><span class="tecnm-badge tecnm-badge-approved">Activo</span></td>
        <td class="tecnm-text-center">—</td>
      </tr>
    `;
  }
};

window.clearModuleSearchFilter = () => {
  const clearBtn = document.getElementById('clearSearchFilterBtn');
  if (clearBtn) clearBtn.classList.add('tecnm-hidden');
  if (typeof loadAdvisorySessions === 'function') loadAdvisorySessions();
};

function escapeHtml(text) {
  if (!text) return '';
  return text.toString()
    .replace(/&/g, "&amp;")
    .replace(/</g, "&lt;")
    .replace(/>/g, "&gt;")
    .replace(/"/g, "&quot;")
    .replace(/'/g, "&#039;");
}
