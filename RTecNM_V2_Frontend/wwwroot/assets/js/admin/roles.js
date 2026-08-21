(() => {
  'use strict';

  let allRoles = [];
  let roleOptions = [];
  let allModules = [];
  let allUsers = [];
  let rolesPageNumber = 1;
  let usersPageNumber = 1;
  let rolesSearch = '';
  let rolesSortBy = 'Name';
  let rolesSortDir = 'asc';
  let rolesIncludeInactive = false;
  let usersSortBy = 'Email';
  let usersSortDir = 'asc';
  let usersIncludeInactive = false;

  window.openRoleAuditModal = (id) => {
    const r = allRoles.find(item => item.id === id);
    if (!r || !window.showAuditModal) return;

    window.showAuditModal(`Auditoría — Rol "${r.name}"`, [
      { label: 'ID', value: r.id },
      { label: 'Estado', value: r.isActive ? 'Activo' : 'Inactivo' },
      { label: 'Visible', value: r.isVisible ? 'Sí' : 'No' },
      { label: 'Orden', value: r.displayOrder },
      { label: 'Creado el', value: window.formatAuditDate(r.createdAt) },
      { label: 'Creado por', value: window.formatAuditUser(r.createdBy) },
      { label: 'Actualizado el', value: r.updatedBy ? window.formatAuditDate(r.updatedAt) : '—' },
      { label: 'Actualizado por', value: r.updatedBy ? window.formatAuditUser(r.updatedBy) : '—' },
      { label: 'Eliminado el', value: r.deletedAt ? window.formatAuditDate(r.deletedAt) : '—' },
      { label: 'Eliminado por', value: r.deletedBy ? window.formatAuditUser(r.deletedBy) : '—' }
    ]);
  };

  window.openUserAuditModal = (id) => {
    const u = allUsers.find(item => item.userId === id);
    if (!u || !window.showAuditModal) return;

    window.showAuditModal(`Auditoría — Usuario ${u.email}`, [
      { label: 'ID', value: u.userId },
      { label: 'Correo', value: u.email },
      { label: 'Estado', value: u.isActive ? 'Activo' : 'Inactivo' },
      { label: 'Visible', value: u.isVisible ? 'Sí' : 'No' },
      { label: 'Orden', value: u.displayOrder },
      { label: 'Creado el', value: window.formatAuditDate(u.createdAt) },
      { label: 'Creado por', value: window.formatAuditUser(u.createdBy) },
      { label: 'Actualizado el', value: u.updatedBy ? window.formatAuditDate(u.updatedAt) : '—' },
      { label: 'Actualizado por', value: u.updatedBy ? window.formatAuditUser(u.updatedBy) : '—' },
      { label: 'Eliminado el', value: u.deletedAt ? window.formatAuditDate(u.deletedAt) : '—' },
      { label: 'Eliminado por', value: u.deletedBy ? window.formatAuditUser(u.deletedBy) : '—' }
    ]);
  };

  const INSTITUTIONAL_DOMAIN = '@monclova.tecnm.mx';
  const INSTITUTIONAL_EMAIL_ERROR = 'Debes ingresar un correo institucional válido (@monclova.tecnm.mx).';

  function isInstitutionalEmail(email) {
    const clean = (email || '').trim().toLowerCase();
    return clean.endsWith(INSTITUTIONAL_DOMAIN) && clean.length > INSTITUTIONAL_DOMAIN.length;
  }

  function showUserFormError(message) {
    const el = document.getElementById('userFormAlert');
    if (el) {
      el.textContent = message;
      el.classList.remove('tecnm-hidden');
    }
  }

  function hideUserFormError() {
    const el = document.getElementById('userFormAlert');
    if (el) el.classList.add('tecnm-hidden');
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

  document.addEventListener('DOMContentLoaded', () => {
    loadRolesData();
    loadRoleOptions();
    loadModulesData();

    document.getElementById('userEmailInput')?.addEventListener('input', hideUserFormError);

    if (window.bindTableSearch) {
      window.bindTableSearch('roleSearchInput', (term) => {
        rolesSearch = term;
        rolesPageNumber = 1;
        loadRolesData();
      });
    }

    if (window.initSortableHeaders) {
      window.initSortableHeaders('rolesTable', (field, dir) => {
        rolesSortBy = field;
        rolesSortDir = dir;
        rolesPageNumber = 1;
        loadRolesData();
      });
      window.initSortableHeaders('usersTable', (field, dir) => {
        usersSortBy = field;
        usersSortDir = dir;
        usersPageNumber = 1;
        loadUsersData();
      });
    }

    document.getElementById('rolesInactiveToggle')?.addEventListener('change', (e) => {
      rolesIncludeInactive = e.target.checked;
      rolesPageNumber = 1;
      loadRolesData();
    });

    document.getElementById('usersInactiveToggle')?.addEventListener('change', (e) => {
      usersIncludeInactive = e.target.checked;
      usersPageNumber = 1;
      loadUsersData();
    });

    document.getElementById('exportRolesBtn')?.addEventListener('click', () => {
      if (window.downloadPdf) {
        const params = new URLSearchParams({ search: rolesSearch, sortBy: rolesSortBy, sortDir: rolesSortDir, includeInactive: rolesIncludeInactive });
        window.downloadPdf(`/api/v1/roles/export?${params}`, 'roles_tecnm.pdf');
      }
    });

    document.getElementById('exportUsersBtn')?.addEventListener('click', () => {
      if (window.downloadPdf) {
        const roleFilter = (document.getElementById('userRoleFilter')?.value || 'all').toLowerCase();
        const search = (document.getElementById('userSearchInput')?.value || '').trim();
        const params = new URLSearchParams({ search, roleFilter, sortBy: usersSortBy, sortDir: usersSortDir, includeInactive: usersIncludeInactive });
        window.downloadPdf(`/api/v1/roles/users/export?${params}`, 'usuarios_tecnm.pdf');
      }
    });
  });

  window.switchTab = (tab) => {
    const rolesTab = document.getElementById('tabRoles');
    const usersTab = document.getElementById('tabUsers');
    const rolesBtn = document.getElementById('tabRolesBtn');
    const usersBtn = document.getElementById('tabUsersBtn');

    if (tab === 'roles') {
      rolesTab.classList.add('active');
      usersTab.classList.remove('active');
      rolesBtn.className = 'tecnm-btn tecnm-btn-primary tab-btn active';
      usersBtn.className = 'tecnm-btn tecnm-btn-secondary tab-btn';
    } else {
      rolesTab.classList.remove('active');
      usersTab.classList.add('active');
      rolesBtn.className = 'tecnm-btn tecnm-btn-secondary tab-btn';
      usersBtn.className = 'tecnm-btn tecnm-btn-primary tab-btn active';
      loadUsersData();
    }
  };

  async function loadRolesData() {
    try {
      const params = new URLSearchParams({
        pageNumber: rolesPageNumber,
        pageSize: 10,
        search: rolesSearch,
        sortBy: rolesSortBy,
        sortDir: rolesSortDir,
        includeInactive: rolesIncludeInactive
      });
      const res = await fetch(`/api/v1/roles?${params}`);
      if (!res.ok) throw new Error('Error al cargar roles');
      const data = await res.json();
      allRoles = (data && data.items) || [];

      if (window.canSeeAudit && window.canSeeAudit()) {
        await window.loadAuditUserNames(window.collectAuditUserIds(allRoles));
      }

      if (allRoles.length === 0 && rolesPageNumber > 1 && data.totalPages > 0) {
        rolesPageNumber = data.totalPages;
        return loadRolesData();
      }

      renderRolesTable(allRoles);
      if (window.renderPagination) {
        window.renderPagination(document.getElementById('rolesPagination'), data, (page) => {
          rolesPageNumber = page;
          loadRolesData();
        });
      }
    } catch (err) {
      console.error(err);
      document.getElementById('rolesTableBody').innerHTML = `
        <tr><td colspan="5" class="tecnm-table-empty tecnm-text-danger">Error al cargar roles.</td></tr>
      `;
    }
  }

  async function loadRoleOptions() {
    try {
      const res = await fetch('/api/v1/roles?pageNumber=1&pageSize=50');
      if (!res.ok) throw new Error('Error al cargar catálogo de roles');
      const data = await res.json();
      roleOptions = (data && data.items) || [];
    } catch (err) {
      console.error(err);
      roleOptions = [];
    }
  }

  async function loadModulesData() {
    try {
      const res = await fetch('/api/v1/roles/modules-permissions');
      if (!res.ok) throw new Error('Error al cargar catálogo de permisos');
      allModules = await res.json();
    } catch (err) {
      console.error(err);
    }
  }

  async function loadUsersData() {
    try {
      const roleFilter = (document.getElementById('userRoleFilter')?.value || 'all').toLowerCase();
      const search = (document.getElementById('userSearchInput')?.value || '').trim();
      const params = new URLSearchParams({
        pageNumber: usersPageNumber,
        pageSize: 10,
        roleFilter,
        search,
        sortBy: usersSortBy,
        sortDir: usersSortDir,
        includeInactive: usersIncludeInactive
      });
      const res = await fetch(`/api/v1/roles/users?${params}`);
      if (!res.ok) throw new Error('Error al cargar usuarios');
      const data = await res.json();
      allUsers = (data && data.items) || [];

      if (window.canSeeAudit && window.canSeeAudit()) {
        await window.loadAuditUserNames(window.collectAuditUserIds(allUsers));
      }

      if (allUsers.length === 0 && usersPageNumber > 1 && data.totalPages > 0) {
        usersPageNumber = data.totalPages;
        return loadUsersData();
      }

      renderUsersTable(allUsers);
      if (window.renderPagination) {
        window.renderPagination(document.getElementById('usersPagination'), data, (page) => {
          usersPageNumber = page;
          loadUsersData();
        });
      }
    } catch (err) {
      console.error(err);
      document.getElementById('usersTableBody').innerHTML = `
        <tr><td colspan="5" class="tecnm-table-empty tecnm-text-danger">Error al cargar usuarios.</td></tr>
      `;
    }
  }

  function renderRolesTable(roles) {
    const tbody = document.getElementById('rolesTableBody');
    if (!roles || roles.length === 0) {
      tbody.innerHTML = `<tr><td colspan="5" class="tecnm-table-empty">No hay roles registrados.</td></tr>`;
      return;
    }

    tbody.innerHTML = roles.map(r => {
      const permsBadge = r.permissions && r.permissions.length > 0
        ? r.permissions.map(p => `<span class="tecnm-badge tecnm-badge-neutral">${p.slug}</span>`).join(' ')
        : '<span class="tecnm-form-hint">Sin permisos</span>';

      return `
        <tr>
          <td><strong>${r.code}</strong></td>
          <td>${r.name}</td>
          <td>${r.description || '—'}</td>
          <td>${permsBadge}</td>
          <td>
            <div class="tecnm-row-actions">
              <button type="button" class="tecnm-btn tecnm-btn-secondary tecnm-btn-sm" onclick="openEditRoleModal(${r.id})">Editar</button>
              <button type="button" class="tecnm-btn tecnm-btn-secondary tecnm-btn-sm" onclick="openRoleAuditModal(${r.id})">Auditoría</button>
              <button type="button" class="tecnm-btn tecnm-btn-danger tecnm-btn-sm" onclick="deleteRole(${r.id})">Eliminar</button>
            </div>
          </td>
        </tr>
      `;
    }).join('');
  }

  let userFilterDebounce = null;

  window.applyUserFilters = () => {
    clearTimeout(userFilterDebounce);
    userFilterDebounce = setTimeout(() => {
      usersPageNumber = 1;
      loadUsersData();
    }, 300);
  };

  function renderUsersTable(users) {
    const tbody = document.getElementById('usersTableBody');
    if (!users || users.length === 0) {
      tbody.innerHTML = `<tr><td colspan="5" class="tecnm-table-empty">No se encontraron usuarios.</td></tr>`;
      return;
    }

    tbody.innerHTML = users.map(u => {
      let displayName = `${u.firstName || ''} ${u.lastName || ''}`.trim();
      if (u.lastName2) displayName += ` ${u.lastName2}`;
      if (!displayName) displayName = u.fullName || 'Usuario';

      const controlNum = u.controlNumber || '—';
      const phoneNum = u.phone || '—';

      const assignedRoleName = u.assignedRoles && u.assignedRoles.length > 0
        ? `<span class="tecnm-badge tecnm-badge-approved">${escapeHtml(u.assignedRoles[0].name)}</span>`
        : (u.isAdmin
          ? `<span class="tecnm-badge tecnm-badge-approved">SuperAdministrador</span>`
          : `<span class="tecnm-badge tecnm-badge-pending">Sin Rol Asignado</span>`);

      return `
        <tr>
          <td>
            <div><strong>${escapeHtml(displayName)}</strong></div>
            <div class="tecnm-form-hint">${escapeHtml(u.email)}</div>
          </td>
          <td>${escapeHtml(controlNum)}</td>
          <td>${escapeHtml(phoneNum)}</td>
          <td>${assignedRoleName}</td>
          <td>
            <div class="tecnm-row-actions">
              <button type="button" class="tecnm-btn tecnm-btn-secondary tecnm-btn-sm" onclick="openEditUserModal(${u.userId})">Editar Usuario</button>
              <button type="button" class="tecnm-btn tecnm-btn-secondary tecnm-btn-sm" onclick="openUserAuditModal(${u.userId})">Auditoría</button>
            </div>
          </td>
        </tr>
      `;
    }).join('');
  }

  function renderPermissionsChecklist(selectedPermissionIds = []) {
    const container = document.getElementById('permissionsCatalogContainer');
    if (!allModules || allModules.length === 0) {
      container.innerHTML = `<p class="tecnm-form-hint">Sin catálogo de permisos.</p>`;
      return;
    }

    container.innerHTML = allModules.map(m => {
      const perms = m.permissions || [];
      const checkboxes = perms.map(p => {
        const checked = selectedPermissionIds.includes(p.id) ? 'checked' : '';
        return `
          <label class="tecnm-perm-checkbox">
            <input type="checkbox" name="permCheckbox" value="${p.id}" ${checked} />
            <span><strong>${p.slug}</strong> — ${p.name}</span>
          </label>
        `;
      }).join('');

      return `
        <div class="tecnm-perm-section">
          <h4 class="tecnm-perm-section-title">
            Módulo: ${m.moduleName} (${m.moduleSlug})
          </h4>
          <div class="tecnm-perm-section-body">
            ${checkboxes || '<span class="tecnm-form-hint">Sin permisos</span>'}
          </div>
        </div>
      `;
    }).join('');
  }

  window.openCreateRoleModal = () => {
    document.getElementById('roleModalTitle').textContent = 'Nuevo Rol';
    document.getElementById('roleId').value = '';
    document.getElementById('roleCode').value = '';
    document.getElementById('roleCode').disabled = false;
    document.getElementById('roleName').value = '';
    document.getElementById('roleDescription').value = '';
    renderPermissionsChecklist([]);

    const modal = document.getElementById('roleModal');
    modal.classList.add('active');
    modal.setAttribute('aria-hidden', 'false');
  };

  window.openEditRoleModal = async (id) => {
    try {
      const res = await fetch(`/api/v1/roles/${id}`);
      if (!res.ok) throw new Error('Rol no encontrado');
      const role = await res.json();

      document.getElementById('roleModalTitle').textContent = 'Editar Rol';
      document.getElementById('roleId').value = role.id;
      document.getElementById('roleCode').value = role.code;
      document.getElementById('roleCode').disabled = true;
      document.getElementById('roleName').value = role.name;
      document.getElementById('roleDescription').value = role.description || '';

      const selectedIds = (role.permissions || []).map(p => p.id);
      renderPermissionsChecklist(selectedIds);

      const modal = document.getElementById('roleModal');
      modal.classList.add('active');
      modal.setAttribute('aria-hidden', 'false');
    } catch (err) {
      alert('No se pudo cargar el rol solicitado.');
    }
  };

  window.closeRoleModal = () => {
    const modal = document.getElementById('roleModal');
    modal.classList.remove('active');
    modal.setAttribute('aria-hidden', 'true');
  };

  window.saveRole = async (e) => {
    e.preventDefault();
    const id = document.getElementById('roleId').value;
    const code = document.getElementById('roleCode').value.trim();
    const name = document.getElementById('roleName').value.trim();
    const description = document.getElementById('roleDescription').value.trim();

    const checkedPerms = Array.from(document.querySelectorAll('input[name="permCheckbox"]:checked'))
      .map(cb => parseInt(cb.value, 10));

    const isEdit = !!id;
    const url = isEdit ? `/api/v1/roles/${id}` : '/api/v1/roles';
    const method = isEdit ? 'PUT' : 'POST';
    const payload = isEdit ? { name, description, permissionIds: checkedPerms } : { code, name, description, permissionIds: checkedPerms };

    try {
      const res = await fetch(url, {
        method,
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(payload)
      });

      if (!res.ok) {
        const errData = await res.json().catch(() => ({}));
        alert(errData.message || 'Error al guardar el rol.');
        return;
      }

      closeRoleModal();
      await loadRolesData();
    } catch (err) {
      console.error(err);
      alert('Ocurrió un error al guardar el rol.');
    }
  };

  window.deleteRole = async (id) => {
    const confirmed = await window.tecnmConfirm('¿Está seguro de eliminar este rol?', 'Eliminar Rol');
    if (!confirmed) return;

    try {
      const res = await fetch(`/api/v1/roles/${id}`, { method: 'DELETE' });
      if (!res.ok) {
        alert('Error al eliminar el rol.');
        return;
      }
      await loadRolesData();
    } catch (err) {
      console.error(err);
      alert('Error de conexión.');
    }
  };

  function populateUserRoleSelect(selectedRoleId = 0) {
    const select = document.getElementById('userRoleSelect');
    if (!select) return;

    if (!roleOptions || roleOptions.length === 0) {
      select.innerHTML = '<option value="">-- No hay roles registrados --</option>';
      return;
    }

    select.innerHTML = '<option value="">-- Seleccionar Rol --</option>' +
      roleOptions.map(r => {
        const isSelected = r.id === selectedRoleId ? 'selected' : '';
        return `<option value="${r.id}" ${isSelected}>${escapeHtml(r.name)} (${escapeHtml(r.code)})</option>`;
      }).join('');
  }

  function setVal(id, val) {
    const el = document.getElementById(id);
    if (el) el.value = val;
  }

  window.openCreateUserModal = () => {
    const titleEl = document.getElementById('userModalTitle');
    if (titleEl) titleEl.textContent = 'Registrar Usuario y Asignar Rol';

    hideUserFormError();
    setVal('assignUserId', '');
    setVal('userEmailInput', '');
    setVal('userPasswordInput', '');

    const passInput = document.getElementById('userPasswordInput');
    if (passInput) passInput.required = true;

    const passLabel = document.getElementById('userPasswordLabel');
    if (passLabel) passLabel.textContent = 'Contraseña de Acceso *';

    // Reset profile fields safely
    setVal('userFirstNameInput', '');
    setVal('userLastNameInput', '');
    setVal('userLastName2Input', '');
    setVal('userControlNumberInput', '');
    setVal('userCareerSelect', '4');
    setVal('userPhoneInput', '');
    setVal('userTitleInput', '');
    setVal('userCurpInput', '');

    populateUserRoleSelect(0);

    const modal = document.getElementById('userRoleModal');
    if (modal) {
      modal.classList.add('active');
      modal.setAttribute('aria-hidden', 'false');
    }
  };

  window.openEditUserModal = (userId) => {
    const user = allUsers.find(u => u.userId === userId);
    if (!user) return;

    const titleEl = document.getElementById('userModalTitle');
    if (titleEl) titleEl.textContent = 'Editar Usuario y Asignar Rol';

    hideUserFormError();
    setVal('assignUserId', user.userId);
    setVal('userEmailInput', user.email);
    setVal('userPasswordInput', '');

    const passInput = document.getElementById('userPasswordInput');
    if (passInput) passInput.required = false;

    const passLabel = document.getElementById('userPasswordLabel');
    if (passLabel) passLabel.textContent = 'Nueva Contraseña (opcional - dejar en blanco para mantener la actual)';

    // Load profile fields safely
    let firstName = user.firstName || '';
    let lastName = user.lastName || '';
    let lastName2 = user.lastName2 || '';

    if (!firstName && user.fullName) {
      const parts = user.fullName.trim().split(/\s+/);
      firstName = parts[0] || '';
      if (parts.length > 1) {
        lastName = parts.slice(1).join(' ');
      }
    }

    setVal('userFirstNameInput', firstName);
    setVal('userLastNameInput', lastName);
    setVal('userLastName2Input', lastName2);
    setVal('userControlNumberInput', user.controlNumber || '');
    setVal('userCareerSelect', (user.careerId || 4).toString());
    setVal('userPhoneInput', user.phone || '');
    setVal('userTitleInput', user.title || '');
    setVal('userCurpInput', user.curp || '');

    const currentRoleId = (user.assignedRoles && user.assignedRoles.length > 0) ? user.assignedRoles[0].id : 0;
    populateUserRoleSelect(currentRoleId);

    const modal = document.getElementById('userRoleModal');
    if (modal) {
      modal.classList.add('active');
      modal.setAttribute('aria-hidden', 'false');
    }
  };

  window.closeUserRoleModal = () => {
    const modal = document.getElementById('userRoleModal');
    modal.classList.remove('active');
    modal.setAttribute('aria-hidden', 'true');
  };

  window.saveUser = async (e) => {
    e.preventDefault();
    const userId = document.getElementById('assignUserId').value;
    const email = document.getElementById('userEmailInput').value.trim();
    const password = document.getElementById('userPasswordInput').value;
    const roleIdVal = parseInt(document.getElementById('userRoleSelect').value, 10);

    if (!roleIdVal) {
      alert('Por favor seleccione un rol para el usuario.');
      return;
    }

    if (!isInstitutionalEmail(email)) {
      showUserFormError(INSTITUTIONAL_EMAIL_ERROR);
      document.getElementById('userEmailInput')?.focus();
      return;
    }

    const isEdit = !!userId;
    const url = isEdit ? `/api/v1/roles/users/${userId}` : '/api/v1/roles/users';
    const method = isEdit ? 'PUT' : 'POST';

    const firstName = document.getElementById('userFirstNameInput')?.value.trim() || null;
    const lastName = document.getElementById('userLastNameInput')?.value.trim() || null;
    const fullName = firstName ? (lastName ? `${firstName} ${lastName}` : firstName) : null;

    const controlNumRaw = document.getElementById('userControlNumberInput')?.value.trim() || null;
    const controlNumber = controlNumRaw ? controlNumRaw.toUpperCase() : null;

    const payload = {
      email,
      roleId: roleIdVal,
      firstName,
      lastName,
      lastName2: document.getElementById('userLastName2Input')?.value.trim() || null,
      controlNumber,
      careerId: parseInt(document.getElementById('userCareerSelect')?.value || '1', 10),
      fullName,
      title: document.getElementById('userTitleInput')?.value.trim() || null,
      phone: document.getElementById('userPhoneInput')?.value.trim() || null,
      curp: document.getElementById('userCurpInput')?.value.trim()?.toUpperCase() || null,
      departmentId: 1,
      advisorType: 0
    };

    if (isEdit) {
      if (password) payload.newPassword = password;
    } else {
      payload.password = password;
    }

    try {
      const res = await fetch(url, {
        method,
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(payload)
      });

      if (!res.ok) {
        const errData = await res.json().catch(() => ({}));
        let errMsg = errData.message || errData.detail || errData.title;
        if (!errMsg && errData.errors) {
          errMsg = Object.values(errData.errors).flat().join('\n');
        }
        alert(errMsg || 'Error al guardar el usuario.');
        return;
      }

      closeUserRoleModal();
      await loadUsersData();
    } catch (err) {
      console.error(err);
      alert('Ocurrió un error al guardar el usuario.');
    }
  };
})();
