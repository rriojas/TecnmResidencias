<script setup>
import { ref, computed, onMounted } from 'vue'
import { useAuthStore } from '@/stores/auth'
import { useAudit } from '@/composables/useAudit'
import { useConfirm } from '@/composables/useConfirm'
import TecnmPagination from '@/components/common/TecnmPagination.vue'
import apiClient from '@/services/api'

const authStore = useAuthStore()
const { showAudit } = useAudit()
const { confirm } = useConfirm()

const activeTab = ref('roles')
const alertMessage = ref('')
const alertType = ref('info')

// Tab 1: Roles
const roles = ref([])
const rolesLoading = ref(false)
const rolesError = ref('')
const rolesPageNumber = ref(1)
const rolesPageSize = ref(10)
const rolesTotalCount = ref(0)
const rolesTotalPages = ref(0)
const rolesSearch = ref('')
const rolesSortBy = ref('Name')
const rolesSortDir = ref('asc')
const rolesIncludeInactive = ref(false)

// Tab 2: Usuarios
const users = ref([])
const usersLoading = ref(false)
const usersError = ref('')
const usersPageNumber = ref(1)
const usersPageSize = ref(10)
const usersTotalCount = ref(0)
const usersTotalPages = ref(0)
const userRoleFilter = ref('all')
const userSearch = ref('')
const usersSortBy = ref('Email')
const usersSortDir = ref('asc')
const usersIncludeInactive = ref(false)

// Catálogos auxiliares
const roleOptions = ref([])
const allModules = ref([])

// Modales
const isRoleModalOpen = ref(false)
const isUserModalOpen = ref(false)
const isSubmitting = ref(false)
const userFormError = ref('')

// Formulario de Rol
const roleForm = ref({
  id: null,
  code: '',
  name: '',
  description: '',
  permissionIds: [],
})

// Formulario de Usuario
const userForm = ref({
  userId: null,
  controlNumber: '',
  firstName: '',
  lastName: '',
  lastName2: '',
  email: '',
  password: '',
  roleId: '',
  careerId: 4,
  phone: '',
  title: '',
  curp: '',
  gender: 'Masculino',
  academicPeriodId: 1,
})

const INSTITUTIONAL_DOMAIN = '@monclova.tecnm.mx'
const INSTITUTIONAL_EMAIL_ERROR =
  'Debes ingresar un correo institucional válido (@monclova.tecnm.mx).'

function isInstitutionalEmail(email) {
  const clean = (email || '').trim().toLowerCase()
  return clean.endsWith(INSTITUTIONAL_DOMAIN) && clean.length > INSTITUTIONAL_DOMAIN.length
}

function showAlert(message, type = 'info') {
  alertMessage.value = message
  alertType.value = type
  setTimeout(() => {
    if (alertMessage.value === message) {
      alertMessage.value = ''
    }
  }, 5000)
}

function switchTab(tab) {
  activeTab.value = tab
  if (tab === 'roles') {
    loadRolesData()
  } else {
    loadUsersData()
  }
}

// -------------------------------------------------------------
// Catálogo de Roles
// -------------------------------------------------------------
async function loadRolesData() {
  rolesLoading.value = true
  rolesError.value = ''
  try {
    const params = {
      pageNumber: rolesPageNumber.value,
      pageSize: rolesPageSize.value,
      search: rolesSearch.value.trim(),
      sortBy: rolesSortBy.value,
      sortDir: rolesSortDir.value,
      includeInactive: rolesIncludeInactive.value,
    }
    const res = await apiClient.get('/v1/roles', { params })
    const data = res.data || {}
    roles.value = data.items || []
    rolesTotalCount.value = data.totalCount || 0
    rolesTotalPages.value = data.totalPages || 0
  } catch (err) {
    rolesError.value = err.response?.data?.message || 'Error al cargar roles del sistema.'
    roles.value = []
  } finally {
    rolesLoading.value = false
  }
}

async function loadRoleOptions() {
  try {
    const res = await apiClient.get('/v1/roles?pageNumber=1&pageSize=50')
    const data = res.data || {}
    roleOptions.value = data.items || []
  } catch {
    roleOptions.value = []
  }
}

async function loadModulesData() {
  try {
    const res = await apiClient.get('/v1/roles/modules-permissions')
    allModules.value = Array.isArray(res.data) ? res.data : []
  } catch {
    allModules.value = []
  }
}

function toggleRoleSort(field) {
  if (rolesSortBy.value === field) {
    rolesSortDir.value = rolesSortDir.value === 'asc' ? 'desc' : 'asc'
  } else {
    rolesSortBy.value = field
    rolesSortDir.value = 'asc'
  }
  rolesPageNumber.value = 1
  loadRolesData()
}

