<script setup>
import { ref } from 'vue'
import { useRouter, useRoute } from 'vue-router'
import { useAuthStore } from '@/stores/auth'

const router = useRouter()
const route = useRoute()
const authStore = useAuthStore()

const email = ref('')
const password = ref('')
const isLoading = ref(false)
const errorMessage = ref('')
const blockDetails = ref(null)

function isValidEmail(val) {
  const regex = /^[^\s@]+@[^\s@]+\.[^\s@]+$/
  return regex.test(val)
}

function parseBlockReason(message) {
  if (!message) return null
  
  // Check if message contains requirement failures
  const requirements = {
    'Actividades Complementarias': message.includes('Actividades Complementarias'),
    'Servicio Social': message.includes('Servicio Social'),
    'Especiales': message.includes('Especiales')
  }
  
  const failedRequirements = Object.entries(requirements)
    .filter(([_, failed]) => failed)
    .map(([name]) => name)
  
  const isManualBlock = message.includes('[Bloqueo manual]')
  const cleanMessage = message.replace('[Bloqueo manual] ', '')
  
  return {
    isManualBlock,
    failedRequirements,
    message: cleanMessage
  }
}

async function handleLogin() {
  errorMessage.value = ''
  blockDetails.value = null

  const cleanEmail = email.value.trim()
  if (!cleanEmail) {
    errorMessage.value = 'Ingrese su correo electrónico.'
    return
  }

  if (!isValidEmail(cleanEmail)) {
    errorMessage.value = 'Ingrese un correo electrónico institucional válido.'
    return
  }

  if (!password.value) {
    errorMessage.value = 'Ingrese su contraseña.'
    return
  }

  isLoading.value = true

  try {
    await authStore.login(cleanEmail, password.value)
    const redirectPath = route.query.redirect || '/dashboard'
    router.push(redirectPath)
  } catch (error) {
    const msg =
      error.response?.data?.message ||
      (error.response?.status === 401
        ? 'Correo electrónico o contraseña incorrectos.'
        : 'Error al conectar con el servidor. Intente nuevamente.')
    
    // Check if it's a block reason (403)
    if (error.response?.status === 403) {
      blockDetails.value = parseBlockReason(msg)
      errorMessage.value = msg
    } else {
      errorMessage.value = msg
    }
  } finally {
    isLoading.value = false
  }
}
</script>

<template>
  <div class="login-main">
    <div class="login-card">
      <div class="login-icon">
        <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="1.5" stroke="currentColor">
          <path stroke-linecap="round" stroke-linejoin="round" d="M16.5 10.5V6.75a4.5 4.5 0 1 0-9 0v3.75m-.75 11.25h10.5a2.25 2.25 0 0 0 2.25-2.25v-6.75a2.25 2.25 0 0 0-2.25-2.25H6.75a2.25 2.25 0 0 0-2.25 2.25v6.75a2.25 2.25 0 0 0 2.25 2.25Z" />
        </svg>
      </div>

      <h1 class="login-title">Sistema de Residencias Profesionales TecNM</h1>
      <p class="login-subtitle">Inicio de Sesión</p>

      <div
        v-if="errorMessage"
        id="loginAlert"
        class="tecnm-alert tecnm-alert-danger login-alert visible"
        role="alert"
      >
        <span id="loginAlertMessage">{{ errorMessage }}</span>
        
        <div v-if="blockDetails && !blockDetails.isManualBlock && blockDetails.failedRequirements.length > 0" class="block-details mt-3">
          <h4 class="block-details-title">Requisitos no cumplidos:</h4>
          <ul class="block-requirements-list">
            <li v-for="req in blockDetails.failedRequirements" :key="req" class="block-requirement-item">
              <span class="requirement-icon">✗</span>
              <span class="requirement-name">{{ req }}</span>
            </li>
          </ul>
          <p class="block-details-note">Debe regularizar su situación académica para acceder al sistema.</p>
        </div>
        
        <div v-else-if="blockDetails && blockDetails.isManualBlock" class="block-details mt-3">
          <h4 class="block-details-title">Cuenta bloqueada por administrador:</h4>
          <p class="block-manual-reason">{{ blockDetails.message }}</p>
          <p class="block-details-note">Contacte a la administración para resolver esta situación.</p>
        </div>
      </div>

      <form id="loginForm" class="login-form" @submit.prevent="handleLogin">
        <div class="tecnm-form-group">
          <label for="email" class="tecnm-label">Correo institucional</label>
          <input
            id="email"
            v-model="email"
            type="email"
            name="email"
            class="tecnm-form-control"
            placeholder="ejemplo@monclova.tecnm.mx"
            autocomplete="email"
            :disabled="isLoading"
            required
            @input="errorMessage = ''"
          />
        </div>

        <div class="tecnm-form-group">
          <label for="password" class="tecnm-label">Contraseña</label>
          <input
            id="password"
            v-model="password"
            type="password"
            name="password"
            class="tecnm-form-control"
            placeholder="••••••••"
            autocomplete="current-password"
            :disabled="isLoading"
            required
            @input="errorMessage = ''"
          />
        </div>

        <button
          id="loginBtn"
          type="submit"
          class="tecnm-btn tecnm-btn-primary login-btn"
          :disabled="isLoading"
        >
          <span v-if="!isLoading" id="loginBtnText">Iniciar Sesión</span>
          <span v-else id="loginBtnSpinner" class="login-spinner"></span>
        </button>
      </form>
    </div>
  </div>
</template>
