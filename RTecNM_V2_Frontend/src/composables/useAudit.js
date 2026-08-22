import { ref } from 'vue'
import apiClient from '@/services/api'

const isVisible = ref(false)
const auditTitle = ref('Auditoría del Registro')
const auditRows = ref([])
const userNamesCache = ref({})

const MONTH_NAMES_ES = [
  'Enero', 'Febrero', 'Marzo', 'Abril', 'Mayo', 'Junio',
  'Julio', 'Agosto', 'Septiembre', 'Octubre', 'Noviembre', 'Diciembre'
]

export function useAudit() {
  function formatAuditDate(iso) {
    if (!iso) return '—'
    const d = new Date(iso)
    if (isNaN(d.getTime())) return '—'
    const day = String(d.getDate()).padStart(2, '0')
    const monthName = MONTH_NAMES_ES[d.getMonth()]
    const year = d.getFullYear()
    return `${day}/${monthName}/${year}`
  }

  async function loadUserNames(ids) {
    const missing = (ids || []).filter(
      (id) => id && !(id in userNamesCache.value)
    )
    if (!missing.length) return

    try {
      const res = await apiClient.get(
        `/v1/auth/users/names?ids=${missing.join(',')}`
      )
      if (res.data) {
        Object.entries(res.data).forEach(([id, name]) => {
          userNamesCache.value[id] = name
        })
      }
    } catch {
      // Si falla, el formateador mostrará "Usuario #id"
    }
  }

  function formatAuditUser(id) {
    if (!id) return 'Sistema (semilla)'
    const val = userNamesCache.value[id]
    if (!val) return `Usuario #${id}`
    if (val.includes('@')) {
      const local = val.split('@')[0] || ''
      const words = local.split(/[._\-\d]+/).filter(Boolean)
      return words.length
        ? words.map((w) => w.charAt(0).toUpperCase() + w.slice(1)).join(' ')
        : local
    }
    return val
  }

  async function showAudit({ title = 'Auditoría del Registro', item = {}, rows = null } = {}) {
    auditTitle.value = title

    // Si se pasan filas personalizadas
    if (Array.isArray(rows) && rows.length > 0) {
      auditRows.value = rows
      isVisible.value = true
      return
    }

    // Recolectar IDs de usuarios para resolver nombres
    const userIds = [item.createdBy, item.updatedBy, item.deletedBy].filter(Boolean)
    if (userIds.length) {
      await loadUserNames(userIds)
    }

    // Estructura completa oficial de 10 campos de auditoría
    const builtRows = [
      { label: 'ID', value: item.id != null ? item.id : '—' },
    ]

    if (item.title) {
      builtRows.push({ label: 'Título', value: item.title })
    } else if (item.name) {
      builtRows.push({ label: 'Nombre', value: item.name })
    }

    builtRows.push(
      { label: 'Estado', value: item.isActive !== false ? 'Activo' : 'Inactivo' },
      { label: 'Visible', value: item.isVisible !== false ? 'Sí' : 'No' },
      { label: 'Orden', value: item.displayOrder != null ? item.displayOrder : 0 },
      { label: 'Creado el', value: formatAuditDate(item.createdAt) },
      { label: 'Creado por', value: formatAuditUser(item.createdBy) },
      { label: 'Actualizado el', value: item.updatedBy ? formatAuditDate(item.updatedAt) : '—' },
      { label: 'Actualizado por', value: item.updatedBy ? formatAuditUser(item.updatedBy) : '—' },
      { label: 'Eliminado el', value: item.deletedAt ? formatAuditDate(item.deletedAt) : '—' },
      { label: 'Eliminado por', value: item.deletedBy ? formatAuditUser(item.deletedBy) : '—' }
    )

    auditRows.value = builtRows
    isVisible.value = true
  }

  function close() {
    isVisible.value = false
  }

  return {
    isVisible,
    auditTitle,
    auditRows,
    formatAuditDate,
    formatAuditUser,
    loadUserNames,
    showAudit,
    close,
  }
}
