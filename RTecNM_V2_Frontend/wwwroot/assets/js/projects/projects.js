const API_BASE = '/api/v1/projects';

const ACTIVE_STATUSES = ['draft', 'pending', 'proposed', 'under_review', 'approved', 'in_progress'];
const DRAFT_STATUSES = ['draft', 'rejected'];
const PRINTABLE_STATUSES = ['approved', 'in_progress'];

let proposalsCache = [];
let reviewProjectsCache = [];
let editingProposalId = null;

window.openProjectAuditModal = (id) => {
  const p = (reviewProjectsCache.concat(proposalsCache)).find(item => item.id === id);
  if (!p || !window.showAuditModal) return;

  window.showAuditModal(`Auditoría — Anteproyecto #${p.id}`, [
    { label: 'ID', value: p.id },
    { label: 'Título', value: p.title },
    { label: 'Estado', value: p.isActive ? 'Activo' : 'Inactivo' },
    { label: 'Visible', value: p.isVisible ? 'Sí' : 'No' },
    { label: 'Orden', value: p.displayOrder },
    { label: 'Creado el', value: window.formatAuditDate(p.createdAt) },
    { label: 'Creado por', value: window.formatAuditUser(p.createdBy) },
    { label: 'Actualizado el', value: p.updatedBy ? window.formatAuditDate(p.updatedAt) : '—' },
    { label: 'Actualizado por', value: p.updatedBy ? window.formatAuditUser(p.updatedBy) : '—' },
    { label: 'Eliminado el', value: p.deletedAt ? window.formatAuditDate(p.deletedAt) : '—' },
    { label: 'Eliminado por', value: p.deletedBy ? window.formatAuditUser(p.deletedBy) : '—' }
  ]);
};

// -------------------------------------------------------------
// Proposal Page Logic
// -------------------------------------------------------------
async function loadCompaniesOptions() {
  const companySelect = document.getElementById('companyId');
  if (!companySelect) return;
  try {
    const res = await fetch('/api/v1/companies/options');
    if (!res.ok) return;
    const options = await res.json();
    companySelect.innerHTML = '<option value="">-- Seleccione la empresa receptora (Obligatorio) --</option>' +
      options.map(c => `<option value="${c.id}">${escapeHtml(c.name)} (${escapeHtml(c.rfc || 'Sin RFC')})</option>`).join('');
  } catch (err) {
    console.error('Error al cargar empresas:', err);
  }
}

async function loadAdvisorsOptions() {
  const advisorSelect = document.getElementById('advisorId');
  if (!advisorSelect) return;
  try {
    const res = await fetch('/api/v1/advisors/options');
    if (!res.ok) return;
    const options = await res.json();
    advisorSelect.innerHTML = '<option value="">-- Seleccione el asesor asignado (Obligatorio) --</option>' +
      (Array.isArray(options) ? options : [])
        .map(a => `<option value="${a.id}">${escapeHtml(a.fullName || a.name || 'Asesor')}</option>`).join('');
  } catch (err) {
    console.error('Error al cargar asesores:', err);
  }
}

function initProposalPage() {
  setupObjectiveRows();
  setupAdminStudentSelect();
  loadCompaniesOptions();
  loadAdvisorsOptions();
  loadStudentProposals();

  const openModalBtn = document.getElementById('openProposalModalBtn');
  const modal = document.getElementById('createProposalModal');
  const closeModalBtn = document.getElementById('closeProposalModalBtn');
  const cancelModalBtn = document.getElementById('cancelProposalModalBtn');

  if (openModalBtn && modal) {
    if (window.canCreateProposal && !window.canCreateProposal()) {
      openModalBtn.remove();
    } else {
      openModalBtn.addEventListener('click', () => modal.classList.add('active'));
    }
  }
  const hideModal = () => {
    if (modal) modal.classList.remove('active');
    resetEditingState();
    if (form) form.reset();
  };
  if (closeModalBtn) closeModalBtn.addEventListener('click', hideModal);
  if (cancelModalBtn) cancelModalBtn.addEventListener('click', hideModal);

  const detailCloseBtn = document.getElementById('closeProposalDetailModalBtn');
  if (detailCloseBtn) detailCloseBtn.addEventListener('click', closeProposalDetailModal);
  const detailCancelBtn = document.getElementById('cancelProposalDetailModalBtn');
  if (detailCancelBtn) detailCancelBtn.addEventListener('click', closeProposalDetailModal);

  const form = document.getElementById('proposalForm');
  if (form) {
    form.addEventListener('submit', handleProposalSubmit);
  }

  const refreshBtn = document.getElementById('refreshProposalsBtn');
  if (refreshBtn) {
    refreshBtn.addEventListener('click', loadStudentProposals);
  }

  const inactiveToggle = document.getElementById('proposalIncludeInactiveToggle');
  if (inactiveToggle) {
    inactiveToggle.addEventListener('change', () => {
      proposalIncludeInactive = inactiveToggle.checked;
      proposalPageNumber = 1;
      loadStudentProposals();
    });
  }
}

