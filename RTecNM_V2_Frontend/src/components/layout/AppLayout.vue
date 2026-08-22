<script setup>
import { computed } from 'vue'
import { useRoute } from 'vue-router'
import { useAuthStore } from '@/stores/auth'
import AppHeader from '@/components/layout/AppHeader.vue'
import AppNavbar from '@/components/layout/AppNavbar.vue'
import AppFooter from '@/components/layout/AppFooter.vue'
import ConfirmModal from '@/components/common/ConfirmModal.vue'
import AuditModal from '@/components/common/AuditModal.vue'
import GlobalSearchModal from '@/components/search/GlobalSearchModal.vue'

const route = useRoute()
const authStore = useAuthStore()

const isPublicRoute = computed(() => route.meta.isPublic === true)
</script>

<template>
  <div class="tecnm-page-layout">
    <!-- Header -->
    <AppHeader />

    <!-- Navbar Institucional (solo usuarios autenticados) -->
    <AppNavbar v-if="authStore.isAuthenticated && !isPublicRoute" />

    <!-- Contenido Principal -->
    <main v-if="isPublicRoute" class="tecnm-public-main">
      <router-view />
    </main>
    <main v-else class="tecnm-main-content">
      <div class="tecnm-container">
        <router-view />
      </div>
    </main>

    <!-- Footer -->
    <AppFooter />

    <!-- Modales Globales -->
    <ConfirmModal />
    <AuditModal />
    <GlobalSearchModal v-if="authStore.isAuthenticated" />
  </div>
</template>
