function showAlert(message, type = 'info', autoDismissMs = 6000) {
  let container = document.getElementById('alertContainer');
  if (!container) {
    container = document.createElement('div');
    container.id = 'alertContainer';
    document.body.appendChild(container);
  }

  const alertClass = type === 'success' ? 'tecnm-alert-success' :
                     type === 'danger' ? 'tecnm-alert-danger' :
                     type === 'warning' ? 'tecnm-alert-warning' : 'tecnm-alert-info';

  const alertId = 'alert_' + Date.now() + '_' + Math.floor(Math.random() * 1000);

  const alertDiv = document.createElement('div');
  alertDiv.id = alertId;
  alertDiv.className = `tecnm-alert ${alertClass}`;
  alertDiv.style.display = 'flex';
  alertDiv.style.alignItems = 'center';
  alertDiv.style.justifyContent = 'space-between';
  alertDiv.style.gap = '0.75rem';

  alertDiv.innerHTML = `
    <span style="flex-grow: 1;">${message}</span>
    <button type="button" class="tecnm-alert-close" aria-label="Cerrar">&times;</button>
  `;

  const closeBtn = alertDiv.querySelector('.tecnm-alert-close');
  if (closeBtn) {
    closeBtn.addEventListener('click', () => {
      alertDiv.remove();
    });
  }

  container.appendChild(alertDiv);

  if (autoDismissMs > 0) {
    setTimeout(() => {
      const el = document.getElementById(alertId);
      if (el) el.remove();
    }, autoDismissMs);
  }
}

function getBadgeHtml(status) {
  const s = (status || '').toLowerCase();
  if (s === 'draft' || s === 'borrador') {
    return `<span class="tecnm-badge tecnm-badge-neutral">Borrador</span>`;
  } else if (s === 'approved' || s === 'aprobado') {
    return `<span class="tecnm-badge tecnm-badge-approved">Aprobado</span>`;
  } else if (s === 'in_progress' || s === 'inprogress' || s === 'en_progreso') {
    return `<span class="tecnm-badge tecnm-badge-approved">En Progreso</span>`;
  } else if (s === 'completed' || s === 'completado') {
    return `<span class="tecnm-badge tecnm-badge-approved">Completado</span>`;
  } else if (s === 'rejected' || s === 'rechazado') {
    return `<span class="tecnm-badge tecnm-badge-rejected">Correcciones Requeridas</span>`;
  } else if (s === 'cancelled' || s === 'cancelado') {
    return `<span class="tecnm-badge tecnm-badge-rejected">Cancelado</span>`;
  } else {
    return `<span class="tecnm-badge tecnm-badge-pending">En Revisión</span>`;
  }
}