function setupObjectiveRows() {
  const addBtn = document.getElementById('addObjectiveBtn');
  const container = document.getElementById('objectivesContainer');
  if (!addBtn || !container) return;

  addBtn.addEventListener('click', () => {
    const count = container.querySelectorAll('.objective-row').length + 1;
    const row = document.createElement('div');
    row.className = 'objective-row';
    row.innerHTML = `
      <input type="text" class="tecnm-form-control specific-objective-input" placeholder="Objetivo específico ${count}" required>
      <button type="button" class="tecnm-btn tecnm-btn-danger tecnm-btn-sm remove-obj-btn">&times;</button>
    `;
    container.appendChild(row);
    attachRemoveHandler(row.querySelector('.remove-obj-btn'));
  });

  container.querySelectorAll('.remove-obj-btn').forEach(attachRemoveHandler);
}

function attachRemoveHandler(btn) {
  if (!btn) return;
  btn.addEventListener('click', (e) => {
    const container = document.getElementById('objectivesContainer');
    if (container.querySelectorAll('.objective-row').length > 1) {
      e.target.closest('.objective-row').remove();
    } else {
      showAlert('Debe ingresar al menos un objetivo específico.', 'warning');
    }
  });
}

async function setupAdminStudentSelect() {
  const group = document.getElementById('adminStudentGroup');
  if (!group) return;
  if (window.hasRole && !window.hasRole('admin', 'departmenthead')) {
    group.classList.add('tecnm-hidden');
    return;
  }
  group.classList.remove('tecnm-hidden');
  try {
    const res = await fetch('/api/v1/students/options');
    if (!res.ok) return;
    const students = await res.json();
    const select = document.getElementById('adminStudentId');
    if (!select) return;
    select.innerHTML = '<option value="">-- Seleccione el estudiante destinatario (Obligatorio) --</option>' +
      (Array.isArray(students) ? students : [])
        .map(s => `<option value="${s.id}">${escapeHtml(s.controlNumber)} - ${escapeHtml(s.fullName)}</option>`)
        .join('');
  } catch {
    // El grupo de selección queda sin opciones; el submit validará.
  }
}

let proposalPageNumber = 1;
let proposalIncludeInactive = false;

async function loadStudentProposals() {
  const tableBody = document.getElementById('studentProposalsTableBody');
  const paginationContainer = document.getElementById('studentProposalsPagination');
  if (!tableBody) return;

  tableBody.innerHTML = `<tr><td colspan="5" class="tecnm-table-empty">Cargando propuestas del residente...</td></tr>`;

  try {
    const isStaff = window.hasRole ? window.hasRole('admin', 'departmenthead') : false;
    const endpoint = isStaff ? API_BASE : `${API_BASE}/me`;
    const params = new URLSearchParams({ pageNumber: proposalPageNumber, pageSize: 10, includeInactive: proposalIncludeInactive });
    if (isStaff) params.set('status', 'all');

    const res = await fetch(`${endpoint}?${params}`);
    if (!res.ok) {
      const errData = await res.json().catch(() => ({}));
      throw new Error(errData.message || 'Error al consultar historial');
    }

    const data = await res.json();
    const proposals = (data && data.items) || [];
    proposalsCache = proposals;

    if (window.canSeeAudit && window.canSeeAudit()) {
      await window.loadAuditUserNames(window.collectAuditUserIds(proposals));
    }

    if (proposals.length === 0 && proposalPageNumber > 1 && data.totalPages > 0) {
      proposalPageNumber = data.totalPages;
      return loadStudentProposals();
    }

    const canSeeAudit = window.canSeeAudit ? window.canSeeAudit() : false;

    if (proposals.length === 0) {
      tableBody.innerHTML = `<tr><td colspan="6" class="tecnm-table-empty">No hay solicitudes de anteproyecto registradas.</td></tr>`;
    } else {
      tableBody.innerHTML = proposals.map(p => {
        const formattedDate = window.formatTecNMDate(p.createdAt);
        return `
          <tr>
            <td>${escapeHtml(p.title)}</td>
            <td>${escapeHtml(p.companyName || '—')}</td>
            <td>${escapeHtml(p.projectType || 'Desarrollo')}</td>
            <td>${formattedDate}</td>
            <td>${getBadgeHtml(p.status)}</td>
            <td>${renderProposalActions(p, isStaff, canSeeAudit)}</td>
          </tr>
        `;
      }).join('');
    }

    updateProposalGate(proposals);

    if (window.renderPagination) {
      window.renderPagination(paginationContainer, data, (page) => {
        proposalPageNumber = page;
        loadStudentProposals();
      });
    }
  } catch (err) {
    tableBody.innerHTML = `<tr><td colspan="6" class="tecnm-table-empty tecnm-text-danger">${escapeHtml(err.message || 'Error al cargar propuestas.')}</td></tr>`;
    if (paginationContainer) paginationContainer.innerHTML = '';
  }
}

