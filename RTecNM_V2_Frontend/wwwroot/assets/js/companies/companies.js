const API_COMPANIES_BASE = '/api/v1/companies';

let companiesCache = [];
let editingCompanyId = null;
let companyIncludeInactive = false;

function initCompaniesPage() {
  setupCompanyGate();
  loadCompaniesList();

  const openModalBtn = document.getElementById('openCompanyModalBtn');
  const modal = document.getElementById('companyModal');
  const closeModalBtn = document.getElementById('closeCompanyModalBtn');
  const cancelModalBtn = document.getElementById('cancelCompanyModalBtn');
  const form = document.getElementById('companyForm');

  const hideModal = () => {
    if (modal) modal.classList.remove('active');
    resetCompanyFormState();
  };

  if (openModalBtn && modal) {
    openModalBtn.addEventListener('click', () => {
      resetCompanyFormState();
      document.getElementById('companyModalTitle').textContent = 'Registrar Nueva Empresa Receptora';
      modal.classList.add('active');
    });
  }

  if (closeModalBtn) closeModalBtn.addEventListener('click', hideModal);
  if (cancelModalBtn) cancelModalBtn.addEventListener('click', hideModal);

  if (form) {
    form.addEventListener('submit', handleCompanySubmit);
  }

  const refreshBtn = document.getElementById('refreshCompaniesBtn');
  if (refreshBtn) {
    refreshBtn.addEventListener('click', loadCompaniesList);
  }

  const inactiveToggle = document.getElementById('companyIncludeInactiveToggle');
  if (inactiveToggle) {
    inactiveToggle.addEventListener('change', () => {
      companyIncludeInactive = inactiveToggle.checked;
      loadCompaniesList();
    });
  }
}

function setupCompanyGate() {
  const openModalBtn = document.getElementById('openCompanyModalBtn');
  if (!openModalBtn) return;

  const canEditCompanies = window.hasRole ? window.hasRole('admin', 'vinculacion') : false;
  if (canEditCompanies) {
    openModalBtn.classList.remove('tecnm-hidden');
  } else {
    openModalBtn.classList.add('tecnm-hidden');
  }
}

function resetCompanyFormState() {
  editingCompanyId = null;
  const form = document.getElementById('companyForm');
  if (form) form.reset();
}

async function loadCompaniesList() {
  const tableBody = document.getElementById('companiesTableBody');
  if (!tableBody) return;

  try {
    const res = await fetch(`${API_COMPANIES_BASE}?activeOnly=${!companyIncludeInactive}`);
    if (!res.ok) {
      if (res.status === 403) throw new Error('No tiene permisos para consultar la lista de empresas.');
      throw new Error('Error al cargar catálogo de empresas.');
    }

    const data = await res.json();
    companiesCache = data || [];

    if (window.loadAuditUserNames && window.collectAuditUserIds) {
      await window.loadAuditUserNames(window.collectAuditUserIds(data));
    }

    renderCompaniesTable(companiesCache);
  } catch (err) {
    tableBody.innerHTML = `<tr><td colspan="7" class="tecnm-table-empty tecnm-text-danger">${escapeHtml(err.message)}</td></tr>`;
  }
}

function renderCompaniesTable(companies) {
  const tableBody = document.getElementById('companiesTableBody');
  if (!tableBody) return;

  const canEdit = window.hasRole ? window.hasRole('admin', 'vinculacion') : false;
  const canSeeAudit = window.canSeeAudit ? window.canSeeAudit() : false;

  if (companies.length === 0) {
    tableBody.innerHTML = '<tr><td colspan="7" class="tecnm-table-empty">No hay empresas receptoras registradas.</td></tr>';
    return;
  }

  tableBody.innerHTML = companies.map(c => {
    const statusBadge = c.isActive
      ? '<span class="tecnm-badge tecnm-badge-success">Activa</span>'
      : '<span class="tecnm-badge tecnm-badge-danger">Inactiva</span>';

    const actions = [];
    if (canEdit) {
      actions.push(`<button type="button" class="tecnm-btn tecnm-btn-secondary tecnm-btn-sm" onclick="openCompanyEditModal(${c.id})">Editar</button>`);
      if (c.isActive) {
        actions.push(`<button type="button" class="tecnm-btn tecnm-btn-danger tecnm-btn-sm" onclick="softDeleteCompany(${c.id})">Desactivar</button>`);
      } else {
        actions.push(`<button type="button" class="tecnm-btn tecnm-btn-success tecnm-btn-sm" onclick="reactivateCompany(${c.id})">Reactivar</button>`);
      }
    } else {
      actions.push('<span class="tecnm-text-muted">Lectura únicamente</span>');
    }
    if (canSeeAudit) {
      actions.push(`<button type="button" class="tecnm-btn tecnm-btn-secondary tecnm-btn-sm" onclick="openCompanyAuditModal(${c.id})">Auditoría</button>`);
    }

    return `
      <tr>
        <td><strong>${escapeHtml(c.name)}</strong></td>
        <td><code>${escapeHtml(c.rfc || '—')}</code></td>
        <td>${escapeHtml(c.sector || '—')}</td>
        <td>${escapeHtml(c.contactName)}</td>
        <td>
          <div>${escapeHtml(c.contactEmail)}</div>
          <small class="tecnm-text-muted">${escapeHtml(c.contactPhone || '—')}</small>
        </td>
        <td>${statusBadge}</td>
        <td><div class="tecnm-row-actions">${actions.join('')}</div></td>
      </tr>
    `;
  }).join('');
}

