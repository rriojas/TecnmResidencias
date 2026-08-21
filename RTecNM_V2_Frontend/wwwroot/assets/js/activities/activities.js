async function selectProjectForSchedule(project) {
  if (!project || !project.id) return;
  currentProjectId = project.id;

  const title = project.title || project.titleText || project.name || 'Anteproyecto';
  const studentName = project.studentName || project.student_name || project.student || '';
  const controlNumber = project.studentControlNumber || project.student_control_number || project.controlNumber || '';
  const studentText = studentName ? ` (Alumno: ${escapeHtml(studentName)}${controlNumber ? ' - ' + escapeHtml(controlNumber) : ''})` : '';

  const badge = document.getElementById('selectedProjectBadge');
  if (badge) {
    badge.innerHTML = `<strong>${escapeHtml(title)}</strong>${studentText}`;
  }

  loadSchedule();

  if (!studentName || !controlNumber) {
    try {
      const res = await fetch(`/api/v1/projects/${project.id}`, { headers: getAuthHeaders() });
      if (res.ok) {
        const fullProj = await res.json();
        const fStudent = fullProj.studentName || fullProj.student_name || '';
        const fCtrl = fullProj.studentControlNumber || fullProj.student_control_number || '';
        if (fStudent) {
          const enrichedText = ` (Alumno: ${escapeHtml(fStudent)}${fCtrl ? ' - ' + escapeHtml(fCtrl) : ''})`;
          if (badge) badge.innerHTML = `<strong>${escapeHtml(fullProj.title || title)}</strong>${enrichedText}`;
        }
      }
    } catch (_) {}
  }
}

window.selectProjectForSchedule = selectProjectForSchedule;

async function initSchedulePage() {
  const isStudent = window.hasRole && window.hasRole('student') && !window.hasRole('admin', 'departmenthead', 'advisor');
  const isAdvisor = window.hasRole && window.hasRole('advisor') && !window.hasRole('admin', 'departmenthead');

  const searchBtn = document.getElementById('searchProjectBtn');

  if (isStudent) {
    if (searchBtn) searchBtn.style.display = 'none';
    resolveCurrentProject().then((project) => {
      if (project) {
        selectProjectForSchedule(project);
      }
    });
  } else {
    if (searchBtn) {
      searchBtn.style.display = 'inline-flex';
      searchBtn.addEventListener('click', () => {
        if (window.openGlobalSearch) {
          window.openGlobalSearch({
            initialSource: 'PROJECTS',
            onSelect: (item) => {
              if (item && item.id) {
                selectProjectForSchedule(item);
              }
            }
          });
        }
      });
    }
    loadInitialProjectForStaff(isAdvisor);
  }

  const addBtn = document.getElementById('addActivityBtn');
  if (addBtn && isAdvisor) {
    addBtn.style.display = 'none';
  }

  const modal = document.getElementById('createActivityModal');
  const closeBtn = document.getElementById('closeActivityModalBtn');
  const cancelBtn = document.getElementById('cancelActivityModalBtn');
  const form = document.getElementById('activityForm');

  if (addBtn && modal && addBtn.style.display !== 'none') {
    addBtn.addEventListener('click', () => {
      if (!currentProjectId) {
        showAlert('Debe seleccionar o registrar un anteproyecto activo primero.', 'warning');
        return;
      }
      modal.classList.add('active');
    });
  }

  const hideModal = () => {
    if (modal) modal.classList.remove('active');
    if (form) form.reset();
  };

  if (closeBtn) closeBtn.addEventListener('click', hideModal);
  if (cancelBtn) cancelBtn.addEventListener('click', hideModal);

  if (form) {
    form.addEventListener('submit', handleAddActivitySubmit);
  }
}

function getAuthHeaders() {
  const headers = { 'Content-Type': 'application/json' };
  const token = sessionStorage.getItem('authToken');
  if (token) headers['Authorization'] = `Bearer ${token}`;
  return headers;
}