function renderProposalActions(p, isStaff, canSeeAudit) {
  const st = (p.status || '').toLowerCase();
  const canEdit = isStaff ? !['completed', 'cancelled'].includes(st) : DRAFT_STATUSES.includes(st);
  const canSubmit = (st === 'draft' || st === 'rejected');
  const canPrint = PRINTABLE_STATUSES.includes(st);
  const canCancel = ACTIVE_STATUSES.includes(st);
  const canManage = window.canManageRegistry ? window.canManageRegistry() : false;

  const buttons = [
    `<button type="button" class="tecnm-btn tecnm-btn-secondary tecnm-btn-sm" onclick="openProposalDetailModal(${p.id})">Ver detalle</button>`
  ];
  if (canEdit) {
    buttons.push(`<button type="button" class="tecnm-btn tecnm-btn-secondary tecnm-btn-sm" onclick="openProposalEditModal(${p.id})">${isStaff ? 'Editar' : 'Editar borrador'}</button>`);
  }
  if (canSubmit) {
    buttons.push(`<button type="button" class="tecnm-btn tecnm-btn-primary tecnm-btn-sm" onclick="submitProposal(${p.id})">Enviar a revisión</button>`);
  }
  if (canPrint) {
    buttons.push(`<button type="button" class="tecnm-btn tecnm-btn-secondary tecnm-btn-sm" onclick="downloadProposalPdf(${p.id})">Descargar PDF</button>`);
  }
  if (canSeeAudit) {
    buttons.push(`<button type="button" class="tecnm-btn tecnm-btn-secondary tecnm-btn-sm" onclick="openProjectAuditModal(${p.id})">Auditoría</button>`);
  }
  if (canCancel) {
    buttons.push(`<button type="button" class="tecnm-btn tecnm-btn-danger tecnm-btn-sm" onclick="cancelProposal(${p.id})">Cancelar solicitud</button>`);
  }
  if ((!p.isActive || st === 'cancelled') && canManage) {
    buttons.push(`<button type="button" class="tecnm-btn tecnm-btn-success tecnm-btn-sm" onclick="reactivateProposal(${p.id})">Reactivar</button>`);
  }

  return `<div class="tecnm-row-actions">${buttons.join('')}</div>`;
}

function updateProposalGate(proposals) {
  const openModalBtn = document.getElementById('openProposalModalBtn');
  if (!openModalBtn) return;

  const isStaff = window.hasRole ? window.hasRole('admin', 'departmenthead') : false;
  if (isStaff) return;

  const hasActiveProject = (proposals || []).some(p => ACTIVE_STATUSES.includes((p.status || '').toLowerCase()));
  const hasDraft = (proposals || []).some(p => (p.status || '').toLowerCase() === 'draft');

  if (hasActiveProject) {
    openModalBtn.disabled = true;
    openModalBtn.title = hasDraft
      ? 'Ya cuentas con un anteproyecto en borrador. Edítalo o envíalo a revisión desde la tabla.'
      : 'Ya cuentas con un anteproyecto vigente. Cancélalo o espera su dictamen para registrar uno nuevo.';
    if (hasDraft) {
      showAlert('Ya tienes un anteproyecto guardado como borrador. Continúa editándolo o envíalo a revisión desde la tabla.', 'info');
    } else {
      showAlert('Ya cuentas con un anteproyecto vigente. No puedes registrar una nueva solicitud hasta que sea dictaminado o cancelado.', 'warning');
    }
  } else {
    openModalBtn.disabled = false;
    openModalBtn.title = '';
  }
}

async function openProposalDetailModal(id) {
  try {
    const res = await fetch(`${API_BASE}/${id}`);
    if (!res.ok) throw new Error();
    const p = await res.json();

    document.getElementById('detailProjectId').innerText = p.id;
    document.getElementById('detailProjectTitle').innerText = p.title;
    document.getElementById('detailProjectType').innerText = p.projectType || '—';
    document.getElementById('detailProjectStatus').innerHTML = getBadgeHtml(p.status);
    if (document.getElementById('detailProjectCompanyName')) {
      document.getElementById('detailProjectCompanyName').innerText = p.companyName || '—';
    }
    document.getElementById('detailProjectCreatedAt').innerText = window.formatTecNMDate(p.createdAt);
    document.getElementById('detailProblemStatement').innerText = p.problemStatement;
    document.getElementById('detailJustification').innerText = p.justification;
    document.getElementById('detailGeneralObjective').innerText = p.generalObjective;

    const list = document.getElementById('detailObjectivesList');
    if (p.objectives && p.objectives.length > 0) {
      list.innerHTML = p.objectives.map(o => `<li>${escapeHtml(o.description)}</li>`).join('');
    } else {
      list.innerHTML = '<li>Sin objetivos específicos registrados.</li>';
    }

    document.getElementById('proposalDetailModal').classList.add('active');
  } catch (err) {
    showAlert('No se pudieron cargar los detalles del anteproyecto.', 'danger');
  }
}

