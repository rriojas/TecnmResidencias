# 01 - Authentication Domain Specification (Frontend GUI - es-MX)

Module: Auth Views & GUI Components  
Language: Spanish (es-MX)  
Design Standard: [08-ui-design-system.md](file:///c:/Users/rrioj/source/repos/TecNM/ResidenciasTecNM/SISTEMARESIDENCIA/docs/specs/08-ui-design-system.md)

---

## 1. UI Components & Layouts

- **View Template**: `templates/auth/login.php`
- **Asset**: `public/assets/js/auth/login.js`
- **Centralized CSS Standards & Classes**:
  - Encabezado Institucional: `.tecnm-header`, `.tecnm-brand-title`
  - Contenedor Formulario: `.tecnm-card` con borde superior dorado (`var(--tecnm-gold-accent)`)
  - Campos de Entrada: `.tecnm-form-group`, `.tecnm-label`, `.tecnm-form-control`
  - Botón de Acción: `.tecnm-btn-primary` (Azul TecNM `#1B396A`)
  - Alert de Error: `.tecnm-alert-danger`

---

## 2. Text & Micro-copy Dictionary (es-MX)

- **Título Formulario**: "Sistema de Residencias Profesionales TecNM"
- **Subtítulo**: "Inicio de Sesión"
- **Etiqueta Correo**: "Correo institucional"
- **Placeholder Correo**: "ejemplo@monclova.tecnm.mx"
- **Nota Correo**: "Solo se aceptan correos institucionales (@monclova.tecnm.mx)."
- **Etiqueta Contraseña**: "Contraseña"
- **Boton Acceder**: "Iniciar Sesión"
- **Mensaje Error Credenciales**: "Correo electrónico o contraseña incorrectos."
- **Mensaje Error Cuenta Inactiva**: "Su cuenta se encuentra desactivada. Comuníquese con la Coordinación de Residencias."

---

## 3. Interaction & Client Validation

- Frontend validates format of institutional email prior to sending HTTP `POST`.
- On successful authentication, stores token in `sessionStorage` / HttpOnly cookie and redirects user based on role (`/dashboard/student`, `/dashboard/advisor`, `/dashboard/admin`).

