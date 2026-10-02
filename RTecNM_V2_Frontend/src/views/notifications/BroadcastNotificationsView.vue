<script setup>
import { ref, reactive, computed, onMounted } from 'vue'
import apiClient from '@/services/api'
import { useAuthStore } from '@/stores/auth'
import { useConfirm } from '@/composables/useConfirm'
import { useGlobalSearch } from '@/composables/useGlobalSearch'
import TecnmPagination from '@/components/common/TecnmPagination.vue'

const authStore = useAuthStore()
const { confirm } = useConfirm()
const globalSearch = useGlobalSearch()

// Estados reactivos
const isLoadingHistory = ref(false)
const isSubmitting = ref(false)
const historyList = ref([])
const totalCount = ref(0)
const totalPages = ref(1)
const currentPage = ref(1)
const pageSize = ref(10)
const careers = ref([])

// Feedback institucional
const alertMessage = ref('')
const alertType = ref('success')
let alertTimer = null

function showAlert(msg, type = 'success') {
  alertMessage.value = msg
  alertType.value = type
  clearTimeout(alertTimer)
  alertTimer = setTimeout(() => {
    alertMessage.value = ''
  }, 4500)
}

// Filtros del historial
const filters = reactive({
  search: '',
  role: '',
  careerId: '',
  activeOnly: false
})

// Formulario de emisión
const form = reactive({
  title: '',
  description: '',
  expiresAt: '',
  targetRoles: ['all'],
  careerId: '',
  residencyModality: ''
})

// Modo de emisión: 'broadcast' o 'individual' (Pruebas unitarias)
const sendMode = ref('broadcast')
const userSearchQuery = ref('')
const isSearchingUsers = ref(false)
const userSearchResults = ref([])
const selectedUser = ref(null)
let searchDebounceTimer = null

const isDirectorOnly = computed(() => {
  return form.targetRoles.length === 1 && form.targetRoles[0] === 'director'
})

function onSearchUsers() {
  clearTimeout(searchDebounceTimer)
  const q = userSearchQuery.value.trim()
  if (!q || q.length < 2) {
    userSearchResults.value = []
    return
  }
  searchDebounceTimer = setTimeout(async () => {
    isSearchingUsers.value = true
    try {
      const res = await apiClient.get('/v1/notifications/user-options', {
        params: { search: q }
      })
      userSearchResults.value = res.data || []
    } catch (err) {
      console.error('Error buscando usuarios:', err)
    } finally {
      isSearchingUsers.value = false
    }
  }, 300)
}

function selectTargetUser(u) {
  selectedUser.value = u
  userSearchResults.value = []
  userSearchQuery.value = ''
}

function clearTargetUser() {
  selectedUser.value = null
  userSearchResults.value = []
  userSearchQuery.value = ''
}

function openGlobalSearchModal() {
  globalSearch.open({
    initialSource: 'USERS',
    onSelect: (row) => {
      const getVal = (r, k) => {
        if (!r) return null
        if (r[k] !== undefined) return r[k]
        const match = Object.keys(r).find((x) => x.toLowerCase() === k.toLowerCase())
        return match ? r[match] : null
      }

      const userId = getVal(row, 'user_id') || getVal(row, 'id')
      const fullName = getVal(row, 'full_name') || `${getVal(row, 'first_name') || ''} ${getVal(row, 'last_name') || ''}`.trim()
      const email = getVal(row, 'email') || ''
      const role = getVal(row, 'role') || (getVal(row, 'control_number') ? 'Estudiante' : 'Usuario')
      const controlNumber = getVal(row, 'control_number') || null

      selectTargetUser({
        id: Number(userId),
        fullName: fullName || email,
        email: email,
        role: role,
        controlNumber: controlNumber
      })
    }
  })
}

const availableRoles = [
  { code: 'all', label: 'Todos los Roles' },
  { code: 'student', label: 'Estudiantes' },
  { code: 'advisor', label: 'Asesores' },
  { code: 'jefecarrera', label: 'Jefes de Carrera' },
  { code: 'coordinadora', label: 'Coordinación' },
  { code: 'vinculacion', label: 'Vinculación' },
  { code: 'academic', label: 'Personal Académico' },
  { code: 'director', label: 'Dirección' }
]