function closeProposalDetailModal() {
  const modal = document.getElementById('proposalDetailModal');
  if (modal) modal.classList.remove('active');
}

async function openProposalEditModal(id) {
  try {
    const res = await fetch(`${API_BASE}/${id}`);
    if (!res.ok) throw new Error();
    const p = await res.json();

    const st = (p.status || '').toLowerCase();
    const isStaff = window.hasRole ? window.hasRole('admin', 'departmenthead') : false;
    const editable = isStaff ? !['completed', 'cancelled'].includes(st) : DRAFT_STATUSES.includes(st);
    if (!editable) {
      showAlert(isStaff
        ? 'No se puede editar un anteproyecto completado o cancelado.'
        : 'Solo puedes editar anteproyectos en estado de borrador. Una vez dictaminado, contacta a la División.', 'warning');
      return;
    }

    editingProposalId = id;

    if (document.getElementById('companyId')) document.getElementById('companyId').value = p.companyId || '';
    if (document.getElementById('advisorId')) document.getElementById('advisorId').value = p.advisorId || '';
    document.getElementById('title').value = p.title || '';
    document.getElementById('projectType').value = p.projectType || '';
    document.getElementById('problemStatement').value = p.problemStatement || '';
    document.getElementById('justification').value = p.justification || '';
    document.getElementById('generalObjective').value = p.generalObjective || '';

    const container = document.getElementById('objectivesContainer');
    if (container) {
      container.innerHTML = '';
      const texts = (p.objectives || []).map(o => o.description);
      if (texts.length === 0) texts.push('');
      texts.forEach((text, index) => {
        const row = document.createElement('div');
        row.className = 'objective-row';
        row.innerHTML = `
          <input type="text" class="tecnm-form-control specific-objective-input" placeholder="Objetivo específico ${index + 1}" value="${escapeHtml(text)}" required>
          <button type="button" class="tecnm-btn tecnm-btn-danger tecnm-btn-sm remove-obj-btn">&times;</button>
        `;
        container.appendChild(row);
        attachRemoveHandler(row.querySelector('.remove-obj-btn'));
      });
    }

    const modalTitle = document.getElementById('createProposalModalTitle');
    if (modalTitle) modalTitle.textContent = 'Editar Solicitud de Anteproyecto';
    const submitBtn = document.getElementById('submitProposalBtn');
    if (submitBtn) submitBtn.textContent = 'Guardar Cambios';

    document.getElementById('createProposalModal').classList.add('active');
  } catch (err) {
    showAlert('No se pudo cargar el anteproyecto para editar.', 'danger');
  }
}

function resetEditingState() {
  editingProposalId = null;
  const modalTitle = document.getElementById('createProposalModalTitle');
  if (modalTitle) modalTitle.textContent = 'Registrar Nueva Solicitud de Anteproyecto';
  const submitBtn = document.getElementById('submitProposalBtn');
  if (submitBtn) submitBtn.textContent = 'Guardar Borrador';
}

function downloadProposalPdf(id) {
  if (!window.downloadPdf) {
    showAlert('No es posible generar el PDF en este momento.', 'danger');
    return;
  }
  const p = proposalsCache.find(x => x.id === id);
  const title = (p && p.title) || '';
  const safeTitle = title.toString().replace(/[^\w\s-]/g, '').trim().replace(/\s+/g, '_').slice(0, 50);
  const filename = safeTitle ? `Anteproyecto_${safeTitle}.pdf` : 'Anteproyecto.pdf';
  window.downloadPdf(`${API_BASE}/${id}/pdf`, filename);
}

async function submitProposal(id) {
  const confirmed = await window.tecnmConfirm('¿Desea enviar el anteproyecto a revisión de la División? Una vez enviado ya no podrá editarlo desde el borrador.', 'Enviar a Revisión');
  if (!confirmed) return;

  try {
    const res = await fetch(`${API_BASE}/${id}/submit`, { method: 'PATCH' });
    if (res.ok) {
      showAlert('Anteproyecto enviado a revisión correctamente.', 'success');
      loadStudentProposals();
    } else {
      const err = await res.json();
      showAlert(err.message || 'Error al enviar el anteproyecto a revisión.', 'danger');
    }
  } catch (err) {
    showAlert('Error de conexión al enviar el anteproyecto a revisión.', 'danger');
  }
}

async function cancelProposal(id) {
  const confirmed = await window.tecnmConfirm(`¿Desea cancelar la solicitud de anteproyecto #${id}? Esta acción no se puede deshacer.`, 'Cancelar Solicitud');
  if (!confirmed) return;

  try {
    const res = await fetch(`${API_BASE}/${id}/cancel`, { method: 'PATCH' });
    if (res.ok) {
      showAlert('Solicitud de anteproyecto cancelada correctamente.', 'success');
      loadStudentProposals();
    } else {
      const err = await res.json();
      showAlert(err.message || 'Error al cancelar la solicitud.', 'danger');
    }
  } catch (err) {
    showAlert('Error de conexión al cancelar la solicitud.', 'danger');
  }
}

