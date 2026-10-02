<script setup>
import { ref, computed, onMounted, onUnmounted } from 'vue'
import { useRouter } from 'vue-router'
import apiClient from '@/services/api'
import { useAuthStore } from '@/stores/auth'

const router = useRouter()
const authStore = useAuthStore()

const isOpen = ref(false)
const isLoading = ref(false)
const notifications = ref([])
let pollTimer = null

const pendingCount = computed(() => notifications.value.length)

async function loadNotifications() {
  if (!authStore.isAuthenticated) {
    notifications.value = []
    return
  }

  try {
    isLoading.value = true
    const response = await apiClient.get('/v1/notifications/pending')
    const list = Array.isArray(response.data) ? response.data : []

    // Filtrar descartes locales de notificaciones sintéticas del sistema
    const dismissedSynthetic = JSON.parse(localStorage.getItem('tecnm_dismissed_alerts') || '[]')
    notifications.value = list.filter((n) => !dismissedSynthetic.includes(n.id))
  } catch (err) {
    console.error('Error cargando notificaciones pendientes:', err)
  } finally {
    isLoading.value = false
  }
}

function toggleDropdown() {
  isOpen.value = !isOpen.value
}

function closeDropdown() {
  isOpen.value = false
}

async function markAsRead(item) {
  try {
    if (item.id > 0) {
      await apiClient.patch(`/v1/notifications/${item.id}/read`)
    } else {
      // Notificación sintética de sistema: guardar en localStorage para no volver a mostrar en esta sesión
      const dismissed = JSON.parse(localStorage.getItem('tecnm_dismissed_alerts') || '[]')
      dismissed.push(item.id)
      localStorage.setItem('tecnm_dismissed_alerts', JSON.stringify(dismissed))
    }

    notifications.value = notifications.value.filter((n) => n.id !== item.id)
    if (notifications.value.length === 0) {
      isOpen.value = false
    }
  } catch (err) {
    console.error('Error al marcar notificación como leída:', err)
  }
}

function handleAction(item) {
  if (item.actionUrl) {
    closeDropdown()
    router.push(item.actionUrl)
  }
}

function handleClickOutside(e) {
  if (!e.target.closest('.tecnm-notification-container')) {
    closeDropdown()
  }
}

function handleKeydown(e) {
  if (e.key === 'Escape') {
    closeDropdown()
  }
}

function formatNotificationDate(dateStr) {
  if (!dateStr) return ''
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

onMounted(() => {
  loadNotifications()
  document.addEventListener('click', handleClickOutside)
  document.addEventListener('keydown', handleKeydown)
  pollTimer = setInterval(loadNotifications, 60000) // Actualizar cada 60s
})

onUnmounted(() => {
  document.removeEventListener('click', handleClickOutside)
  document.removeEventListener('keydown', handleKeydown)
  if (pollTimer) clearInterval(pollTimer)
})
</script>

<template>
  <!-- El módulo es invisible si no hay notificaciones pendientes -->
  <div v-if="pendingCount > 0" class="tecnm-notification-container">
    <button
      type="button"
      id="notificationBellBtn"
      class="tecnm-notification-btn"
      :class="{ 'is-active': isOpen }"
      :aria-expanded="isOpen"
      aria-label="Notificaciones pendientes"
      title="Tienes notificaciones pendientes"
      @click="toggleDropdown"
    >
      <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="1.8" stroke="currentColor" class="tecnm-bell-icon">
        <path stroke-linecap="round" stroke-linejoin="round" d="M14.857 17.082a23.848 23.848 0 0 0 5.454-1.31A8.967 8.967 0 0 1 18 9.75V9A6 6 0 0 0 6 9v.75a8.967 8.967 0 0 1-2.312 6.022c1.733.64 3.56 1.085 5.455 1.31m5.714 0a24.255 24.255 0 0 1-5.714 0m5.714 0a3 3 0 1 1-5.714 0" />
      </svg>
      <span class="tecnm-notification-badge">{{ pendingCount }}</span>
    </button>

    <!-- Dropdown de Notificaciones -->
    <transition name="tecnm-fade">
      <div v-if="isOpen" class="tecnm-notification-dropdown" role="region" aria-label="Lista de notificaciones">
        <div class="tecnm-notification-header">
          <div class="tecnm-notification-header-title">
            <span class="tecnm-notification-title-text">Avisos y Notificaciones</span>
            <span class="tecnm-notification-count-tag">{{ pendingCount }} pendientes</span>
          </div>
          <button
            type="button"
            class="tecnm-notification-close-btn"
            aria-label="Cerrar notificaciones"
            @click="closeDropdown"
          >
            &times;
          </button>
        </div>

        <div class="tecnm-notification-list">
          <article
            v-for="item in notifications"
            :key="item.id"
            class="tecnm-notification-card"
            :class="{ 'is-system': item.type === 'system' }"
          >
            <div class="tecnm-notification-meta">
              <span
                class="tecnm-notification-source-badge"
                :class="item.type === 'system' ? 'badge-system' : 'badge-manual'"
              >
                {{ item.source || 'Aviso' }}
              </span>
              <span v-if="item.expiresAt" class="tecnm-notification-expiry">
                Vence: {{ formatNotificationDate(item.expiresAt) }}
              </span>
            </div>

            <h4 class="tecnm-notification-item-title">{{ item.title }}</h4>
            <p class="tecnm-notification-desc">{{ item.description }}</p>

            <div class="tecnm-notification-actions">
              <button
                v-if="item.actionUrl"
                type="button"
                class="tecnm-notification-action-link"
                @click="handleAction(item)"
              >
                Ver módulo
              </button>
              <button
                type="button"
                class="tecnm-notification-dismiss-btn"
                title="Marcar como leída y quitar de la campana"
                @click="markAsRead(item)"
              >
                <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="2" stroke="currentColor" class="tecnm-check-icon">
                  <path stroke-linecap="round" stroke-linejoin="round" d="m4.5 12.75 6 6 9-13.5" />
                </svg>
                <span>Entendido</span>
              </button>
            </div>
          </article>
        </div>
      </div>
    </transition>
  </div>
</template>