async function handleCompanySubmit(e) {
  e.preventDefault();

  const name = document.getElementById('companyNameInput').value.trim();
  const rfc = document.getElementById('companyRfcInput').value.trim().toUpperCase();
  const sector = document.getElementById('companySectorInput').value.trim();
  const address = document.getElementById('companyAddressInput').value.trim();
  const contactName = document.getElementById('companyContactNameInput').value.trim();
  const contactEmail = document.getElementById('companyContactEmailInput').value.trim();
  const contactPhone = document.getElementById('companyContactPhoneInput').value.trim();

  const payload = { name, rfc, sector, address, contactName, contactEmail, contactPhone };
  const isEdit = editingCompanyId !== null;

  const submitBtn = document.getElementById('submitCompanyBtn');
  submitBtn.disabled = true;
  submitBtn.textContent = 'Guardando...';

  try {
    const endpoint = isEdit ? `${API_COMPANIES_BASE}/${editingCompanyId}` : API_COMPANIES_BASE;
    const res = await fetch(endpoint, {
      method: isEdit ? 'PUT' : 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(payload)
    });

    const data = await res.json();
    if (!res.ok) throw new Error(data.message || 'Error al guardar empresa.');

    showAlert(`Empresa ${isEdit ? 'actualizada' : 'registrada'} correctamente.`, 'success');
    const modal = document.getElementById('companyModal');
    if (modal) modal.classList.remove('active');
    resetCompanyFormState();
    loadCompaniesList();
  } catch (err) {
    showAlert(err.message, 'danger');
  } finally {
    submitBtn.disabled = false;
    submitBtn.textContent = 'Guardar Empresa';
  }
}

function openCompanyEditModal(id) {
  const company = companiesCache.find(c => c.id === id);
  if (!company) return;

  editingCompanyId = id;
  document.getElementById('companyModalTitle').textContent = `Editar Empresa #${company.id}`;
  document.getElementById('companyNameInput').value = company.name || '';
  document.getElementById('companyRfcInput').value = company.rfc || '';
  document.getElementById('companySectorInput').value = company.sector || '';
  document.getElementById('companyAddressInput').value = company.address || '';
  document.getElementById('companyContactNameInput').value = company.contactName || '';
  document.getElementById('companyContactEmailInput').value = company.contactEmail || '';
  document.getElementById('companyContactPhoneInput').value = company.contactPhone || '';

  const modal = document.getElementById('companyModal');
  if (modal) modal.classList.add('active');
}

function openCompanyAuditModal(id) {
  const company = companiesCache.find(c => c.id === id);
  if (!company || !window.showAuditModal) return;

  window.showAuditModal(`Auditoría - Empresa ${company.name}`, [
    { label: 'ID', value: company.id },
    { label: 'RFC', value: company.rfc },
    { label: 'Estado', value: company.isActive ? 'Activa' : 'Inactiva' },
    { label: 'Visible', value: company.isVisible ? 'Sí' : 'No' },
    { label: 'Orden', value: company.displayOrder },
    { label: 'Creado el', value: window.formatAuditDate(company.createdAt) },
    { label: 'Creado por', value: window.formatAuditUser(company.createdBy) },
    { label: 'Actualizado el', value: company.updatedBy ? window.formatAuditDate(company.updatedAt) : '—' },
    { label: 'Actualizado por', value: company.updatedBy ? window.formatAuditUser(company.updatedBy) : '—' },
    { label: 'Eliminado el', value: company.deletedAt ? window.formatAuditDate(company.deletedAt) : '—' },
    { label: 'Eliminado por', value: company.deletedBy ? window.formatAuditUser(company.deletedBy) : '—' }
  ]);
}

async function softDeleteCompany(id) {
  if (!confirm('¿Desea desactivar esta empresa receptora?')) return;
  try {
    const res = await fetch(`${API_COMPANIES_BASE}/${id}`, { method: 'DELETE' });
    const data = await res.json();
    if (!res.ok) throw new Error(data.message || 'Error al desactivar empresa.');

    showAlert('Empresa desactivada correctamente.', 'success');
    loadCompaniesList();
  } catch (err) {
    showAlert(err.message, 'danger');
  }
}

async function reactivateCompany(id) {
  try {
    const res = await fetch(`${API_COMPANIES_BASE}/${id}/activate`, { method: 'PATCH' });
    const data = await res.json();
    if (!res.ok) throw new Error(data.message || 'Error al reactivar empresa.');

    showAlert('Empresa reactivada correctamente.', 'success');
    loadCompaniesList();
  } catch (err) {
    showAlert(err.message, 'danger');
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