async function loadInitialProjectForStaff(isAdvisor) {
  const badge = document.getElementById('selectedProjectBadge');
  const tbody = document.getElementById('scheduleTableBody');

  try {
    const endpoint = isAdvisor ? '/api/v1/projects/advisor/me?pageSize=50' : '/api/v1/projects?pageSize=50';
    const res = await fetch(endpoint, { headers: getAuthHeaders() });
    if (!res.ok) throw new Error();

    const rawData = await res.json();
    let projects = Array.isArray(rawData) ? rawData : (rawData && Array.isArray(rawData.items) ? rawData.items : []);
    projects = projects.filter(p => (p.status || '').toLowerCase() !== 'draft');

    if (projects.length === 0) {
      if (badge) badge.innerText = 'Sin anteproyectos asignados';
      if (tbody) tbody.innerHTML = `<tr><td colspan="28" class="tecnm-table-empty">No se encontraron anteproyectos asignados. Utilice el botón "Buscar Anteproyecto" para seleccionar uno.</td></tr>`;
      currentProjectId = null;
      return;
    }

    selectProjectForSchedule(projects[0]);
  } catch {
    if (badge) badge.innerText = 'Seleccione un anteproyecto';
    if (tbody) tbody.innerHTML = `<tr><td colspan="28" class="tecnm-table-empty">Haga clic en "Buscar Anteproyecto" para cargar un cronograma.</td></tr>`;
    currentProjectId = null;
  }
}

async function resolveCurrentProject() {
  const tbody = document.getElementById('scheduleTableBody');
  const addBtn = document.getElementById('addActivityBtn');

  try {
    const res = await fetch('/api/v1/projects/me/current', { headers: getAuthHeaders() });

    if (res.status === 404) {
      if (tbody) {
        tbody.innerHTML = `<tr><td colspan="28" class="tecnm-table-empty">No tienes un proyecto aprobado o en curso. Registra tu solicitud de anteproyecto para generar tu cronograma de actividades.</td></tr>`;
      }
      if (addBtn) addBtn.disabled = true;
      return null;
    }

    if (res.status === 403) {
      if (tbody) {
        tbody.innerHTML = `<tr><td colspan="28" class="tecnm-table-empty">El cronograma personal es exclusivo para estudiantes con un proyecto vigente.</td></tr>`;
      }
      if (addBtn) addBtn.disabled = true;
      return null;
    }

    if (!res.ok) throw new Error();

    const project = await res.json();
    if (!project || !project.id) throw new Error();

    currentProjectId = project.id;
    if (addBtn) addBtn.disabled = false;
    return project;
  } catch (err) {
    if (tbody) {
      tbody.innerHTML = `<tr><td colspan="28" class="tecnm-table-empty tecnm-text-danger">Error al cargar el cronograma de actividades desde la base de datos.</td></tr>`;
    }
    if (addBtn) addBtn.disabled = true;
    return null;
  }
}

async function loadSchedule() {
  const tbody = document.getElementById('scheduleTableBody');
  if (!tbody) return;

  if (!currentProjectId) {
    tbody.innerHTML = `<tr><td colspan="28" class="tecnm-table-empty">No hay anteproyectos registrados para mostrar el cronograma.</td></tr>`;
    return;
  }

  tbody.innerHTML = `<tr><td colspan="28" class="tecnm-table-empty">Cargando cronograma de actividades...</td></tr>`;

  try {
    const res = await fetch(`/api/v1/projects/${currentProjectId}/activities`);
    if (!res.ok) throw new Error();

    const activities = await res.json();
    if (!Array.isArray(activities) || activities.length === 0) {
      tbody.innerHTML = `<tr><td colspan="28" class="tecnm-table-empty">No hay actividades registradas en el cronograma. Haga clic en "+ Nueva Actividad".</td></tr>`;
      return;
    }

    tbody.innerHTML = activities.map(act => {
      let weekCells = '';
      for (let w = 1; w <= 26; w++) {
        const prog = (act.progresses || []).find(p => p.weekNumber === w) || { status: 'pending' };
        const statusClass = getStatusClass(prog.status);
        const statusLabel = getStatusSymbol(prog.status);
        
        weekCells += `
          <td class="week-cell ${statusClass}" 
              data-activity-id="${act.id}" 
              data-week="${w}" 
              data-status="${prog.status}"
              title="Actividad: ${escapeHtml(act.title)} - Semana ${w} (${prog.status})"
              onclick="cycleWeekStatus(this)">
            ${statusLabel}
          </td>
        `;
      }

      return `
        <tr>
          <td><strong>${act.activityNumber}</strong></td>
          <td class="act-title-col">${escapeHtml(act.title)}</td>
          ${weekCells}
        </tr>
      `;
    }).join('');

  } catch (err) {
    tbody.innerHTML = `<tr><td colspan="28" class="tecnm-table-empty tecnm-text-danger">Error al cargar el cronograma de actividades desde la base de datos.</td></tr>`;
  }
}