async function reactivateProposal(id) {
  const confirmed = await window.tecnmConfirm(`¿Desea reactivar el anteproyecto #${id}?`, 'Reactivar Anteproyecto');
  if (!confirmed) return;

  try {
    const res = await fetch(`${API_BASE}/${id}/activate`, { method: 'PATCH' });
    if (res.ok) {
      showAlert('Anteproyecto reactivado correctamente.', 'success');
      loadStudentProposals();
    } else {
      const err = await res.json();
      showAlert(err.message || 'Error al reactivar el anteproyecto.', 'danger');
    }
  } catch (err) {
    showAlert('Error de conexión al reactivar el anteproyecto.', 'danger');
  }
}

async function handleProposalSubmit(e) {
  e.preventDefault();
  const companyIdVal = document.getElementById('companyId')?.value;
  const companyId = parseInt(companyIdVal, 10);
  if (!companyIdVal || isNaN(companyId) || companyId <= 0) {
    showAlert('Debe seleccionar obligatoriamente una empresa receptora vinculada.', 'warning');
    return;
  }

  const advisorIdVal = document.getElementById('advisorId')?.value;
  const advisorId = parseInt(advisorIdVal, 10);
  if (!advisorIdVal || isNaN(advisorId) || advisorId <= 0) {
    showAlert('Debe seleccionar obligatoriamente al asesor interno asignado.', 'warning');
    return;
  }

  const title = document.getElementById('title').value.trim();
  const projectType = document.getElementById('projectType').value.trim();
  const problemStatement = document.getElementById('problemStatement').value.trim();
  const justification = document.getElementById('justification').value.trim();
  const generalObjective = document.getElementById('generalObjective').value.trim();

  const objectiveInputs = document.querySelectorAll('.specific-objective-input');
  const specificObjectives = Array.from(objectiveInputs)
    .map(input => input.value.trim())
    .filter(val => val.length > 0);

  const payload = {
    companyId,
    advisorId,
    title,
    projectType,
    problemStatement,
    justification,
    generalObjective,
    specificObjectives
  };

  const isEdit = editingProposalId != null;

  // La atribución del estudiante se resuelve en el servidor desde la sesión para alumnos.
  // Los Administradores / Jefatura NUNCA registran anteproyectos a su nombre; siempre deben derivárselo a un estudiante destinatario.
  if (!isEdit && window.hasRole && window.hasRole('admin', 'departmenthead')) {
    const studentIdVal = document.getElementById('adminStudentId')?.value;
    const studentId = parseInt(studentIdVal, 10);
    if (!studentIdVal || isNaN(studentId) || studentId <= 0) {
      showAlert('Los administradores no pueden registrar anteproyectos a nombre propio. Seleccione obligatoriamente el estudiante destinatario al que se le derivará.', 'warning');
      return;
    }
    payload.studentId = studentId;
  }

  const submitBtn = document.getElementById('submitProposalBtn');
  submitBtn.disabled = true;
  submitBtn.textContent = 'Guardando...';

  try {
    const endpoint = isEdit ? `${API_BASE}/${editingProposalId}` : API_BASE;
    const res = await fetch(endpoint, {
      method: isEdit ? 'PUT' : 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(payload)
    });

    if (res.ok) {
      showAlert(isEdit
        ? '¡Borrador del anteproyecto guardado correctamente!'
        : '¡Solicitud de anteproyecto guardada como borrador! Envíala a revisión cuando esté lista.', 'success');
      const modal = document.getElementById('createProposalModal');
      if (modal) modal.classList.remove('active');
      document.getElementById('proposalForm').reset();
      resetEditingState();
      loadStudentProposals();
    } else {
      const errData = await res.json();
      showAlert(errData.message || 'Error al registrar la propuesta.', 'danger');
    }
  } catch (err) {
    showAlert('Error de conexión con el servidor.', 'danger');
  } finally {
    submitBtn.disabled = false;
    submitBtn.textContent = isEdit ? 'Guardar Cambios' : 'Guardar Borrador';
  }
}

// -------------------------------------------------------------
// Review Page Logic
// -------------------------------------------------------------
let selectedProjectId = null;
let reviewPageNumber = 1;
let reviewSearch = '';
let reviewSortBy = 'CreatedAt';
let reviewSortDir = 'desc';
let reviewIncludeInactive = false;

