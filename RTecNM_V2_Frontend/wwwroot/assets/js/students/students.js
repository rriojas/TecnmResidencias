(() => {
  'use strict';

  const API_URL = '/api/v1/students';
  const STORAGE_KEY = 'authToken';

  const INSTITUTIONAL_DOMAIN = '@monclova.tecnm.mx';
  const INSTITUTIONAL_EMAIL_ERROR = 'Debes ingresar un correo institucional válido (@monclova.tecnm.mx).';

  function isInstitutionalEmail(email) {
    const clean = (email || '').trim().toLowerCase();
    return clean.endsWith(INSTITUTIONAL_DOMAIN) && clean.length > INSTITUTIONAL_DOMAIN.length;
  }

  function showStudentFormError(message) {
    const el = document.getElementById('studentFormAlert');
    if (el) {
      el.textContent = message;
      el.classList.remove('tecnm-hidden');
    }
  }

  function hideStudentFormError() {
    const el = document.getElementById('studentFormAlert');
    if (el) el.classList.add('tecnm-hidden');
  }

  const tableBody = document.getElementById('studentsTableBody');
  const paginationContainer = document.getElementById('studentsPagination');
  const modal = document.getElementById('studentModal');
  const modalTitle = document.getElementById('modalTitle');
  const studentForm = document.getElementById('studentForm');

  const openCreateModalBtn = document.getElementById('openCreateModalBtn');
  const closeModalBtn = document.getElementById('closeModalBtn');

  if (openCreateModalBtn) {
    if (window.isReadOnlyUser && window.isReadOnlyUser()) {
      openCreateModalBtn.classList.add('tecnm-hidden');
    } else {
      openCreateModalBtn.addEventListener('click', () => openModal(false));
    }
  }

  const inputId = document.getElementById('studentId');
  const inputControlNumber = document.getElementById('controlNumber');
  const inputFirstName = document.getElementById('firstName');
  const inputLastName = document.getElementById('lastName');
  const inputEmail = document.getElementById('email');
  const inputCareerId = document.getElementById('careerId');
  const inputGpa = document.getElementById('gpa');

  function getHeaders() {
    const token = sessionStorage.getItem(STORAGE_KEY);
    const headers = { 'Content-Type': 'application/json' };
    if (token) headers['Authorization'] = `Bearer ${token}`;
    return headers;
  }

  const showAudit = window.canSeeAudit ? window.canSeeAudit() : false;
  const showActions = window.canManageRegistry ? window.canManageRegistry() : true;
  const colCount = 6;

  let studentsCache = [];

  window.openStudentAuditModal = (id) => {
    const s = studentsCache.find(item => item.id === id);
    if (!s || !window.showAuditModal) return;

    window.showAuditModal(`Auditoría — Estudiante ${s.controlNumber}`, [
      { label: 'ID', value: s.id },
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

  let pageNumber = 1;
  const pageSize = 10;
  let searchTerm = '';
  let sortBy = 'CreatedAt';
  let sortDir = 'desc';
  let includeInactive = false;

  window.bindTableSearch && window.bindTableSearch('studentSearchInput', (term) => {
    searchTerm = term;
    pageNumber = 1;
    loadStudents();
  });

  window.initSortableHeaders && window.initSortableHeaders('studentsTable', (field, dir) => {
    sortBy = field;
    sortDir = dir;
    pageNumber = 1;
    loadStudents();
  });

  const inactiveToggle = document.getElementById('studentsIncludeInactiveToggle');
  if (inactiveToggle) {
    inactiveToggle.addEventListener('change', () => {
      includeInactive = inactiveToggle.checked;
      pageNumber = 1;
      loadStudents();
    });
  }

  const exportStudentsBtn = document.getElementById('exportStudentsBtn');
  if (exportStudentsBtn && window.downloadPdf) {
    exportStudentsBtn.addEventListener('click', () => {
      const params = new URLSearchParams({ search: searchTerm, sortBy, sortDir, includeInactive });
      window.downloadPdf(`/api/v1/students/export?${params}`, 'estudiantes_tecnm.pdf');
    });
  }

  async function loadStudents() {
    try {
      const params = new URLSearchParams({ pageNumber, pageSize, search: searchTerm, sortBy, sortDir, includeInactive });
      const res = await fetch(`${API_URL}?${params}`, { headers: getHeaders() });
      if (!res.ok) throw new Error('Error al cargar lista de estudiantes desde la base de datos.');

      const data = await res.json();
      const students = (data && data.items) || [];

      if (window.canSeeAudit && window.canSeeAudit()) {
        await window.loadAuditUserNames(window.collectAuditUserIds(students));
      }

      if (students.length === 0 && pageNumber > 1 && data.totalPages > 0) {
        pageNumber = data.totalPages;
        return loadStudents();
      }

      if (includeInactive) {
        const hasInactive = students.some(s => s.isActive === false || s.is_active === false);
        if (!hasInactive) {
          showAlert('No existen registros inactivos.', 'info');
          const inactiveToggle = document.getElementById('studentsIncludeInactiveToggle');
          if (inactiveToggle) inactiveToggle.checked = false;
          includeInactive = false;
        }
      }

      renderStudents(students);
      window.renderPagination(paginationContainer, data, (page) => {
        pageNumber = page;
        loadStudents();
      });
    } catch (err) {
      tableBody.innerHTML = `<tr><td colspan="${colCount}" class="tecnm-table-empty tecnm-text-danger">${err.message}</td></tr>`;
      if (paginationContainer) paginationContainer.innerHTML = '';
    }
  }

  function renderStudents(students) {
    studentsCache = students || [];

    if (!students || students.length === 0) {
      tableBody.innerHTML = `<tr><td colspan="${colCount}" class="tecnm-table-empty">No hay estudiantes que coincidan con el filtro.</td></tr>`;
      return;
    }

    tableBody.innerHTML = students.map(s => {
      const badgeClass = s.isActive ? 'tecnm-badge-approved' : 'tecnm-badge-rejected';
      const statusText = s.isActive ? 'Activo' : 'Inactivo';

      return `
        <tr>
          <td><a href="/students/profile?id=${s.id}"><strong>${s.controlNumber}</strong></a></td>
          <td>${s.firstName} ${s.lastName}</td>
          <td>${s.email}</td>
          <td><strong>${s.gpa.toFixed(1)}</strong></td>
          <td><span class="tecnm-badge ${badgeClass}">${statusText}</span></td>
          ${showActions ? `
          <td class="tecnm-text-center">
            <button class="tecnm-btn tecnm-btn-secondary tecnm-btn-sm" onclick="editStudent(${s.id})">Editar</button>
            ${showAudit ? `<button class="tecnm-btn tecnm-btn-secondary tecnm-btn-sm" onclick="openStudentAuditModal(${s.id})">Auditoría</button>` : ''}
            ${s.isActive
              ? `<button class="tecnm-btn tecnm-btn-danger tecnm-btn-sm" onclick="deactivateStudent(${s.id})">Desactivar</button>`
              : `<button class="tecnm-btn tecnm-btn-success tecnm-btn-sm" onclick="reactivateStudent(${s.id})">Reactivar</button>`
            }
          </td>
          ` : ''}
        </tr>
      `;
    }).join('');
  }

  function openModal(editMode = false) {
    modalTitle.textContent = editMode ? 'Editar Estudiante' : 'Registrar Nuevo Estudiante';
    inputEmail.disabled = editMode;
    inputControlNumber.disabled = editMode;
    hideStudentFormError();
    modal.classList.add('active');
  }

  function closeModal() {
    modal.classList.remove('active');
    studentForm.reset();
    inputId.value = '';
    inputControlNumber.disabled = false;
    inputEmail.disabled = false;
    hideStudentFormError();
  }

  window.editStudent = async (id) => {
    try {
      const res = await fetch(`${API_URL}/${id}`, { headers: getHeaders() });
      if (!res.ok) throw new Error('Estudiante no encontrado');
      const student = await res.json();

      inputId.value = student.id;
      inputControlNumber.value = student.controlNumber;
      inputFirstName.value = student.firstName;
      inputLastName.value = student.lastName;
      inputEmail.value = student.email;
      inputCareerId.value = student.careerId;
      inputGpa.value = student.gpa;

      openModal(true);
    } catch (err) {
      showAlert(err.message, 'danger');
    }
  };

  window.deactivateStudent = async (id) => {
    const confirmed = await window.tecnmConfirm('¿Está seguro de desactivar a este estudiante del sistema de residencias?', 'Desactivar Estudiante');
    if (!confirmed) return;

    try {
      const res = await fetch(`${API_URL}/${id}`, {
        method: 'DELETE',
        headers: getHeaders()
      });
      if (!res.ok) throw new Error('Error al desactivar estudiante');

      showAlert('Estudiante desactivado correctamente.', 'success');
      loadStudents();
    } catch (err) {
      showAlert(err.message, 'danger');
    }
  };

  window.reactivateStudent = async (id) => {
    try {
      const res = await fetch(`${API_URL}/${id}/activate`, {
        method: 'PATCH',
        headers: getHeaders()
      });
      if (!res.ok) throw new Error('Error al reactivar estudiante');

      showAlert('Estudiante reactivado correctamente.', 'success');
      loadStudents();
    } catch (err) {
      showAlert(err.message, 'danger');
    }
  };

  studentForm.addEventListener('submit', async (e) => {
    e.preventDefault();
    const id = inputId.value;

    if (id) {
      // Update
      const dto = {
        firstName: inputFirstName.value.trim(),
        lastName: inputLastName.value.trim(),
        careerId: parseInt(inputCareerId.value, 10),
        gpa: parseFloat(inputGpa.value)
      };

      try {
        const res = await fetch(`${API_URL}/${id}`, {
          method: 'PUT',
          headers: getHeaders(),
          body: JSON.stringify(dto)
        });
        if (!res.ok) throw new Error('Error al actualizar estudiante');

        showAlert('Perfil de estudiante guardado exitosamente.', 'success');
        closeModal();
        loadStudents();
      } catch (err) {
        showAlert(err.message, 'danger');
      }
    } else {
      // Create
      const email = inputEmail.value.trim();
      if (!isInstitutionalEmail(email)) {
        showStudentFormError(INSTITUTIONAL_EMAIL_ERROR);
        inputEmail.focus();
        return;
      }

      const dto = {
        controlNumber: inputControlNumber.value.trim().toUpperCase(),
        firstName: inputFirstName.value.trim(),
        lastName: inputLastName.value.trim(),
        careerId: parseInt(inputCareerId.value, 10),
        email,
        gpa: parseFloat(inputGpa.value)
      };

      try {
        const res = await fetch(API_URL, {
          method: 'POST',
          headers: getHeaders(),
          body: JSON.stringify(dto)
        });
        if (!res.ok) {
          const errData = await res.json();
          throw new Error(errData.message || 'Error al crear estudiante');
        }

        showAlert('Perfil de estudiante guardado exitosamente.', 'success');
        closeModal();
        loadStudents();
      } catch (err) {
        showAlert(err.message, 'danger');
      }
    }
  });

  openCreateModalBtn.addEventListener('click', () => openModal(false));
  closeModalBtn.addEventListener('click', closeModal);
  inputEmail.addEventListener('input', hideStudentFormError);

  if (!showActions) {
    const createBtn = document.getElementById('openCreateModalBtn');
    if (createBtn) createBtn.remove();
  }

  const loadingRow = tableBody.querySelector('tr');
  if (loadingRow) loadingRow.colSpan = colCount;

  window.displaySingleStudent = (row) => {
    const clearBtn = document.getElementById('clearSearchFilterBtn');
    if (clearBtn) clearBtn.classList.remove('tecnm-hidden');

    if (paginationContainer) paginationContainer.innerHTML = '';

    const id = row.id;
    const controlNumber = row.control_number || row.controlNumber || '-';
    const fullName = row.full_name || row.fullName || '-';
    const email = row.email || '-';
    const isActive = row.is_active !== false;
    const badgeClass = isActive ? 'tecnm-badge-approved' : 'tecnm-badge-rejected';
    const statusText = isActive ? 'Activo' : 'Inactivo';

    tableBody.innerHTML = `
      <tr>
        <td><a href="/students/profile?id=${id}"><strong>${controlNumber}</strong></a></td>
        <td>${fullName}</td>
        <td>${email}</td>
        <td><strong>-</strong></td>
        <td><span class="tecnm-badge ${badgeClass}">${statusText}</span></td>
        ${showActions ? `
        <td class="tecnm-text-center">
          <button class="tecnm-btn tecnm-btn-secondary tecnm-btn-sm" onclick="editStudent(${id})">Editar</button>
        </td>
        ` : ''}
      </tr>
    `;
  };

  window.clearModuleSearchFilter = () => {
    const clearBtn = document.getElementById('clearSearchFilterBtn');
    if (clearBtn) clearBtn.classList.add('tecnm-hidden');
    loadStudents();
  };

  loadStudents();
})();