function changeRolesPage(page) {
  rolesPageNumber.value = page
  loadRolesData()
}

function openCreateRoleModal() {
  roleForm.value = {
    id: null,
    code: '',
    name: '',
    description: '',
    permissionIds: [],
  }
  isRoleModalOpen.value = true
}

async function openEditRoleModal(r) {
  try {
    const res = await apiClient.get(`/v1/roles/${r.id}`)
    const fullRole = res.data || r
    roleForm.value = {
      id: fullRole.id,
      code: fullRole.code,
      name: fullRole.name,
      description: fullRole.description || '',
      permissionIds: (fullRole.permissions || []).map((p) => p.id),
    }
    isRoleModalOpen.value = true
  } catch {
    roleForm.value = {
      id: r.id,
      code: r.code,
      name: r.name,
      description: r.description || '',
      permissionIds: (r.permissions || []).map((p) => p.id),
    }
    isRoleModalOpen.value = true
  }
}

function closeRoleModal() {
  isRoleModalOpen.value = false
}

async function handleSaveRole() {
  if (!roleForm.value.name.trim()) {
    showAlert('El nombre del rol es obligatorio.', 'warning')
    return
  }
  if (!roleForm.value.id && !roleForm.value.code.trim()) {
    showAlert('El código del rol es obligatorio.', 'warning')
    return
  }

  isSubmitting.value = true
  const isEdit = !!roleForm.value.id
  const url = isEdit ? `/v1/roles/${roleForm.value.id}` : '/v1/roles'
  const payload = isEdit
    ? {
        name: roleForm.value.name.trim(),
        description: roleForm.value.description.trim(),
        permissionIds: roleForm.value.permissionIds,
      }
    : {
        code: roleForm.value.code.trim().toLowerCase(),
        name: roleForm.value.name.trim(),
        description: roleForm.value.description.trim(),
        permissionIds: roleForm.value.permissionIds,
      }

  try {
    if (isEdit) {
      await apiClient.put(url, payload)
    } else {
      await apiClient.post(url, payload)
    }
    showAlert(
      isEdit ? 'Rol actualizado correctamente.' : 'Rol registrado correctamente.',
      'success'
    )
    closeRoleModal()
    await loadRolesData()
    await loadRoleOptions()
  } catch (err) {
    const msg = err.response?.data?.message || 'Error al guardar el rol.'
    showAlert(msg, 'danger')
  } finally {
    isSubmitting.value = false
  }
}

async function handleDeleteRole(r) {
  const ok = await confirm({
    title: 'Eliminar Rol',
    message: `¿Está seguro de que desea eliminar el rol "${r.name}" (${r.code})? Esta acción no se puede deshacer.`,
    okText: 'Eliminar',
    cancelText: 'Cancelar',
  })
  if (!ok) return

  try {
    await apiClient.delete(`/v1/roles/${r.id}`)
    showAlert('Rol eliminado correctamente.', 'success')
    await loadRolesData()
    await loadRoleOptions()
  } catch (err) {
    const msg = err.response?.data?.message || 'Error al eliminar el rol.'
    showAlert(msg, 'danger')
  }
}

function handleOpenRoleAudit(r) {
  showAudit({
    title: `Auditoría — Rol "${r.name}"`,
    item: r,
  })
}

// -------------------------------------------------------------
// Asignación de Roles a Usuarios
// -------------------------------------------------------------
async function loadUsersData() {
  usersLoading.value = true
  usersError.value = ''
  try {
    const params = {
      pageNumber: usersPageNumber.value,
      pageSize: usersPageSize.value,
      roleFilter: userRoleFilter.value,
      search: userSearch.value.trim(),
      sortBy: usersSortBy.value,
      sortDir: usersSortDir.value,
      includeInactive: usersIncludeInactive.value,
    }
    const res = await apiClient.get('/v1/roles/users', { params })
    const data = res.data || {}
    users.value = data.items || []
    usersTotalCount.value = data.totalCount || 0
    usersTotalPages.value = data.totalPages || 0
  } catch (err) {
    usersError.value = err.response?.data?.message || 'Error al cargar usuarios del sistema.'
    users.value = []
  } finally {
    usersLoading.value = false
  }
}

function toggleUserSort(field) {
  if (usersSortBy.value === field) {
    usersSortDir.value = usersSortDir.value === 'asc' ? 'desc' : 'asc'
  } else {
    usersSortBy.value = field
    usersSortDir.value = 'asc'
  }
  usersPageNumber.value = 1
  loadUsersData()
}

