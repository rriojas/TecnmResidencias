document.addEventListener('DOMContentLoaded', () => {
    const tableBody = document.getElementById('advisorsTableBody');
    const paginationContainer = document.getElementById('advisorsPagination');
    const form = document.getElementById('advisorForm');
    const reloadBtn = document.getElementById('reloadBtn');

    const openModalBtn = document.getElementById('openAdvisorModalBtn');
    const modal = document.getElementById('createAdvisorModal');
    const cancelModalBtn = document.getElementById('cancelAdvisorModalBtn');
    const advisorIdInput = document.getElementById('advisorId');

    if (openModalBtn && modal) {
        if (window.isReadOnlyUser && window.isReadOnlyUser()) {
            openModalBtn.classList.add('tecnm-hidden');
        } else {
            openModalBtn.addEventListener('click', () => {
                openCreateModal();
            });
        }
    }

    const showAudit = window.canSeeAudit ? window.canSeeAudit() : false;
    const showActions = window.canManageRegistry ? window.canManageRegistry() : true;
    const colCount = 6;

    let advisorsCache = [];

    window.openAdvisorAuditModal = (id) => {
        const a = advisorsCache.find(item => item.id === id);
        if (!a || !window.showAuditModal) return;

        window.showAuditModal(`Auditoría — Asesor #${a.id} (${a.fullName})`, [
            { label: 'ID', value: a.id },
            { label: 'Estado', value: a.isActive ? 'Activo' : 'Inactivo' },
            { label: 'Visible', value: a.isVisible ? 'Sí' : 'No' },
            { label: 'Orden', value: a.displayOrder },
            { label: 'Creado el', value: window.formatAuditDate(a.createdAt) },
            { label: 'Creado por', value: window.formatAuditUser(a.createdBy) },
            { label: 'Actualizado el', value: a.updatedBy ? window.formatAuditDate(a.updatedAt) : '—' },
            { label: 'Actualizado por', value: a.updatedBy ? window.formatAuditUser(a.updatedBy) : '—' },
            { label: 'Eliminado el', value: a.deletedAt ? window.formatAuditDate(a.deletedAt) : '—' },
            { label: 'Eliminado por', value: a.deletedBy ? window.formatAuditUser(a.deletedBy) : '—' }
        ]);
    };

    let pageNumber = 1;
    const pageSize = 10;
    let searchTerm = '';
    let sortBy = 'CreatedAt';
    let sortDir = 'desc';
    let includeInactive = false;

    if (window.bindTableSearch) {
        window.bindTableSearch('advisorSearchInput', (term) => {
            searchTerm = term;
            pageNumber = 1;
            loadAdvisors();
        });
    }

    if (window.initSortableHeaders) {
        window.initSortableHeaders('advisorsTable', (field, dir) => {
            sortBy = field;
            sortDir = dir;
            pageNumber = 1;
            loadAdvisors();
        });
    }

    const inactiveToggle = document.getElementById('advisorsIncludeInactiveToggle');
    if (inactiveToggle) {
        inactiveToggle.addEventListener('change', () => {
            includeInactive = inactiveToggle.checked;
            pageNumber = 1;
            loadAdvisors();
        });
    }

    const exportAdvisorsBtn = document.getElementById('exportAdvisorsBtn');
    if (exportAdvisorsBtn && window.downloadPdf) {
        exportAdvisorsBtn.addEventListener('click', () => {
            const params = new URLSearchParams({ search: searchTerm, sortBy, sortDir, includeInactive });
            window.downloadPdf(`/api/v1/advisors/export?${params}`, 'asesores_tecnm.pdf');
        });
    }

    if (!showActions && openModalBtn) {
        openModalBtn.remove();
    }

    const loadingRow = tableBody.querySelector('tr');
    if (loadingRow) loadingRow.colSpan = colCount;

    let advisorUserAutocomplete = null;
    if (window.initTecNMAutocomplete && document.getElementById('advisorUserAutocompleteWrapper')) {
        advisorUserAutocomplete = window.initTecNMAutocomplete({
            containerId: 'advisorUserAutocompleteWrapper',
            hiddenInputId: 'userId',
            placeholder: 'Buscar cuenta de usuario por correo o nombre...',
            searchFn: async (query) => {
                try {
                    const res = await fetch('/api/v1/roles/users/options');
                    if (!res.ok) return [];
                    const all = await res.json();
                    const q = (query || '').toLowerCase();
                    return (all || []).filter(u =>
                        (u.email && u.email.toLowerCase().includes(q)) ||
                        (u.name && u.name.toLowerCase().includes(q))
                    );
                } catch {
                    return [];
                }
            },
            titleExtractor: (u) => u.email || `Usuario #${u.id}`,
            subtitleExtractor: (u) => u.name ? `Nombre: ${u.name}` : ''
        });
    }

    const hideModal = () => {
        if (modal) modal.classList.remove('active');
        if (form) form.reset();
        if (advisorIdInput) advisorIdInput.value = '';
        if (advisorUserAutocomplete) advisorUserAutocomplete.clear();
        const userGroup = document.getElementById('advisorUserFormGroup');
        if (userGroup) userGroup.classList.remove('tecnm-hidden');
        const modalTitle = document.getElementById('advisorModalTitle');
        if (modalTitle) modalTitle.textContent = 'Registrar Nuevo Asesor';
    };

    function openCreateModal() {
        if (advisorIdInput) advisorIdInput.value = '';
        const modalTitle = document.getElementById('advisorModalTitle');
        if (modalTitle) modalTitle.textContent = 'Registrar Nuevo Asesor';

        if (advisorUserAutocomplete) advisorUserAutocomplete.clear();
        const userGroup = document.getElementById('advisorUserFormGroup');
        if (userGroup) userGroup.classList.remove('tecnm-hidden');
        document.getElementById('userId')?.setAttribute('required', 'required');

        if (modal) modal.classList.add('active');
    }

    window.openEditAdvisorModal = async function(id) {
        try {
            const response = await fetch(`/api/v1/advisors/${id}`);
            if (!response.ok) throw new Error('Error al obtener el asesor');
            const advisor = await response.json();

            if (advisorIdInput) advisorIdInput.value = advisor.id;
            const modalTitle = document.getElementById('advisorModalTitle');
            if (modalTitle) modalTitle.textContent = 'Editar Asesor';

            document.getElementById('fullName').value = advisor.fullName || '';
            document.getElementById('title').value = advisor.title || '';
            document.getElementById('phone').value = advisor.phone || '';
            document.getElementById('departmentId').value = String(advisor.departmentId);
            document.getElementById('advisorType').value = advisor.advisorType === 'internal' ? '1' : '2';

            const userGroup = document.getElementById('advisorUserFormGroup');
            if (userGroup) userGroup.classList.add('tecnm-hidden');
            document.getElementById('userId')?.removeAttribute('required');

            if (modal) modal.classList.add('active');
        } catch (err) {
            showAlert('No se pudo cargar el asesor solicitado.', 'danger');
        }
    };

    if (cancelModalBtn) cancelModalBtn.addEventListener('click', hideModal);

    async function loadAdvisors() {
        try {
            const params = new URLSearchParams({ pageNumber, pageSize, search: searchTerm, sortBy, sortDir, includeInactive });
            const response = await fetch(`/api/v1/advisors?${params}`);
            if (!response.ok) throw new Error('Error al obtener asesores de la base de datos');

            const data = await response.json();
            const advisors = (data && data.items) || [];

            if (window.canSeeAudit && window.canSeeAudit()) {
                await window.loadAuditUserNames(window.collectAuditUserIds(advisors));
            }

            if (advisors.length === 0 && pageNumber > 1 && data.totalPages > 0) {
                pageNumber = data.totalPages;
                return loadAdvisors();
            }

            if (includeInactive) {
                const hasInactive = advisors.some(a => a.isActive === false || a.is_active === false);
                if (!hasInactive) {
                    showAlert('No existen registros inactivos.', 'info');
                    const inactiveToggle = document.getElementById('advisorsIncludeInactiveToggle');
                    if (inactiveToggle) inactiveToggle.checked = false;
                    includeInactive = false;
                }
            }

            renderTable(advisors);
            if (window.renderPagination) {
                window.renderPagination(paginationContainer, data, (page) => {
                    pageNumber = page;
                    loadAdvisors();
                });
            }
        } catch (err) {
            tableBody.innerHTML = `<tr><td colspan="${colCount}" class="tecnm-table-empty tecnm-text-danger">${escapeHtml(err.message)}</td></tr>`;
            if (paginationContainer) paginationContainer.innerHTML = '';
        }
    }

    async function loadUserOptions() {
        const userSelect = document.getElementById('userId');
        if (!userSelect) return;

        userSelect.innerHTML = '<option value="">Cargando usuarios...</option>';

        try {
            const response = await fetch('/api/v1/roles/users/options');
            if (!response.ok) throw new Error('Error al cargar usuarios');
            const users = await response.json();

            if (!users || users.length === 0) {
                userSelect.innerHTML = '<option value="">-- No hay usuarios disponibles --</option>';
                return;
            }

            userSelect.innerHTML = '<option value="">-- Seleccionar Usuario --</option>' +
                users.map(u => `<option value="${u.id}">${escapeHtml(u.email)}</option>`).join('');
        } catch {
            userSelect.innerHTML = '<option value="">-- Error al cargar usuarios --</option>';
        }
    }

    function renderTable(advisors) {
        advisorsCache = advisors || [];

        if (!advisors || advisors.length === 0) {
            tableBody.innerHTML = `<tr><td colspan="${colCount}" class="tecnm-table-empty">No hay asesores registrados en la base de datos.</td></tr>`;
            return;
        }

        tableBody.innerHTML = advisors.map(a => `
            <tr>
                <td><strong>${escapeHtml(a.fullName)}</strong></td>
                <td><span class="tecnm-badge ${a.advisorType === 'internal' ? 'tecnm-badge-info' : 'tecnm-badge-warning'}">${escapeHtml(a.advisorType === 'internal' ? 'Interno' : 'Externo')}</span></td>
                <td>${escapeHtml(a.title || '-')}</td>
                <td>${escapeHtml(a.phone || '-')}</td>
                <td>
                    <span class="tecnm-badge ${a.isActive ? 'tecnm-badge-approved' : 'tecnm-badge-rejected'}">
                        ${a.isActive ? 'Activo' : 'Inactivo'}
                    </span>
                </td>
                ${showActions ? `
                <td>
                    <button onclick="openEditAdvisorModal(${a.id})" class="tecnm-btn tecnm-btn-secondary tecnm-btn-sm">Editar</button>
                    ${showAudit ? `<button onclick="openAdvisorAuditModal(${a.id})" class="tecnm-btn tecnm-btn-secondary tecnm-btn-sm">Auditoría</button>` : ''}
                    ${a.isActive 
                        ? `<button onclick="toggleActive(${a.id}, false)" class="tecnm-btn tecnm-btn-danger tecnm-btn-sm">Desactivar</button>`
                        : `<button onclick="toggleActive(${a.id}, true)" class="tecnm-btn tecnm-btn-success tecnm-btn-sm">Reactivar</button>`
                    }
                </td>
                ` : ''}
            </tr>
        `).join('');
    }

    if (form) {
        form.addEventListener('submit', async (e) => {
            e.preventDefault();
            const editingId = advisorIdInput ? advisorIdInput.value : '';

            const dto = {
                departmentId: parseInt(document.getElementById('departmentId').value, 10),
                advisorType: parseInt(document.getElementById('advisorType').value, 10),
                title: document.getElementById('title').value.trim(),
                fullName: document.getElementById('fullName').value.trim(),
                phone: document.getElementById('phone').value.trim()
            };

            if (editingId) {
                delete dto.userId;
            } else {
                dto.userId = parseInt(document.getElementById('userId').value, 10);
            }

            try {
                const url = editingId ? `/api/v1/advisors/${editingId}` : '/api/v1/advisors';
                const method = editingId ? 'PUT' : 'POST';

                const response = await fetch(url, {
                    method,
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify(dto)
                });

                if (!response.ok) {
                    const errData = await response.json().catch(() => ({}));
                    throw new Error(errData.message || 'Error al guardar asesor en la base de datos');
                }

                showAlert(editingId ? '¡Asesor actualizado correctamente!' : '¡Asesor guardado correctamente en la base de datos!', 'success');
                hideModal();
                loadAdvisors();
            } catch (err) {
                showAlert(err.message, 'danger');
            }
        });
    }

    window.toggleActive = async function(id, activate) {
        const url = `/api/v1/advisors/${id}` + (activate ? '/activate' : '');
        const method = activate ? 'PATCH' : 'DELETE';

        try {
            const response = await fetch(url, { method });
            if (!response.ok) throw new Error('Error al cambiar estado del asesor');
            showAlert(`Asesor #${id} ${activate ? 'reactivado' : 'desactivado'} correctamente.`, 'warning');
            loadAdvisors();
        } catch (err) {
            showAlert(err.message, 'danger');
        }
    };

    window.displaySingleAdvisor = (row) => {
        const clearBtn = document.getElementById('clearSearchFilterBtn');
        if (clearBtn) clearBtn.classList.remove('tecnm-hidden');

        if (paginationContainer) paginationContainer.innerHTML = '';

        const id = row.id;
        const fullName = row.full_name || row.fullName || '-';
        const advisorType = row.advisor_type || row.advisorType || 'internal';
        const title = row.title || '-';
        const phone = row.phone || '-';
        const isActive = row.is_active !== false;

        tableBody.innerHTML = `
            <tr>
                <td><strong>${escapeHtml(fullName)}</strong></td>
                <td><span class="tecnm-badge ${advisorType === 'internal' ? 'tecnm-badge-info' : 'tecnm-badge-warning'}">${escapeHtml(advisorType === 'internal' ? 'Interno' : 'Externo')}</span></td>
                <td>${escapeHtml(title)}</td>
                <td>${escapeHtml(phone)}</td>
                <td>
                    <span class="tecnm-badge ${isActive ? 'tecnm-badge-approved' : 'tecnm-badge-rejected'}">
                        ${isActive ? 'Activo' : 'Inactivo'}
                    </span>
                </td>
                ${showActions ? `
                <td>
                    <button onclick="openEditAdvisorModal(${id})" class="tecnm-btn tecnm-btn-secondary tecnm-btn-sm">Editar</button>
                </td>
                ` : ''}
            </tr>
        `;
    };

    window.clearModuleSearchFilter = () => {
        const clearBtn = document.getElementById('clearSearchFilterBtn');
        if (clearBtn) clearBtn.classList.add('tecnm-hidden');
        loadAdvisors();
    };

    if (reloadBtn) reloadBtn.addEventListener('click', loadAdvisors);
    loadAdvisors();

    function escapeHtml(text) {
        if (!text) return '';
        return text.toString()
            .replace(/&/g, "&amp;")
            .replace(/</g, "&lt;")
            .replace(/>/g, "&gt;")
            .replace(/"/g, "&quot;")
            .replace(/'/g, "&#039;");
    }
});