function handleRoleToggle(code) {
  if (code === 'all') {
    form.targetRoles = ['all']
    return
  }
  // Si se selecciona un rol específico, quitar 'all'
  form.targetRoles = form.targetRoles.filter((r) => r !== 'all')
  const idx = form.targetRoles.indexOf(code)
  if (idx >= 0) {
    form.targetRoles.splice(idx, 1)
  } else {
    form.targetRoles.push(code)
  }
  // Si queda vacío, revertir a 'all'
  if (form.targetRoles.length === 0) {
    form.targetRoles = ['all']
  }
}

async function loadCareers() {
  try {
    const res = await apiClient.get('/v1/careers/all')
    const list = Array.isArray(res.data) ? res.data : (res.data?.items || [])
    careers.value = list.filter((c) => c.isActive !== false)
  } catch (err) {
    console.error('Error cargando carreras:', err)
  }
}

async function loadHistory() {
  isLoadingHistory.value = true
  try {
    const params = {
      pageNumber: currentPage.value,
      pageSize: pageSize.value,
      search: filters.search || undefined,
      role: filters.role || undefined,
      careerId: filters.careerId ? Number(filters.careerId) : undefined,
      activeOnly: filters.activeOnly ? true : undefined
    }

    const res = await apiClient.get('/v1/notifications/history', { params })
    const data = res.data
    historyList.value = data.items || []
    totalCount.value = data.totalCount || 0
    totalPages.value = data.totalPages || 1
  } catch (err) {
    showAlert(err.response?.data?.message || 'Error cargando historial de notificaciones.', 'danger')
  } finally {
    isLoadingHistory.value = false
  }
}

async function submitNotification() {
  if (!form.title.trim()) {
    showAlert('El tema o título es obligatorio.', 'warning')
    return
  }
  if (!form.description.trim()) {
    showAlert('La descripción del aviso es obligatoria.', 'warning')
    return
  }
  if (!form.expiresAt) {
    showAlert('La fecha de expedición / expiración es obligatoria.', 'warning')
    return
  }

  if (sendMode.value === 'individual' && !selectedUser.value) {
    showAlert('Debe seleccionar un usuario destinatario para el modo de prueba individual.', 'warning')
    return
  }

  isSubmitting.value = true
  try {
    const payload = {
      title: form.title.trim(),
      description: form.description.trim(),
      expiresAt: new Date(form.expiresAt).toISOString(),
      targetRoles: sendMode.value === 'individual' ? ['individual'] : form.targetRoles,
      targetUserId: sendMode.value === 'individual' ? selectedUser.value.id : null,
      careerId: sendMode.value === 'individual' || isDirectorOnly.value ? null : (form.careerId ? Number(form.careerId) : null),
      residencyModality: sendMode.value === 'individual' ? null : (form.residencyModality || null)
    }

    await apiClient.post('/v1/notifications', payload)
    showAlert(
      sendMode.value === 'individual'
        ? `Aviso y correo de prueba enviados exitosamente a ${selectedUser.value.fullName || selectedUser.value.email}.`
        : 'Aviso emitido y asignado correctamente.',
      'success'
    )

    // Resetear formulario
    form.title = ''
    form.description = ''
    form.expiresAt = ''
    form.targetRoles = ['all']
    form.careerId = ''
    form.residencyModality = ''
    selectedUser.value = null
    userSearchQuery.value = ''

    currentPage.value = 1
    await loadHistory()
  } catch (err) {
    showAlert(err.response?.data?.message || 'Error al emitir la notificación.', 'danger')
  } finally {
    isSubmitting.value = false
  }
}