function initReviewPage() {
  loadProjects();

  if (window.bindTableSearch) {
    window.bindTableSearch('projectSearchInput', (term) => {
      reviewSearch = term;
      reviewPageNumber = 1;
      loadProjects();
    });
  }

  if (window.initSortableHeaders) {
    window.initSortableHeaders('projectsTable', (field, dir) => {
      reviewSortBy = field;
      reviewSortDir = dir;
      reviewPageNumber = 1;
      loadProjects();
    });
  }

  const inactiveToggle = document.getElementById('includeInactiveToggle');
  if (inactiveToggle) {
    inactiveToggle.addEventListener('change', () => {
      reviewIncludeInactive = inactiveToggle.checked;
      reviewPageNumber = 1;
      loadProjects();
    });
  }

  const exportProjectsBtn = document.getElementById('exportProjectsBtn');
  if (exportProjectsBtn && window.downloadPdf) {
    exportProjectsBtn.addEventListener('click', () => {
      const status = document.getElementById('statusFilter')?.value || 'all';
      const params = new URLSearchParams({
        status,
        search: reviewSearch,
        sortBy: reviewSortBy,
        sortDir: reviewSortDir,
        includeInactive: reviewIncludeInactive
      });
      window.downloadPdf(`${API_BASE}/export?${params}`, 'anteproyectos_tecnm.pdf');
    });
  }

  const closeBtn = document.getElementById('closeModalBtn');
  if (closeBtn) closeBtn.addEventListener('click', closeModal);

  const approveBtn = document.getElementById('approveBtn');
  if (approveBtn) approveBtn.addEventListener('click', () => submitDictamen('approved'));

  const rejectBtn = document.getElementById('rejectBtn');
  if (rejectBtn) rejectBtn.addEventListener('click', () => submitDictamen('rejected'));

  const softDeleteBtn = document.getElementById('modalSoftDeleteBtn');
  if (softDeleteBtn) {
    if (window.canManageRegistry && !window.canManageRegistry()) {
      softDeleteBtn.remove();
    } else {
      softDeleteBtn.addEventListener('click', handleModalSoftDelete);
    }
  }

  const statusFilter = document.getElementById('statusFilter');
  if (statusFilter) {
    statusFilter.addEventListener('change', () => {
      reviewPageNumber = 1;
      loadProjects();
    });
  }
}

async function loadProjects() {
  const tbody = document.getElementById('projectsTableBody');
  const paginationContainer = document.getElementById('projectsPagination');
  if (!tbody) return;

  try {
    const status = document.getElementById('statusFilter')?.value || 'all';
    const params = new URLSearchParams({
      pageNumber: reviewPageNumber,
      pageSize: 10,
      status,
      search: reviewSearch,
      sortBy: reviewSortBy,
      sortDir: reviewSortDir,
      includeInactive: reviewIncludeInactive
    });
    const res = await fetch(`${API_BASE}?${params}`);
    if (!res.ok) {
      const errData = await res.json().catch(() => ({}));
      throw new Error(errData.message || 'Error al consultar anteproyectos');
    }

    const data = await res.json();
    const projects = (data && data.items) || [];

    if (window.canSeeAudit && window.canSeeAudit()) {
      await window.loadAuditUserNames(window.collectAuditUserIds(projects));
    }

    if (projects.length === 0 && reviewPageNumber > 1 && data.totalPages > 0) {
      reviewPageNumber = data.totalPages;
      return loadProjects();
    }

    if (reviewIncludeInactive) {
      const hasInactive = projects.some(p => p.isActive === false || p.is_active === false);
      if (!hasInactive) {
        showAlert('No existen registros inactivos.', 'info');
        const inactiveToggle = document.getElementById('includeInactiveToggle');
        if (inactiveToggle) inactiveToggle.checked = false;
        reviewIncludeInactive = false;
      }
    }

    renderProjectsTable(projects);

    if (window.renderPagination) {
      window.renderPagination(paginationContainer, data, (page) => {
        reviewPageNumber = page;
        loadProjects();
      });
    }
  } catch (err) {
    tbody.innerHTML = `<tr><td colspan="5" class="tecnm-table-empty tecnm-text-danger">${escapeHtml(err.message || 'Error al consultar la base de datos de anteproyectos.')}</td></tr>`;
    if (paginationContainer) paginationContainer.innerHTML = '';
  }
}

