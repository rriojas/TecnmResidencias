document.addEventListener('DOMContentLoaded', () => {
  initAssignPage();
});

let currentPage = 1;
let currentSearch = '';
let currentSortBy = 'ControlNumber';
let currentSortDir = 'asc';
let availableAdvisors = [];

async function initAssignPage() {
  await loadAdvisorsOptions();
  await loadStudents();

  window.bindTableSearch('assignSearchInput', (val) => {
    currentSearch = val;
    currentPage = 1;
    loadStudents();
  });

  window.initSortableHeaders('assignTable', (field, dir) => {
    currentSortBy = field;
    currentSortDir = dir;
    loadStudents();
  });

  const reloadBtn = document.getElementById('reloadAssignBtn');
  if (reloadBtn) {
    reloadBtn.addEventListener('click', () => {
      loadStudents();
    });
  }

  const form = document.getElementById('assignAdvisorForm');
  if (form) {
    form.addEventListener('submit', handleAssignSubmit);
  }

  const closeBtn = document.getElementById('closeAssignModalBtn');
  const cancelBtn = document.getElementById('cancelAssignModalBtn');
  if (closeBtn) closeBtn.addEventListener('click', closeAssignModal);
  if (cancelBtn) cancelBtn.addEventListener('click', closeAssignModal);
}

async function loadAdvisorsOptions() {
  try {
    const res = await fetch('/api/v1/advisors/options');
    if (res.ok) {
      availableAdvisors = await res.json();
    }
  } catch (e) {
    console.error('Error al cargar la lista de asesores', e);
  }
}

async function loadStudents() {
  const tbody = document.getElementById('assignTableBody');
  if (!tbody) return;

  tbody.innerHTML = `<tr><td colspan="6" class="tecnm-table-empty">Cargando datos...</td></tr>`;

  try {
    const params = new URLSearchParams({
      pageNumber: currentPage,
      pageSize: 10,
      sortBy: currentSortBy,
      sortDir: currentSortDir,
      search: currentSearch
    });

    const res = await fetch(`/api/v1/students?${params}`);
    if (!res.ok) {
      tbody.innerHTML = `<tr><td colspan="6" class="tecnm-table-empty tecnm-text-danger">Error al cargar la lista de estudiantes.</td></tr>`;
      return;
    }

    const data = await res.json();
    renderStudentsTable(data.items || []);
    window.renderPagination(document.getElementById('assignPagination'), data, (page) => {
      currentPage = page;
      loadStudents();
    });
  } catch (err) {
    tbody.innerHTML = `<tr><td colspan="6" class="tecnm-table-empty tecnm-text-danger">Error de conexión al cargar estudiantes.</td></tr>`;
  }
}

function renderStudentsTable(students) {
  const tbody = document.getElementById('assignTableBody');
  if (!tbody) return;

  if (!students.length) {
    tbody.innerHTML = `<tr><td colspan="6" class="tecnm-table-empty">No se encontraron estudiantes.</td></tr>`;
    return;
  }

  tbody.innerHTML = students.map(s => {
    const fullName = `${s.firstName || ''} ${s.lastName || ''}`.trim();
    const hasAdvisor = !!s.advisorId;
    const advisorText = s.advisorName || 'Sin Asesor Asignado';
    const badgeClass = hasAdvisor ? 'tecnm-badge-success' : 'tecnm-badge-warning';
    const badgeLabel = hasAdvisor ? 'Asesor Asignado' : 'Pendiente de Asignación';

    return `
      <tr>
        <td><strong>${escapeHtml(s.controlNumber || '')}</strong></td>
        <td>${escapeHtml(fullName)}</td>
        <td>${escapeHtml(s.email || '')}</td>
        <td>${escapeHtml(advisorText)}</td>
        <td><span class="tecnm-badge ${badgeClass}">${badgeLabel}</span></td>
        <td>
          <button type="button" class="tecnm-btn tecnm-btn-sm tecnm-btn-primary" onclick="openAssignModal(${s.id}, '${escapeHtml(fullName)}', ${s.advisorId || 'null'})">
            ${hasAdvisor ? 'Cambiar Asesor' : 'Asignar Asesor'}
          </button>
        </td>
      </tr>
    `;
  }).join('');
}

window.openAssignModal = function(studentId, studentName, currentAdvisorId) {
  document.getElementById('modalStudentId').value = studentId;
  document.getElementById('modalStudentName').textContent = studentName;

  const select = document.getElementById('modalAdvisorSelect');
  select.innerHTML = '<option value="">Selecciona un asesor docente...</option>' +
    availableAdvisors.map(a => `<option value="${a.id}">${escapeHtml(a.fullName)} (${escapeHtml(a.title || 'Docente')})</option>`).join('');

  if (currentAdvisorId) {
    select.value = currentAdvisorId;
  }

  const modal = document.getElementById('assignAdvisorModal');
  if (modal) modal.classList.add('active');
};

function closeAssignModal() {
  const modal = document.getElementById('assignAdvisorModal');
  if (modal) modal.classList.remove('active');
}

async function handleAssignSubmit(e) {
  e.preventDefault();
  const studentId = document.getElementById('modalStudentId').value;
  const advisorId = document.getElementById('modalAdvisorSelect').value;

  if (!studentId || !advisorId) {
    showAlert('Debes seleccionar un asesor docente obligatoriamente.', 'warning');
    return;
  }

  try {
    const res = await fetch(`/api/v1/students/${studentId}/advisor`, {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ advisorId: parseInt(advisorId, 10) })
    });

    if (!res.ok) {
      const err = await res.json().catch(() => ({}));
      showAlert(err.message || 'Error al asignar el asesor.', 'danger');
      return;
    }

    showAlert('Asesor asignado correctamente al estudiante.', 'success');
    closeAssignModal();
    loadStudents();
  } catch (err) {
    showAlert('Error de conexión al asignar el asesor.', 'danger');
  }
}

function escapeHtml(str) {
  return String(str || '')
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;');
}