function getStatusClass(status) {
  const s = (status || '').toLowerCase();
  if (s === 'completed' || s === 'completado') return 'completed';
  if (s === 'in_progress' || s === 'en_proceso') return 'in_progress';
  return 'pending';
}

function getStatusSymbol(status) {
  const s = (status || '').toLowerCase();
  if (s === 'completed' || s === 'completado') return '✓';
  if (s === 'in_progress' || s === 'en_proceso') return '•';
  return '';
}

async function cycleWeekStatus(cell) {
  if (!currentProjectId) return;

  const activityId = parseInt(cell.getAttribute('data-activity-id'), 10);
  const weekNumber = parseInt(cell.getAttribute('data-week'), 10);
  const currentStatus = cell.getAttribute('data-status') || 'pending';

  let nextStatus = 'in_progress';
  if (currentStatus === 'in_progress') nextStatus = 'completed';
  else if (currentStatus === 'completed') nextStatus = 'pending';

  cell.className = `week-cell ${getStatusClass(nextStatus)}`;
  cell.setAttribute('data-status', nextStatus);
  cell.innerText = getStatusSymbol(nextStatus);

  try {
    const res = await fetch(`/api/v1/projects/${currentProjectId}/activities/progress`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        activityId,
        weekNumber,
        status: nextStatus
      })
    });

    if (res.ok) {
      showAlert(`Avance actualizado: Semana ${weekNumber} (${nextStatus.toUpperCase()}).`, 'success');
    } else {
      showAlert('Error al actualizar el avance semanal.', 'danger');
    }
  } catch (err) {
    showAlert('Error de conexión al servidor.', 'danger');
  }
}

async function handleAddActivitySubmit(e) {
  e.preventDefault();
  if (!currentProjectId) {
    showAlert('Debe seleccionar un anteproyecto válido.', 'warning');
    return;
  }

  const input = document.getElementById('activityTitleInput');
  const plannedWeeksInput = document.getElementById('plannedWeeksInput');

  const title = input ? input.value.trim() : '';
  const plannedWeeks = plannedWeeksInput ? parseInt(plannedWeeksInput.value, 10) : 2;

  if (!title) {
    showAlert('Ingrese una descripción válida para la actividad.', 'warning');
    return;
  }

  const currentRows = document.querySelectorAll('#scheduleTableBody tr:not(.tecnm-table-empty)').length;
  const activityNumber = currentRows + 1;

  const submitBtn = document.getElementById('submitActivityBtn');
  if (submitBtn) {
    submitBtn.disabled = true;
    submitBtn.textContent = 'Guardando...';
  }

  try {
    const res = await fetch(`/api/v1/projects/${currentProjectId}/activities`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        projectId: currentProjectId,
        activityNumber,
        title,
        plannedWeeks
      })
    });

    if (res.ok) {
      showAlert('¡Actividad agregada correctamente al cronograma!', 'success');
      const modal = document.getElementById('createActivityModal');
      if (modal) modal.classList.remove('active');
      document.getElementById('activityForm').reset();
      loadSchedule();
    } else {
      const err = await res.json();
      showAlert(err.message || 'Error al agregar la actividad.', 'danger');
    }
  } catch (err) {
    showAlert('Error de comunicación con el servidor.', 'danger');
  } finally {
    if (submitBtn) {
      submitBtn.disabled = false;
      submitBtn.textContent = 'Guardar Actividad';
    }
  }
}

function escapeHtml(text) {
  if (!text) return '';
  return text.toString()
    .replace(/&/g, "&amp;")
    .replace(/</g, "&lt;")
    .replace(/>/g, "&gt;")
    .replace(/"/g, "&quot;")
    .replace(/'/g, "&#039;");
}