function renderProjectsTable(projects) {
  const tbody = document.getElementById('projectsTableBody');
  if (!tbody) return;

  reviewProjectsCache = projects || [];

  if (projects.length === 0) {
    tbody.innerHTML = `<tr><td colspan="5" class="tecnm-table-empty">No hay anteproyectos que coincidan con el filtro seleccionado.</td></tr>`;
    return;
  }

  const canDelete = window.canManageRegistry ? window.canManageRegistry() : true;
  const canSeeAudit = window.canSeeAudit ? window.canSeeAudit() : false;

  tbody.innerHTML = projects.map(p => `
    <tr>
      <td>${escapeHtml(p.title)}</td>
      <td>${escapeHtml(p.studentName || `Estudiante #${p.studentId}`)}</td>
      <td>${window.formatTecNMDate(p.createdAt)}</td>
      <td>${getBadgeHtml(p.status)}</td>
      <td>
        <div class="tecnm-row-actions">
          <button class="tecnm-btn tecnm-btn-secondary tecnm-btn-sm" onclick="openReviewModal(${p.id})">Revisar</button>
          ${canSeeAudit ? `<button class="tecnm-btn tecnm-btn-secondary tecnm-btn-sm" onclick="openProjectAuditModal(${p.id})">Auditoría</button>` : ''}
          ${canDelete ? `<button class="tecnm-btn tecnm-btn-danger tecnm-btn-sm" onclick="softDeleteProject(${p.id})">Eliminar</button>` : ''}
        </div>
      </td>
    </tr>
  `).join('');
}

async function openReviewModal(projectId) {
  selectedProjectId = projectId;
  try {
    const res = await fetch(`${API_BASE}/${projectId}`);
    if (!res.ok) throw new Error();

    const p = await res.json();

    document.getElementById('modalProjectId').innerText = p.id;
    document.getElementById('modalProjectTitle').innerText = p.title;
    const studentEl = document.getElementById('modalStudentName');
    if (studentEl) studentEl.innerText = p.studentName || `Estudiante #${p.studentId}`;
    document.getElementById('modalProblemStatement').innerText = p.problemStatement;
    document.getElementById('modalJustification').innerText = p.justification;
    document.getElementById('modalGeneralObjective').innerText = p.generalObjective;

    const list = document.getElementById('modalObjectivesList');
    if (p.objectives && p.objectives.length > 0) {
      list.innerHTML = p.objectives.map(o => `<li>${escapeHtml(o.description)}</li>`).join('');
    } else {
      list.innerHTML = `<li>Sin objetivos específicos registrados.</li>`;
    }

    const reviewNotice = document.getElementById('reviewNotice');
    const approveBtn = document.getElementById('approveBtn');
    const rejectBtn = document.getElementById('rejectBtn');
    const commentsGroup = document.getElementById('reviewCommentsGroup');

    const st = (p.status || '').toLowerCase();
    const isDraft = st === 'draft' || st === 'borrador';
    const isApproved = st === 'approved' || st === 'aprobado';
    const isInProgress = st === 'in_progress' || st === 'inprogress' || st === 'en_progreso' || st === 'en progreso';
    const isCompleted = st === 'completed' || st === 'completado';
    const isCancelled = st === 'cancelled' || st === 'cancelado';
    const isReadOnly = window.isReadOnlyUser ? window.isReadOnlyUser() : false;
    const canApprove = window.hasRole ? window.hasRole('admin', 'vinculacion') : true;
    const deleteBtn = document.getElementById('modalSoftDeleteBtn');

    if (isReadOnly) {
      if (reviewNotice) {
        reviewNotice.innerHTML = '<strong>Modo Consulta (Director):</strong> Vista de solo lectura. No tiene permisos para dictaminar ni modificar registros.';
        reviewNotice.classList.remove('tecnm-hidden');
      }
      if (approveBtn) approveBtn.classList.add('tecnm-hidden');
      if (rejectBtn) rejectBtn.classList.add('tecnm-hidden');
      if (commentsGroup) commentsGroup.classList.add('tecnm-hidden');
    } else if (isDraft) {
      if (reviewNotice) {
        reviewNotice.innerHTML = 'Este anteproyecto está en <strong>BORRADOR</strong> y aún no ha sido enviado a revisión. La División no puede dictaminarlo hasta que el estudiante lo envíe.';
        reviewNotice.classList.remove('tecnm-hidden');
      }
      if (approveBtn) approveBtn.classList.add('tecnm-hidden');
      if (rejectBtn) rejectBtn.classList.add('tecnm-hidden');
      if (commentsGroup) commentsGroup.classList.add('tecnm-hidden');
    } else if (isApproved) {
      if (reviewNotice) {
        reviewNotice.innerHTML = 'Este anteproyecto ya ha sido <strong>APROBADO</strong>. No se pueden realizar modificaciones al dictamen ni solicitar correcciones.';
        reviewNotice.classList.remove('tecnm-hidden');
      }
      if (approveBtn) approveBtn.classList.add('tecnm-hidden');
      if (rejectBtn) rejectBtn.classList.add('tecnm-hidden');
      if (commentsGroup) commentsGroup.classList.add('tecnm-hidden');
    } else if (isInProgress) {
      if (reviewNotice) {
        reviewNotice.innerHTML = 'Este proyecto está <strong>EN PROGRESO</strong> (en desarrollo). No admite revisiones ni cambios de anteproyecto.';
        reviewNotice.classList.remove('tecnm-hidden');
      }
      if (approveBtn) approveBtn.classList.add('tecnm-hidden');
      if (rejectBtn) rejectBtn.classList.add('tecnm-hidden');
      if (commentsGroup) commentsGroup.classList.add('tecnm-hidden');
    } else if (isCompleted) {
      if (reviewNotice) {
        reviewNotice.innerHTML = 'Este proyecto ya se encuentra <strong>COMPLETADO</strong>. No admite modificaciones ni cambios de estado.';
        reviewNotice.classList.remove('tecnm-hidden');
      }
      if (approveBtn) approveBtn.classList.add('tecnm-hidden');
      if (rejectBtn) rejectBtn.classList.add('tecnm-hidden');
      if (commentsGroup) commentsGroup.classList.add('tecnm-hidden');
      if (deleteBtn) deleteBtn.classList.add('tecnm-hidden');
    } else if (isCancelled) {
      if (reviewNotice) {
        reviewNotice.innerHTML = 'Este anteproyecto ha sido <strong>CANCELADO</strong>.';
        reviewNotice.classList.remove('tecnm-hidden');
      }
      if (approveBtn) approveBtn.classList.add('tecnm-hidden');
      if (rejectBtn) rejectBtn.classList.add('tecnm-hidden');
      if (commentsGroup) commentsGroup.classList.add('tecnm-hidden');
    } else {
      if (reviewNotice) reviewNotice.classList.add('tecnm-hidden');
      if (approveBtn) {
        if (canApprove) approveBtn.classList.remove('tecnm-hidden');
        else approveBtn.classList.add('tecnm-hidden');
      }
      if (rejectBtn) rejectBtn.classList.remove('tecnm-hidden');
      if (commentsGroup) commentsGroup.classList.remove('tecnm-hidden');
      if (deleteBtn) deleteBtn.classList.remove('tecnm-hidden');
    }

    document.getElementById('reviewComments').value = '';
    document.getElementById('reviewModal').classList.add('active');
  } catch (err) {
    showAlert('No se pudieron cargar los detalles del anteproyecto.', 'danger');
  }
}