function changeUsersPage(page) {
  usersPageNumber.value = page
  loadUsersData()
}

function openCreateUserModal() {
  userFormError.value = ''
  userForm.value = {
    userId: null,
    controlNumber: '',
    firstName: '',
    lastName: '',
    lastName2: '',
    email: '',
    password: '',
    roleId: '',
    careerId: 4,
    phone: '',
    title: '',
    curp: '',
    gender: 'Masculino',
    academicPeriodId: 1,
  }
  isUserModalOpen.value = true
}

function openEditUserModal(u) {
  userFormError.value = ''
  let firstName = u.firstName || ''
  let lastName = u.lastName || ''
  let lastName2 = u.lastName2 || ''

  if (!firstName && u.fullName) {
    const parts = u.fullName.trim().split(/\s+/)
    firstName = parts[0] || ''
    if (parts.length > 1) {
      lastName = parts.slice(1).join(' ')
    }
  }

  const currentRoleId =
    u.assignedRoles && u.assignedRoles.length > 0 ? u.assignedRoles[0].id : ''

  userForm.value = {
    userId: u.userId,
    controlNumber: u.controlNumber || '',
    firstName,
    lastName,
    lastName2,
    email: u.email,
    password: '',
    roleId: currentRoleId,
    careerId: u.careerId || u.departmentId || 4,
    phone: u.phone || '',
    title: u.title || '',
    curp: u.curp || '',
    gender: u.gender || 'Masculino',
    academicPeriodId: u.academicPeriodId || 1,
  }
  isUserModalOpen.value = true
}

function closeUserModal() {
  isUserModalOpen.value = false
  userFormError.value = ''
}

async function handleSaveUser() {
  userFormError.value = ''
  const isEdit = !!userForm.value.userId
  const email = userForm.value.email.trim()

  if (!userForm.value.roleId) {
    userFormError.value = 'Por favor seleccione un rol para el usuario.'
    return
  }

  if (!isInstitutionalEmail(email)) {
    userFormError.value = INSTITUTIONAL_EMAIL_ERROR
    return
  }

  if (
    !isEdit &&
    (!userForm.value.firstName.trim() ||
      !userForm.value.lastName.trim() ||
      !userForm.value.controlNumber.trim() ||
      !userForm.value.phone.trim() ||
      !userForm.value.password)
  ) {
    userFormError.value =
      'Todos los campos con asterisco son obligatorios para registrar un nuevo usuario.'
    return
  }

  isSubmitting.value = true
  const fullName = `${userForm.value.firstName.trim()} ${userForm.value.lastName.trim()}`.trim()
  const controlNumber = userForm.value.controlNumber.trim().toUpperCase()

  const payload = {
    email,
    roleId: parseInt(userForm.value.roleId, 10),
    firstName: userForm.value.firstName.trim() || null,
    lastName: userForm.value.lastName.trim() || null,
    lastName2: userForm.value.lastName2.trim() || null,
    controlNumber,
    careerId: parseInt(userForm.value.careerId || 4, 10),
    fullName,
    title: userForm.value.title.trim() || null,
    phone: userForm.value.phone.trim() || null,
    curp: userForm.value.curp.trim().toUpperCase() || null,
    gender: userForm.value.gender || null,
    academicPeriodId: userForm.value.academicPeriodId ? parseInt(userForm.value.academicPeriodId, 10) : 1,
    departmentId: parseInt(userForm.value.careerId || 4, 10),
    advisorType: 1,
  }

  if (isEdit) {
    if (userForm.value.password) payload.newPassword = userForm.value.password
  } else {
    payload.password = userForm.value.password
  }

  const url = isEdit ? `/v1/roles/users/${userForm.value.userId}` : '/v1/roles/users'

  try {
    if (isEdit) {
      await apiClient.put(url, payload)
    } else {
      await apiClient.post(url, payload)
    }
    showAlert(
      isEdit ? 'Usuario y rol actualizados correctamente.' : 'Usuario y rol registrados correctamente.',
      'success'
    )
    closeUserModal()
    await loadUsersData()
  } catch (err) {
    const errData = err.response?.data
    let errMsg = errData?.message || errData?.detail || errData?.title
    if (!errMsg && errData?.errors) {
      errMsg = Object.values(errData.errors).flat().join('\n')
    }
    userFormError.value = errMsg || 'Error al guardar el usuario.'
  } finally {
    isSubmitting.value = false
  }
}

function handleOpenUserAudit(u) {
  showAudit({
    title: `Auditoría — Usuario ${u.email}`,
    item: {
      ...u,
      id: u.userId,
      title: `${u.fullName || u.email} (${u.controlNumber || 'S/N'})`,
    },
  })
}