async function deleteNotification(item) {
  const ok = await confirm({
    title: 'Eliminar Aviso',
    message: `¿Está seguro de eliminar el aviso "${item.title}"? Dejará de mostrarse a los destinatarios.`,
    okText: 'Eliminar',
    cancelText: 'Cancelar'
  })
  if (!ok) return

  try {
    await apiClient.delete(`/v1/notifications/${item.id}`)
    showAlert('Aviso eliminado correctamente.', 'success')
    await loadHistory()
  } catch (err) {
    showAlert(err.response?.data?.message || 'Error al eliminar el aviso.', 'danger')
  }
}

function formatDate(dateStr) {
  if (!dateStr) return '-'
  try {
    const d = new Date(dateStr)
    const months = [
      'Enero', 'Febrero', 'Marzo', 'Abril', 'Mayo', 'Junio',
      'Julio', 'Agosto', 'Septiembre', 'Octubre', 'Noviembre', 'Diciembre'
    ]
    const day = String(d.getDate()).padStart(2, '0')
    const month = months[d.getMonth()]
    const year = d.getFullYear()
    return `${day}/${month}/${year}`
  } catch {
    return dateStr
  }
}

function getRoleLabel(code) {
  const match = availableRoles.find((r) => r.code === code)
  return match ? match.label : code
}

function getModalityLabel(mod) {
  if (!mod) return ''
  const m = mod.toLowerCase()
  if (m === 'acreditacion' || m === 'innovatec' || m === 'hackatec') {
    return 'InnovaTecNM / HackaTec'
  }
  if (m === 'regular') return 'Regular / Empresa'
  return mod
}

onMounted(async () => {
  await Promise.all([loadCareers(), loadHistory()])
})
</script>