function closeModal() {
  document.getElementById('reviewModal').classList.remove('active');
  selectedProjectId = null;
}

async function submitDictamen(status) {
  if (!selectedProjectId) return;

  const comments = document.getElementById('reviewComments').value.trim();

  try {
    const res = await fetch(`${API_BASE}/${selectedProjectId}/status`, {
      method: 'PATCH',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ status, comments })
    });

    if (res.ok) {
      closeModal();
      showAlert(`Dictamen emitido correctamente como ${status.toUpperCase()}.`, 'success');
      loadProjects();
    } else {
      const err = await res.json();
      showAlert(err.message || 'Error al registrar dictamen.', 'danger');
    }
  } catch (err) {
    showAlert('Error de conexión al emitir dictamen.', 'danger');
  }
}

async function handleModalSoftDelete() {
  if (!selectedProjectId) return;
  const deleted = await softDeleteProject(selectedProjectId);
  if (deleted) closeModal();
}

window.softDeleteProject = async function(id) {
  const confirmed = await window.tecnmConfirm(`¿Está seguro de eliminar lógicamente el anteproyecto #${id}?`, 'Eliminar Anteproyecto');
  if (!confirmed) return false;

  try {
    const res = await fetch(`${API_BASE}/${id}`, {
      method: 'DELETE'
    });

    if (res.ok) {
      showAlert(`Anteproyecto #${id} eliminado lógicamente de la base de datos.`, 'warning');
      loadProjects();
    } else {
      const err = await res.json();
      showAlert(err.message || 'Error al eliminar anteproyecto.', 'danger');
    }
  } catch (err) {
    showAlert('Error de conexión con el servidor.', 'danger');
  }
};

window.displaySingleProject = (row) => {
  const clearBtn = document.getElementById('clearSearchFilterBtn');
  if (clearBtn) clearBtn.classList.remove('tecnm-hidden');

  const paginationContainer = document.getElementById('projectsPagination');
  if (paginationContainer) paginationContainer.innerHTML = '';

  const id = row.id;
  const title = row.title || '-';
  const studentName = row.student_name || row.studentName || '-';
  const advisorName = row.advisor_name || row.advisorName || 'Sin Asignar';
  const status = row.status || 'Pending';

  const projectsTableBody = document.getElementById('projectsTableBody');
  if (projectsTableBody) {
    projectsTableBody.innerHTML = `
      <tr>
        <td><strong>${escapeHtml(title)}</strong></td>
        <td>${escapeHtml(studentName)}</td>
        <td>${escapeHtml(advisorName)}</td>
        <td>${getBadgeHtml(status)}</td>
        <td class="tecnm-text-center">
          <button type="button" class="tecnm-btn tecnm-btn-secondary tecnm-btn-sm" onclick="openProposalDetailModal(${id})">Ver detalle</button>
        </td>
      </tr>
    `;
  }
};

window.clearModuleSearchFilter = () => {
  const clearBtn = document.getElementById('clearSearchFilterBtn');
  if (clearBtn) clearBtn.classList.add('tecnm-hidden');
  if (typeof loadProjects === 'function') loadProjects();
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