function getUserDisplayName(u) {
  let displayName = `${u.firstName || ''} ${u.lastName || ''}`.trim()
  if (u.lastName2) displayName += ` ${u.lastName2}`
  return displayName || u.fullName || 'Usuario'
}

const careersOptions = ref([])

async function loadCareersOptions() {
  try {
    const res = await apiClient.get('/v1/careers/all')
    careersOptions.value = res.data || []
  } catch (err) {
    careersOptions.value = []
  }
}

onMounted(async () => {
  await Promise.all([loadRolesData(), loadRoleOptions(), loadModulesData(), loadCareersOptions()])
})
</script>

<template>
  <div>
    <!-- Notificación Flotante Superior Derecha -->
    <div
      v-if="alertMessage"
      id="alertContainer"
      class="tecnm-alert"
      :class="`tecnm-alert-${alertType}`"
      role="alert"
    >
      <span>{{ alertMessage }}</span>
      <button
        type="button"
        class="tecnm-alert-close"
        aria-label="Cerrar"
        @click="alertMessage = ''"
      >
        &times;
      </button>
    </div>

    <!-- Barra de Título y Acciones -->
    <div class="tecnm-actions-bar">
      <div>
        <h1 class="tecnm-page-title">Gestión de Roles, Permisos y Usuarios</h1>
        <p class="tecnm-page-subtitle">Administración exclusiva de accesos y seguridad del sistema (SuperAdministrador)</p>
      </div>
    </div>

    <!-- Pestañas Institucionales -->
    <div class="tecnm-tabs">
      <button
        id="tabRolesBtn"
        type="button"
        class="tecnm-btn tab-btn"
        :class="activeTab === 'roles' ? 'tecnm-btn-primary active' : 'tecnm-btn-secondary'"
        @click="switchTab('roles')"
      >
        Catálogo de Roles
      </button>
      <button
        id="tabUsersBtn"
        type="button"
        class="tecnm-btn tab-btn"
        :class="activeTab === 'users' ? 'tecnm-btn-primary active' : 'tecnm-btn-secondary'"
        @click="switchTab('users')"
      >
        Asignar Rol a Usuario
      </button>
    </div>

    <!-- TAB 1: CATÁLOGO DE ROLES -->
    <div v-show="activeTab === 'roles'" id="tabRoles" class="tab-content active">
      <div class="tecnm-card">
        <div class="tecnm-card-header">
          <h3 class="tecnm-card-title">Roles Registrados</h3>
        </div>

        <div class="tecnm-card-toolbar">
          <input
            id="roleSearchInput"
            v-model="rolesSearch"
            type="text"
            class="tecnm-form-control tecnm-search-input"
            placeholder="Buscar por nombre o código..."
            @input="rolesPageNumber = 1; loadRolesData()"
          />

          <div class="tecnm-toolbar-actions">
            <label class="tecnm-switch-label">
              <span class="tecnm-switch">
                <input
                  id="rolesInactiveToggle"
                  v-model="rolesIncludeInactive"
                  type="checkbox"
                  @change="rolesPageNumber = 1; loadRolesData()"
                />
                <span class="tecnm-switch-slider"></span>
              </span>
              Mostrar inactivos
            </label>
            <button
              v-if="!authStore.isReadOnly"
              type="button"
              class="tecnm-btn tecnm-btn-primary"
              @click="openCreateRoleModal"
            >
              + Nuevo Rol
            </button>
          </div>
        </div>

        <div class="tecnm-card-body">
          <div class="tecnm-table-responsive">
            <table id="rolesTable" class="tecnm-table tecnm-table-striped">
              <thead>
                <tr>
                  <th
                    class="tecnm-th-sortable"
                    @click="toggleRoleSort('Code')"
                  >
                    Código
                    <span class="tecnm-sort-icon" :class="{ active: rolesSortBy === 'Code' }">
                      {{ rolesSortBy === 'Code' ? (rolesSortDir === 'asc' ? '↑' : '↓') : '↕' }}
                    </span>
                  </th>
                  <th
                    class="tecnm-th-sortable"
                    @click="toggleRoleSort('Name')"
                  >
                    Nombre
                    <span class="tecnm-sort-icon" :class="{ active: rolesSortBy === 'Name' }">
                      {{ rolesSortBy === 'Name' ? (rolesSortDir === 'asc' ? '↑' : '↓') : '↕' }}
                    </span>
                  </th>
                  <th
                    class="tecnm-th-sortable"
                    @click="toggleRoleSort('Description')"
                  >
                    Descripción
                    <span class="tecnm-sort-icon" :class="{ active: rolesSortBy === 'Description' }">
                      {{ rolesSortBy === 'Description' ? (rolesSortDir === 'asc' ? '↑' : '↓') : '↕' }}
                    </span>
                  </th>
                  <th>Permisos Asignados</th>
                  <th class="tecnm-th-actions">Acciones</th>
                </tr>
              </thead>
              <tbody id="rolesTableBody">
                <tr v-if="rolesLoading">
                  <td colspan="5" class="tecnm-table-empty">
                    Cargando roles...
                  </td>
                </tr>
                <tr v-else-if="rolesError">
                  <td colspan="5" class="tecnm-table-empty tecnm-text-danger">
                    {{ rolesError }}
                  </td>
                </tr>
                <tr v-else-if="roles.length === 0">
                  <td colspan="5" class="tecnm-table-empty">
                    No hay roles registrados.
                  </td>
                </tr>
                <tr
                  v-for="r in roles"
                  v-else
                  :key="r.id"
                >
                  <td><strong>{{ r.code }}</strong></td>
                  <td>{{ r.name }}</td>
                  <td>{{ r.description || '—' }}</td>
                  <td>
                    <div class="tecnm-d-flex tecnm-gap-1" style="flex-wrap: wrap;">
                      <template v-if="r.permissions && r.permissions.length > 0">
                        <span
                          v-for="p in r.permissions"
                          :key="p.id"
                          class="tecnm-badge tecnm-badge-neutral"
                        >
                          {{ p.slug }}
                        </span>
                      </template>
                      <span v-else class="tecnm-form-hint">Sin permisos</span>
                    </div>
                  </td>
                  <td>
                    <div class="tecnm-row-actions">
                      <button
                        v-if="!authStore.isReadOnly"
                        type="button"
                        class="tecnm-btn tecnm-btn-secondary tecnm-btn-sm"
                        @click="openEditRoleModal(r)"
                      >
                        Editar
                      </button>
                      <button
                        type="button"
                        class="tecnm-btn tecnm-btn-secondary tecnm-btn-sm"
                        @click="handleOpenRoleAudit(r)"
                      >
                        Auditoría
                      </button>
                      <button
                        v-if="!authStore.isReadOnly"
                        type="button"
                        class="tecnm-btn tecnm-btn-danger tecnm-btn-sm"
                        @click="handleDeleteRole(r)"
                      >
                        Eliminar
                      </button>
                    </div>
                  </td>
                </tr>
              </tbody>
            </table>
          </div>

          <TecnmPagination
            v-if="rolesTotalPages > 1"
            :current-page="rolesPageNumber"
            :total-pages="rolesTotalPages"
            :total-count="rolesTotalCount"
            :page-size="rolesPageSize"
            @page-change="changeRolesPage"
          />
        </div>
      </div>
    </div>

    <!-- TAB 2: ASIGNACIÓN DE USUARIOS -->
    <div v-show="activeTab === 'users'" id="tabUsers" class="tab-content active">
      <div class="tecnm-card">
        <div class="tecnm-card-header">
          <h3 class="tecnm-card-title">Asignación de Roles a Usuarios</h3>
        </div>

        <div class="tecnm-card-toolbar">
          <select
            id="userRoleFilter"
            v-model="userRoleFilter"
            class="tecnm-form-control tecnm-filter-select"
            @change="usersPageNumber = 1; loadUsersData()"
          >
            <option value="all">Todos los usuarios</option>
            <option value="with_role">Con Rol Asignado</option>
            <option value="without_role">Sin Rol Asignado</option>
          </select>

          <input
            id="userSearchInput"
            v-model="userSearch"
            type="text"
            class="tecnm-form-control tecnm-search-input"
            placeholder="Buscar por nombre o correo..."
            @input="usersPageNumber = 1; loadUsersData()"
          />

          <div class="tecnm-toolbar-actions">
            <label class="tecnm-switch-label">
              <span class="tecnm-switch">
                <input
                  id="usersInactiveToggle"
                  v-model="usersIncludeInactive"
                  type="checkbox"
                  @change="usersPageNumber = 1; loadUsersData()"
                />
                <span class="tecnm-switch-slider"></span>
              </span>
              Mostrar inactivos
            </label>
            <button
              v-if="!authStore.isReadOnly"
              type="button"
              class="tecnm-btn tecnm-btn-primary"
              @click="openCreateUserModal"
            >
              + Registrar Usuario / Asignar Rol
            </button>
          </div>
        </div>

        <div class="tecnm-card-body">
          <div class="tecnm-table-responsive">
            <table id="usersTable" class="tecnm-table tecnm-table-striped">
              <thead>
                <tr>
                  <th
                    class="tecnm-th-sortable"
                    @click="toggleUserSort('Email')"
                  >
                    Usuario / Correo
                    <span class="tecnm-sort-icon" :class="{ active: usersSortBy === 'Email' }">
                      {{ usersSortBy === 'Email' ? (usersSortDir === 'asc' ? '↑' : '↓') : '↕' }}
                    </span>
                  </th>
                  <th
                    class="tecnm-th-sortable"
                    @click="toggleUserSort('ControlNumber')"
                  >
                    Matrícula
                    <span class="tecnm-sort-icon" :class="{ active: usersSortBy === 'ControlNumber' }">
                      {{ usersSortBy === 'ControlNumber' ? (usersSortDir === 'asc' ? '↑' : '↓') : '↕' }}
                    </span>
                  </th>
                  <th
                    class="tecnm-th-sortable"
                    @click="toggleUserSort('Phone')"
                  >
                    Teléfono
                    <span class="tecnm-sort-icon" :class="{ active: usersSortBy === 'Phone' }">
                      {{ usersSortBy === 'Phone' ? (usersSortDir === 'asc' ? '↑' : '↓') : '↕' }}
                    </span>
                  </th>
                  <th
                    class="tecnm-th-sortable"
                    @click="toggleUserSort('Role')"
                  >
                    Rol Asignado
                    <span class="tecnm-sort-icon" :class="{ active: usersSortBy === 'Role' }">
                      {{ usersSortBy === 'Role' ? (usersSortDir === 'asc' ? '↑' : '↓') : '↕' }}
                    </span>
                  </th>
                  <th class="tecnm-th-actions">Acciones</th>
                </tr>
              </thead>
              <tbody id="usersTableBody">
                <tr v-if="usersLoading">
                  <td colspan="5" class="tecnm-table-empty">
                    Cargando usuarios...
                  </td>
                </tr>
                <tr v-else-if="usersError">
                  <td colspan="5" class="tecnm-table-empty tecnm-text-danger">
                    {{ usersError }}
                  </td>
                </tr>
                <tr v-else-if="users.length === 0">
                  <td colspan="5" class="tecnm-table-empty">
                    No se encontraron usuarios registrados.
                  </td>
                </tr>
                <tr
                  v-for="u in users"
                  v-else
                  :key="u.userId"
                >
                  <td>
                    <div><strong>{{ getUserDisplayName(u) }}</strong></div>
                    <div class="tecnm-form-hint">{{ u.email }}</div>
                  </td>
                  <td>{{ u.controlNumber || '—' }}</td>
                  <td>{{ u.phone || '—' }}</td>
                  <td>
                    <span
                      v-if="u.assignedRoles && u.assignedRoles.length > 0"
                      class="tecnm-badge tecnm-badge-approved"
                    >
                      {{ u.assignedRoles[0].name }}
                    </span>
                    <span
                      v-else-if="u.isAdmin"
                      class="tecnm-badge tecnm-badge-approved"
                    >
                      SuperAdministrador
                    </span>
                    <span
                      v-else
                      class="tecnm-badge tecnm-badge-pending"
                    >
                      Sin Rol Asignado
                    </span>
                  </td>
                  <td>
                    <div class="tecnm-row-actions">
                      <button
                        v-if="!authStore.isReadOnly"
                        type="button"
                        class="tecnm-btn tecnm-btn-secondary tecnm-btn-sm"
                        @click="openEditUserModal(u)"
                      >
                        Editar Usuario
                      </button>
                      <button
                        type="button"
                        class="tecnm-btn tecnm-btn-secondary tecnm-btn-sm"
                        @click="handleOpenUserAudit(u)"
                      >
                        Auditoría
                      </button>
                    </div>
                  </td>
                </tr>
              </tbody>
            </table>
          </div>

          <TecnmPagination
            v-if="usersTotalPages > 1"
            :current-page="usersPageNumber"
            :total-pages="usersTotalPages"
            :total-count="usersTotalCount"
            :page-size="usersPageSize"
            @page-change="changeUsersPage"
          />
        </div>
      </div>
    </div>

    <!-- MODAL CREAR / EDITAR ROL -->
    <div
      id="roleModal"
      class="modal-backdrop"
      :class="{ active: isRoleModalOpen }"
      aria-modal="true"
      role="dialog"
    >
      <div class="modal-card">
        <div class="tecnm-modal-header">
          <h3 id="roleModalTitle" class="tecnm-modal-title">
            {{ roleForm.id ? 'Editar Rol' : 'Nuevo Rol' }}
          </h3>
          <button
            type="button"
            class="tecnm-modal-close"
            aria-label="Cerrar"
            @click="closeRoleModal"
          >
            &times;
          </button>
        </div>

        <form id="roleForm" @submit.prevent="handleSaveRole">
          <div class="tecnm-form-grid">
            <div class="tecnm-form-group">
              <label for="roleCode" class="tecnm-label">Código del Rol *</label>
              <input
                id="roleCode"
                v-model="roleForm.code"
                type="text"
                class="tecnm-form-control"
                placeholder="Ej. auditor_calidad"
                :disabled="!!roleForm.id"
                required
              />
            </div>

            <div class="tecnm-form-group">
              <label for="roleName" class="tecnm-label">Nombre del Rol *</label>
              <input
                id="roleName"
                v-model="roleForm.name"
                type="text"
                class="tecnm-form-control"
                placeholder="Ej. Auditor de Calidad"
                required
              />
            </div>

            <div class="tecnm-form-group tecnm-form-group-full">
              <label for="roleDescription" class="tecnm-label">Descripción</label>
              <input
                id="roleDescription"
                v-model="roleForm.description"
                type="text"
                class="tecnm-form-control"
                placeholder="Descripción de funciones"
              />
            </div>

            <div class="tecnm-form-group tecnm-form-group-full">
              <label class="tecnm-label">Permisos Jerárquicos por Módulo</label>
              <div
                id="permissionsCatalogContainer"
                class="tecnm-permissions-box"
                style="max-height: 250px; overflow-y: auto; border: 1px solid var(--tecnm-gray-200); padding: 1rem; border-radius: var(--tecnm-radius-sm);"
              >
                <div
                  v-for="m in allModules"
                  :key="m.moduleSlug"
                  class="tecnm-perm-section"
                  style="margin-bottom: 1rem;"
                >
                  <h4 class="tecnm-perm-section-title" style="font-size: 0.875rem; font-weight: 700; color: var(--tecnm-primary-dark); margin-bottom: 0.5rem;">
                    Módulo: {{ m.moduleName }} ({{ m.moduleSlug }})
                  </h4>
                  <div
                    class="tecnm-perm-section-body"
                    style="display: grid; grid-template-columns: repeat(auto-fill, minmax(220px, 1fr)); gap: 0.5rem;"
                  >
                    <label
                      v-for="p in m.permissions"
                      :key="p.id"
                      class="tecnm-perm-checkbox"
                      style="display: flex; align-items: center; gap: 0.5rem; font-size: 0.8125rem;"
                    >
                      <input
                        v-model="roleForm.permissionIds"
                        type="checkbox"
                        :value="p.id"
                      />
                      <span><strong>{{ p.slug }}</strong> — {{ p.name }}</span>
                    </label>
                  </div>
                </div>
              </div>
            </div>
          </div>

          <div class="tecnm-modal-footer">
            <button
              type="button"
              class="tecnm-btn tecnm-btn-secondary"
              @click="closeRoleModal"
            >
              Cancelar
            </button>
            <button
              type="submit"
              class="tecnm-btn tecnm-btn-primary"
              :disabled="isSubmitting"
            >
              {{ isSubmitting ? 'Guardando...' : 'Guardar Rol' }}
            </button>
          </div>
        </form>
      </div>
    </div>

    <!-- MODAL CREAR / EDITAR USUARIO -->
    <div
      id="userRoleModal"
      class="modal-backdrop"
      :class="{ active: isUserModalOpen }"
      aria-modal="true"
      role="dialog"
    >
      <div class="modal-card">
        <div class="tecnm-modal-header">
          <h3 id="userModalTitle" class="tecnm-modal-title">
            {{ userForm.userId ? 'Editar Usuario y Asignar Rol' : 'Registrar Usuario y Asignar Rol' }}
          </h3>
          <button
            type="button"
            class="tecnm-modal-close"
            aria-label="Cerrar"
            @click="closeUserModal"
          >
            &times;
          </button>
        </div>

        <form id="userRoleForm" @submit.prevent="handleSaveUser">
          <div
            v-if="userFormError"
            id="userFormAlert"
            class="tecnm-alert tecnm-alert-danger"
            style="margin-bottom: 1rem;"
          >
            <span>{{ userFormError }}</span>
          </div>

          <div class="tecnm-form-grid">
            <div class="tecnm-form-group">
              <label for="userControlNumberInput" class="tecnm-label">Número de Control / Matrícula *</label>
              <input
                id="userControlNumberInput"
                v-model="userForm.controlNumber"
                type="text"
                class="tecnm-form-control"
                placeholder="20680123"
                required
              />
            </div>

            <div class="tecnm-form-group">
              <label for="userFirstNameInput" class="tecnm-label">Nombre(s) *</label>
              <input
                id="userFirstNameInput"
                v-model="userForm.firstName"
                type="text"
                class="tecnm-form-control"
                placeholder="Juan"
                required
              />
            </div>

            <div class="tecnm-form-group">
              <label for="userLastNameInput" class="tecnm-label">Apellido Paterno *</label>
              <input
                id="userLastNameInput"
                v-model="userForm.lastName"
                type="text"
                class="tecnm-form-control"
                placeholder="Pérez"
                required
              />
            </div>

            <div class="tecnm-form-group">
              <label for="userLastName2Input" class="tecnm-label">Apellido Materno</label>
              <input
                id="userLastName2Input"
                v-model="userForm.lastName2"
                type="text"
                class="tecnm-form-control"
                placeholder="López"
              />
            </div>

            <div class="tecnm-form-group">
              <label for="userEmailInput" class="tecnm-label">Correo institucional *</label>
              <input
                id="userEmailInput"
                v-model="userForm.email"
                type="email"
                class="tecnm-form-control"
                placeholder="ejemplo@monclova.tecnm.mx"
                required
              />
            </div>

            <div class="tecnm-form-group">
              <label
                for="userPasswordInput"
                class="tecnm-label"
              >
                {{ userForm.userId ? 'Nueva Contraseña (opcional)' : 'Contraseña de Acceso *' }}
              </label>
              <input
                id="userPasswordInput"
                v-model="userForm.password"
                type="password"
                class="tecnm-form-control"
                placeholder="••••••••"
                :required="!userForm.userId"
              />
            </div>

            <div class="tecnm-form-group">
              <label for="userRoleSelect" class="tecnm-label">Rol del Sistema *</label>
              <select
                id="userRoleSelect"
                v-model="userForm.roleId"
                class="tecnm-form-control"
                required
              >
                <option value="">-- Seleccionar Rol --</option>
                <option
                  v-for="r in roleOptions"
                  :key="r.id"
                  :value="r.id"
                >
                  {{ r.name }} ({{ r.code }})
                </option>
              </select>
            </div>

            <div class="tecnm-form-group">
              <label for="userCareerSelect" class="tecnm-label">Carrera / Área Académica *</label>
              <select
                id="userCareerSelect"
                v-model.number="userForm.careerId"
                class="tecnm-form-control"
                required
              >
                <option value="" disabled>-- Seleccionar Carrera --</option>
                <option
                  v-for="c in careersOptions"
                  :key="c.id"
                  :value="c.id"
                >
                  {{ c.name }} ({{ c.code }})
                </option>
              </select>
            </div>

            <div class="tecnm-form-group">
              <label for="userGenderSelect" class="tecnm-label">Género</label>
              <select
                id="userGenderSelect"
                v-model="userForm.gender"
                class="tecnm-form-control"
              >
                <option value="Masculino">Masculino</option>
                <option value="Femenino">Femenino</option>
                <option value="Otro">Otro / No especificado</option>
              </select>
            </div>

            <div class="tecnm-form-group">
              <label for="userAcademicPeriodSelect" class="tecnm-label">Periodo Académico</label>
              <select
                id="userAcademicPeriodSelect"
                v-model.number="userForm.academicPeriodId"
                class="tecnm-form-control"
              >
                <option :value="1">Ene-Jun 2026</option>
                <option :value="2">Ago-Dic 2026</option>
                <option :value="3">Ene-Jun 2027</option>
                <option :value="4">Ago-Dic 2027</option>
              </select>
            </div>

            <div class="tecnm-form-group">
              <label for="userPhoneInput" class="tecnm-label">Teléfono de Contacto *</label>
              <input
                id="userPhoneInput"
                v-model="userForm.phone"
                type="text"
                class="tecnm-form-control"
                placeholder="8661234567"
                required
              />
            </div>
          </div>

          <div class="tecnm-modal-footer">
            <button
              type="button"
              class="tecnm-btn tecnm-btn-secondary"
              @click="closeUserModal"
            >
              Cancelar
            </button>
            <button
              type="submit"
              class="tecnm-btn tecnm-btn-primary"
              :disabled="isSubmitting"
            >
              {{ isSubmitting ? 'Guardando...' : 'Guardar Usuario y Rol' }}
            </button>
          </div>
        </form>
      </div>
    </div>
  </div>
</template>