<template>
  <div class="tecnm-page">
    <div class="tecnm-container">
      <!-- Encabezado de Página -->
      <div class="tecnm-page-header">
        <div class="tecnm-page-header-content">
          <h1 class="tecnm-page-title">Emisión y Gestión de Notificaciones</h1>
          <p class="tecnm-page-subtitle">
            Redacción de avisos para alumnos y otros roles con asignación segmentada y fecha de expiración automática.
          </p>
        </div>
      </div>

      <!-- Alerta institucional -->
      <transition name="tecnm-fade">
        <div
          v-if="alertMessage"
          class="tecnm-alert"
          :class="`tecnm-alert-${alertType}`"
          role="alert"
        >
          <span>{{ alertMessage }}</span>
          <button type="button" class="tecnm-alert-close" aria-label="Cerrar" @click="alertMessage = ''">
            &times;
          </button>
        </div>
      </transition>

      <!-- Layout en 2 columnas: Formulario de Emisión + Historial -->
      <div class="tecnm-notifications-grid">
        <!-- Tarjeta 1: Formulario de Emisión -->
        <section class="tecnm-card tecnm-broadcast-card">
          <div class="tecnm-card-header">
            <h2 class="tecnm-card-title">Redactar Nueva Notificación</h2>
            <span class="tecnm-badge-outline">Acceso Académico / Admin</span>
          </div>

          <form @submit.prevent="submitNotification" class="tecnm-form-stack">
            <!-- Tema / Título -->
            <div class="tecnm-form-group">
              <label for="noticeTitle" class="tecnm-label">
                Tema / Asunto <span class="tecnm-required">*</span>
              </label>
              <input
                id="noticeTitle"
                v-model="form.title"
                type="text"
                class="tecnm-form-control"
                placeholder="Ej. Entrega urgente de cartas de aceptación"
                maxlength="200"
                required
              />
            </div>

            <!-- Descripción -->
            <div class="tecnm-form-group">
              <label for="noticeDesc" class="tecnm-label">
                Descripción / Contenido del Mensaje <span class="tecnm-required">*</span>
              </label>
              <textarea
                id="noticeDesc"
                v-model="form.description"
                class="tecnm-form-control tecnm-textarea"
                rows="4"
                placeholder="Detalla las instrucciones o requerimientos a comunicar..."
                required
              ></textarea>
            </div>

            <!-- Fecha de Expiración -->
            <div class="tecnm-form-group">
              <label for="noticeExpires" class="tecnm-label">
                Fecha de Expiración (Desaparecerá a partir de esta fecha) <span class="tecnm-required">*</span>
              </label>
              <input
                id="noticeExpires"
                v-model="form.expiresAt"
                type="datetime-local"
                class="tecnm-form-control"
                required
              />
              <span class="tecnm-help-text">
                El aviso será retirado de la campana de los roles asignados una vez llegada esta fecha.
              </span>
            </div>

            <!-- Modo de Envío: Segmentado o Usuario Específico -->
            <div class="tecnm-form-group">
              <label class="tecnm-label">Destinatarios del Aviso</label>
              <div class="tecnm-mode-switch">
                <button
                  type="button"
                  class="tecnm-mode-btn"
                  :class="{ 'is-active': sendMode === 'broadcast' }"
                  @click="sendMode = 'broadcast'"
                >
                  📢 Audiencia Segmentada
                </button>
                <button
                  type="button"
                  class="tecnm-mode-btn"
                  :class="{ 'is-active': sendMode === 'individual' }"
                  @click="sendMode = 'individual'"
                >
                  🎯 Usuario Específico (Pruebas)
                </button>
              </div>
            </div>

            <!-- Panel para Modo Individual -->
            <div v-if="sendMode === 'individual'" class="tecnm-form-group">
              <div class="tecnm-flex-between" style="margin-bottom: 6px;">
                <label class="tecnm-label" style="margin-bottom: 0;">
                  Seleccionar Usuario de Prueba <span class="tecnm-required">*</span>
                </label>
                <button
                  type="button"
                  id="btnOpenGlobalSearchUser"
                  class="tecnm-btn-global-search"
                  title="Abrir buscador institucional"
                  @click="openGlobalSearchModal"
                >
                  <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                    <circle cx="11" cy="11" r="8"></circle>
                    <line x1="21" y1="21" x2="16.65" y2="16.65"></line>
                  </svg>
                  Búsqueda Global
                </button>
              </div>
              <div v-if="!selectedUser" class="tecnm-user-search-wrapper">
                <input
                  v-model="userSearchQuery"
                  type="text"
                  class="tecnm-form-control"
                  placeholder="Escribe nombre, correo institucional o N° control..."
                  @input="onSearchUsers"
                />
                <div v-if="isSearchingUsers" class="tecnm-help-text" style="margin-top:4px;">
                  Buscando usuarios institucionales...
                </div>
                <!-- Dropdown de resultados -->
                <div v-if="userSearchResults.length > 0" class="tecnm-user-dropdown">
                  <div
                    v-for="u in userSearchResults"
                    :key="u.id"
                    class="tecnm-user-option"
                    @click="selectTargetUser(u)"
                  >
                    <div class="tecnm-user-option-header">
                      <span class="tecnm-user-option-name">{{ u.fullName }}</span>
                      <span class="tecnm-user-option-role">{{ u.role }}</span>
                    </div>
                    <div class="tecnm-user-option-sub">
                      <span>{{ u.email }}</span>
                      <span v-if="u.controlNumber"> &bull; N°: {{ u.controlNumber }}</span>
                    </div>
                  </div>
                </div>
              </div>

              <!-- Tarjeta de usuario seleccionado -->
              <div v-else class="tecnm-selected-user-card">
                <div class="tecnm-selected-user-info">
                  <span class="tecnm-selected-user-name">🎯 {{ selectedUser.fullName || selectedUser.email }}</span>
                  <span class="tecnm-selected-user-email">{{ selectedUser.email }} &bull; Rol: {{ selectedUser.role }}</span>
                </div>
                <button type="button" class="tecnm-selected-user-remove" title="Cambiar usuario" @click="clearTargetUser">
                  &times;
                </button>
              </div>

              <div class="tecnm-hint-box" style="margin-top: 10px;">
                ℹ️ <strong>Modo pruebas unitario:</strong> El aviso y correo institucional se despacharán <strong>únicamente</strong> a este usuario. No se filtrará por carrera ni por modalidad.
              </div>
            </div>

            <!-- Panel para Modo Broadcast -->
            <template v-else>
              <!-- Roles Destino (Checkboxes) -->
              <div class="tecnm-form-group">
                <label class="tecnm-label">
                  Roles Destinatarios <span class="tecnm-required">*</span>
                </label>
                <div class="tecnm-checkbox-grid">
                  <label
                    v-for="r in availableRoles"
                    :key="r.code"
                    class="tecnm-checkbox-card"
                    :class="{ 'is-selected': form.targetRoles.includes(r.code) }"
                  >
                    <input
                      type="checkbox"
                      :value="r.code"
                      :checked="form.targetRoles.includes(r.code)"
                      @change="handleRoleToggle(r.code)"
                    />
                    <span class="tecnm-checkbox-label">{{ r.label }}</span>
                  </label>
                </div>
              </div>

              <!-- Nota para rol Dirección -->
              <div v-if="form.targetRoles.includes('director')" class="tecnm-hint-box" style="margin-bottom: 12px;">
                🏛️ <strong>Rol Dirección:</strong> La Dirección no pertenece a una carrera específica. Recibirá el aviso sin requerir ni estar limitada por filtro de carrera.
              </div>

              <!-- Filtros Segmentados Opcionales (Carrera y Modalidad) -->
              <div class="tecnm-form-row">
                <div class="tecnm-form-group">
                  <label for="filterCareer" class="tecnm-label">Carrera Específica (Opcional)</label>
                  <select
                    id="filterCareer"
                    v-model="form.careerId"
                    class="tecnm-form-control tecnm-select"
                    :disabled="isDirectorOnly"
                  >
                    <option value="">{{ isDirectorOnly ? '(No aplica a Dirección)' : 'Todas las Carreras' }}</option>
                    <option v-for="c in careers" :key="c.id" :value="c.id">
                      {{ c.name }} ({{ c.code }})
                    </option>
                  </select>
                </div>

                <div class="tecnm-form-group">
                  <label for="filterModality" class="tecnm-label">Modalidad de Residencia (Opcional)</label>
                  <select id="filterModality" v-model="form.residencyModality" class="tecnm-form-control tecnm-select">
                    <option value="">Todas las Modalidades</option>
                    <option value="regular">Residencia Regular / Empresa</option>
                    <option value="acreditacion">Acreditación InnovaTecNM / HackaTec</option>
                  </select>
                </div>
              </div>
            </template>

            <!-- Botón Emitir -->
            <div class="tecnm-form-actions">
              <button
                type="submit"
                id="btnSubmitNotice"
                class="tecnm-btn tecnm-btn-primary"
                :disabled="isSubmitting"
              >
                <span v-if="isSubmitting">Emitiendo...</span>
                <span v-else>Emitir Notificación</span>
              </button>
            </div>
          </form>
        </section>

        <!-- Tarjeta 2: Historial de Emisiones -->
        <section class="tecnm-card tecnm-history-card">
          <div class="tecnm-card-header">
            <h2 class="tecnm-card-title">Historial de Notificaciones</h2>
            <span class="tecnm-badge-count">{{ totalCount }} emitidas</span>
          </div>

          <!-- Filtros de búsqueda en historial -->
          <div class="tecnm-history-filters">
            <input
              v-model="filters.search"
              type="text"
              class="tecnm-form-control"
              placeholder="Buscar por tema o texto..."
              @input="loadHistory"
            />
            <select v-model="filters.role" class="tecnm-form-control tecnm-select" @change="loadHistory">
              <option value="">Todos los roles</option>
              <option v-for="r in availableRoles" :key="r.code" :value="r.code">
                {{ r.label }}
              </option>
            </select>
            <select v-model="filters.careerId" class="tecnm-form-control tecnm-select" @change="loadHistory">
              <option value="">Todas las carreras</option>
              <option v-for="c in careers" :key="c.id" :value="c.id">
                {{ c.name }} ({{ c.code }})
              </option>
            </select>
            <label class="tecnm-filter-checkbox">
              <input type="checkbox" v-model="filters.activeOnly" @change="loadHistory" />
              <span>Solo vigentes</span>
            </label>
          </div>

          <!-- Tabla de Historial -->
          <div class="tecnm-table-responsive">
            <table class="tecnm-table">
              <thead>
                <tr>
                  <th>Fecha de Envío</th>
                  <th>Tema y Contenido</th>
                  <th>Destinatarios</th>
                  <th>Vigencia</th>
                  <th>Emisor</th>
                  <th>Acciones</th>
                </tr>
              </thead>
              <tbody>
                <tr v-if="isLoadingHistory">
                  <td colspan="6" class="tecnm-table-empty">Cargando historial de notificaciones...</td>
                </tr>
                <tr v-else-if="historyList.length === 0">
                  <td colspan="6" class="tecnm-table-empty">No se han emitido notificaciones registradas.</td>
                </tr>
                <tr v-for="item in historyList" :key="item.id">
                  <td class="tecnm-table-date">
                    {{ formatDate(item.createdAt) }}
                  </td>
                  <td>
                    <div class="tecnm-notice-cell">
                      <strong class="tecnm-notice-cell-title">{{ item.title }}</strong>
                      <p class="tecnm-notice-cell-desc">{{ item.description }}</p>
                    </div>
                  </td>
                  <td>
                    <div v-if="item.targetUserId" class="tecnm-tags-cluster">
                      <span class="tecnm-tag tag-individual">
                        🎯 {{ item.targetUserName || 'Usuario Individual' }}
                      </span>
                      <span v-if="item.targetUserEmail" class="tecnm-tag">
                        {{ item.targetUserEmail }}
                      </span>
                    </div>
                    <div v-else class="tecnm-tags-cluster">
                      <span
                        v-for="r in item.targetRolesList"
                        :key="r"
                        class="tecnm-tag"
                      >
                        {{ getRoleLabel(r) }}
                      </span>
                      <span v-if="item.careerName" class="tecnm-tag tag-career">
                        {{ item.careerName }}
                      </span>
                      <span v-if="item.residencyModality" class="tecnm-tag tag-modality">
                        {{ getModalityLabel(item.residencyModality) }}
                      </span>
                    </div>
                  </td>
                  <td>
                    <div class="tecnm-expiry-cell">
                      <span>{{ formatDate(item.expiresAt) }}</span>
                      <span
                        class="tecnm-badge-status"
                        :class="item.isExpired ? 'badge-expired' : 'badge-active'"
                      >
                        {{ item.isExpired ? 'Expirada' : 'Vigente' }}
                      </span>
                    </div>
                  </td>
                  <td class="tecnm-sender-cell">
                    {{ item.senderName }}
                  </td>
                  <td class="tecnm-actions-cell">
                    <button
                      type="button"
                      class="tecnm-btn-icon-danger"
                      title="Eliminar aviso"
                      aria-label="Eliminar aviso"
                      @click="deleteNotification(item)"
                    >
                      <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="1.8" stroke="currentColor">
                        <path stroke-linecap="round" stroke-linejoin="round" d="m14.74 9-.346 9m-4.788 0L9.26 9m9.968-3.21c.342.052.682.107 1.022.166m-1.022-.165L18.16 19.673a2.25 2.25 0 0 1-2.244 2.077H8.084a2.25 2.25 0 0 1-2.244-2.077L4.772 5.79m14.456 0a48.108 48.108 0 0 0-3.478-.397m-12 .562c.34-.059.68-.114 1.022-.165m0 0a48.11 48.11 0 0 1 3.478-.397m7.5 0v-.916c0-1.18-.91-2.164-2.09-2.201a51.964 51.964 0 0 0-3.32 0c-1.18.037-2.09 1.022-2.09 2.201v.916m7.5 0a48.667 48.667 0 0 0-7.5 0" />
                      </svg>
                    </button>
                  </td>
                </tr>
              </tbody>
            </table>
          </div>

          <!-- Paginador institucional -->
          <TecnmPagination
            :current-page="currentPage"
            :total-pages="totalPages"
            :total-count="totalCount"
            :page-size="pageSize"
            @update:current-page="currentPage = $event"
            @page-change="loadHistory"
          />
        </section>
      </div>
    </div>
  </div>
</template>
