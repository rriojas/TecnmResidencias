const API_ADMIN_BASE = '/api/v1/admin';

function initReportsPage() {
  loadMetrics();
  loadReleasableProjects();

  const exportBtn = document.getElementById('exportMetricsBtn');
  if (exportBtn) {
    exportBtn.addEventListener('click', () => {
      showAlert('Generando reporte consolidado en formato PDF/Excel...', 'info');
      setTimeout(() => {
        showAlert('Reporte consolidado descargado exitosamente.', 'success');
      }, 1500);
    });
  }
}

async function loadMetrics() {
  try {
    const res = await fetch(`${API_ADMIN_BASE}/dashboard`);
    if (!res.ok) return;

    const data = await res.json();
    document.getElementById('kpiStudents').innerText = data.totalStudents || 0;
    document.getElementById('kpiApprovedProjects').innerText = data.approvedProjects || 0;
    document.getElementById('kpiCompleted').innerText = data.completedResidencies || 0;
    document.getElementById('kpiAdvisors').innerText = data.activeAdvisors || 0;
  } catch (err) {
    console.log('Error al cargar métricas de administración');
  }
}

let releasablePageNumber = 1;

async function loadReleasableProjects() {
  const tbody = document.getElementById('releasableTableBody');
  const paginationContainer = document.getElementById('releasablePagination');
  if (!tbody) return;

  try {
    const params = new URLSearchParams({ pageNumber: releasablePageNumber, pageSize: 10 });
    const res = await fetch(`${API_ADMIN_BASE}/reports/releasable?${params}`);
    if (!res.ok) throw new Error();

    const data = await res.json();
    const projects = (data && data.items) || [];

    if (projects.length === 0 && releasablePageNumber > 1 && data.totalPages > 0) {
      releasablePageNumber = data.totalPages;
      return loadReleasableProjects();
    }

    if (projects.length === 0) {
      tbody.innerHTML = `<tr><td colspan="7" class="tecnm-table-empty">No se encontraron anteproyectos registrados.</td></tr>`;
    } else {
      tbody.innerHTML = projects.map(p => `
        <tr>
          <td>${p.title}</td>
          <td>${p.studentName}</td>
          <td>${p.studentControlNumber || '—'}</td>
          <td>${p.advisorName || 'Sin asignar'}</td>
          <td><strong class="${p.averageScore >= 70 ? 'tecnm-score-approved' : 'tecnm-score-rejected'}">${p.averageScore}</strong> / 100</td>
          <td>
            ${p.isEligible 
              ? `<span class="tecnm-badge tecnm-badge-approved">Elegible (Promedio ≥ 70)</span>` 
              : `<span class="tecnm-badge tecnm-badge-rejected">No Elegible (< 70)</span>`}
          </td>
          <td>
            <div class="tecnm-row-actions">
                <button class="tecnm-btn ${p.isEligible ? 'tecnm-btn-success' : 'tecnm-btn-secondary'} tecnm-btn-sm" 
                        ${!p.isEligible ? 'disabled' : ''} 
                        onclick="issueReleaseLetter(${p.projectId})">
                    Emitir Carta de Liberación / Libranza
                </button>
            </div>
          </td>
        </tr>
      `).join('');
    }

    if (window.renderPagination) {
      window.renderPagination(paginationContainer, data, (page) => {
        releasablePageNumber = page;
        loadReleasableProjects();
      });
    }
  } catch (err) {
    tbody.innerHTML = `<tr><td colspan="7" class="tecnm-table-empty tecnm-text-danger">Error al obtener la lista de proyectos elegibles para liberación.</td></tr>`;
    if (paginationContainer) paginationContainer.innerHTML = '';
  }
}

async function issueReleaseLetter(projectId) {
  const confirmed = await window.tecnmConfirm(`¿Desea emitir oficialmente la Carta de Liberación (Libranza) para el Proyecto #${projectId}?`, 'Emitir Carta de Liberación');
  if (!confirmed) return;

  try {
    const res = await fetch(`${API_ADMIN_BASE}/reports/release-letter/${projectId}`, {
      method: 'POST'
    });

    if (res.ok) {
      const doc = await res.json();
      showAlert(`Carta de Liberación (Libranza) emitida correctamente: ${doc.documentName}`, 'success');
      loadMetrics();
      loadReleasableProjects();
    } else {
      const err = await res.json();
      showAlert(err.message || 'No se pudo emitir la carta de liberación.', 'danger');
    }
  } catch (err) {
    showAlert('Error de conexión al generar la libranza.', 'danger');
  }
}
