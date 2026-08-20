(() => {
  'use strict';

  const API_BASE = '/api/v1/auth';
  const STORAGE_KEY = 'authToken';
  const USER_KEY = 'authUser';

  const form = document.getElementById('loginForm');
  const emailInput = document.getElementById('email');
  const passwordInput = document.getElementById('password');
  const loginBtn = document.getElementById('loginBtn');
  const loginBtnText = document.getElementById('loginBtnText');
  const loginBtnSpinner = document.getElementById('loginBtnSpinner');
  const loginAlert = document.getElementById('loginAlert');
  const loginAlertMessage = document.getElementById('loginAlertMessage');

  function isValidEmail(email) {
    const emailRegex = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
    return emailRegex.test(email);
  }

  function showAlert(message) {
    loginAlertMessage.textContent = message;
    loginAlert.classList.add('visible');
  }

  function hideAlert() {
    loginAlert.classList.remove('visible');
  }

  function setLoading(loading) {
    loginBtn.disabled = loading;
    loginBtnText.style.display = loading ? 'none' : 'inline';
    loginBtnSpinner.style.display = loading ? 'inline-block' : 'none';
    emailInput.disabled = loading;
    passwordInput.disabled = loading;
  }

  function getRedirectPath(role) {
    return '/dashboard';
  }

  function storeSession(data) {
    sessionStorage.setItem(STORAGE_KEY, data.token);
    sessionStorage.setItem(USER_KEY, JSON.stringify(data.user));
  }

  function checkExistingSession() {
    const token = sessionStorage.getItem(STORAGE_KEY);
    const userStr = sessionStorage.getItem(USER_KEY);

    if (token && userStr) {
      try {
        const user = JSON.parse(userStr);
        if (user.role) {
          window.location.href = getRedirectPath(user.role);
        }
      } catch {
        sessionStorage.clear();
      }
    }
  }

  async function handleLogin(e) {
    e.preventDefault();
    hideAlert();

    const email = emailInput.value.trim();
    const password = passwordInput.value;

    if (!email) {
      showAlert('Ingrese su correo electrónico.');
      emailInput.focus();
      return;
    }

    if (!isValidEmail(email)) {
      showAlert('Ingrese un correo electrónico válido.');
      emailInput.focus();
      return;
    }

    if (!password) {
      showAlert('Ingrese su contraseña.');
      passwordInput.focus();
      return;
    }

    setLoading(true);

    try {
      const response = await fetch(`${API_BASE}/login`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ email, password })
      });

      let data = {};
      try {
        data = await response.json();
      } catch {
        // Ignorar error de desmarcado si la respuesta no es JSON válido
      }

      if (!response.ok) {
        const message = data.message || `Error en el servidor (${response.status}). Intente nuevamente.`;
        showAlert(message);
        setLoading(false);
        return;
      }

      storeSession(data);
      window.location.href = getRedirectPath(data.user.role);

    } catch (error) {
      showAlert('Error de conexión. Intente nuevamente.');
      setLoading(false);
    }
  }

  form.addEventListener('submit', handleLogin);
  emailInput.addEventListener('input', hideAlert);
  passwordInput.addEventListener('input', hideAlert);

  checkExistingSession();
})();
