<script setup>
import { computed } from 'vue'
import { useRouter, useRoute } from 'vue-router'
import { useAuthStore } from '@/stores/auth'
import { useGlobalSearch } from '@/composables/useGlobalSearch'
import isologoPath from '@/assets/images/tecnm-isologo.svg'

const router = useRouter()
const route = useRoute()
const authStore = useAuthStore()
const { open: openSearch } = useGlobalSearch()

const isPublicRoute = computed(() => route.meta.isPublic === true)

function handleLogout() {
  authStore.logout()
  router.push('/auth/login')
}
</script>

<template>
  <header class="tecnm-header">
    <div class="tecnm-container">
      <div class="tecnm-header-inner">
        <!-- Logo Institucional -->
        <div class="tecnm-brand">
          <img
            :src="isologoPath"
            alt="Tecnológico Nacional de México"
            class="tecnm-brand-logo"
          />
          <div class="tecnm-brand-text">
            <span class="tecnm-brand-title">Tecnológico Nacional de México</span>
            <span class="tecnm-brand-subtitle">Sistema de Residencias Profesionales</span>
          </div>
        </div>

        <!-- Acciones del Header -->
        <div class="tecnm-header-actions">
          <!-- Vista Pública (Login) -->
          <template v-if="isPublicRoute || !authStore.isAuthenticated">
            <span class="tecnm-campus">Campus Monclova</span>
          </template>

          <!-- Vista Autenticada -->
          <template v-else>
            <div class="user-menu">
              <!-- Botón Búsqueda Universal (Ctrl + K) -->
              <button
                v-if="authStore.currentRole !== 'student'"
                type="button"
                id="globalSearchTriggerBtn"
                class="tecnm-btn tecnm-btn-sm tecnm-btn-outline"
                title="Búsqueda Universal (Ctrl + K)"
                @click="openSearch()"
              >
                <svg xmlns="http://www.w3.org/2000/svg" width="16" height="16" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
                  <path stroke-linecap="round" stroke-linejoin="round" d="m21 21-5.197-5.197m0 0A7.5 7.5 0 1 0 5.196 5.196a7.5 7.5 0 0 0 10.607 10.607Z" />
                </svg>
                <span>Buscar (Ctrl+K)</span>
              </button>

              <!-- Perfil de Usuario -->
              <div class="user-profile">
                <span id="userAvatar" class="user-avatar" aria-hidden="true">{{ authStore.userAvatarInitials }}</span>
                <span class="user-profile-info">
                  <span id="userDisplayName" class="user-profile-name">{{ authStore.userDisplayName }}</span>
                  <span id="userRoleDisplay" class="user-profile-role">{{ authStore.roleLabel }}</span>
                </span>
              </div>

              <!-- Botón Cerrar Sesión -->
              <button
                type="button"
                id="logoutBtn"
                class="user-logout-btn"
                aria-label="Cerrar Sesión"
                title="Cerrar Sesión"
                @click="handleLogout"
              >
                <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="2" stroke="currentColor" aria-hidden="true">
                  <path stroke-linecap="round" stroke-linejoin="round" d="M15.75 9V5.25A2.25 2.25 0 0 0 13.5 3h-6a2.25 2.25 0 0 0-2.25 2.25v13.5A2.25 2.25 0 0 0 7.5 21h6a2.25 2.25 0 0 0 2.25-2.25V15m3 0 3-3m0 0-3-3m3 3H9" />
                </svg>
              </button>
            </div>
          </template>
        </div>
      </div>
    </div>
  </header>
</template>
