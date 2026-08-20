(() => {
  'use strict';

  const params = new URLSearchParams(window.location.search);
  const studentId = params.get('id');
  const url = studentId ? `/api/v1/students/${studentId}` : '/api/v1/students/me';
  if (!studentId && !window.location.pathname.endsWith('/profile')) return;

  fetch(url)
    .then(res => res.ok ? res.json() : Promise.reject())
    .then(s => {
      const fullName = [s.firstName, s.lastName, s.lastName2].filter(Boolean).join(' ').trim();
      document.getElementById('displayControlNumber').textContent = s.controlNumber || '--';
      document.getElementById('displayName').textContent = fullName || '--';
      document.getElementById('displayEmail').textContent = s.email || '--';
      document.getElementById('displayGpa').textContent = s.gpa != null ? s.gpa.toFixed(1) : '--';
    })
    .catch(() => {
      document.getElementById('displayName').textContent = 'No se pudo cargar el expediente.';
    });
})();
