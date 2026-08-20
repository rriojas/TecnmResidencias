# 08 - TecNM Graphic Identity & Centralized UI Design System Specification

System: TecNM Professional Residency System (v2)  
Reference Standard: **Manual de Identidad Gráfica TecNM 2024** (https://iguala.tecnm.mx/pdf/Manual_Identidad_Grafica_TecNM_2024.pdf)  
Language: Spanish (es-MX) / CSS Custom Properties

---

## 1. Centralized CSS Architecture

All styles across the application MUST be strictly centralized and inherited from core CSS files located in `public/assets/css/`. In-line styles (`style="..."`) and ad-hoc utility classes are strictly prohibited.

### Core Style Directory & Load Order:
1. `public/assets/css/tecnm-theme.css`: Defines all `:root` CSS Design Tokens (Colors, Typography, Spacing, Shadows, Border Radii, Transitions).
2. `public/assets/css/main.css`: Base component stylesheet implementing global reset, layout grid, typography classes, and TecNM component primitives.

---

## 2. Institutional Color Palette (TecNM 2024)

### Primary & Accent Brand Tokens (`tecnm-theme.css`)
```css
:root {
  /* Institutional Brand Colors (Manual TecNM 2024) */
  --tecnm-blue-primary: #1B396A;      /* Pantone 288 C - Dominant Header/Navbar */
  --tecnm-blue-dark: #0F2548;         /* Dark variant for footers & active states */
  --tecnm-blue-hover: #244B88;        /* Interactive hover state */
  --tecnm-gold-accent: #C5A059;       /* Pantone 117 C - Institutional Accent/Borders */
  --tecnm-gold-hover: #D4AF37;        /* Gold highlight hover */
  --tecnm-gold-light: #F7F3E9;        /* Soft gold container fill */

  /* Neutral Background & Surface Palette */
  --tecnm-bg-main: #F4F6F9;           /* App background tint */
  --tecnm-surface-white: #FFFFFF;     /* Cards, Modals, Form Containers */
  --tecnm-border-color: #E2E8F0;      /* Standard card and table borders */
  --tecnm-border-focus: #1B396A;      /* Input focus indicator */

  /* Typography Colors */
  --tecnm-text-primary: #1E293B;     /* Body & Headline text (High contrast) */
  --tecnm-text-secondary: #64748B;   /* Subtitles, hints & captions */
  --tecnm-text-on-blue: #FFFFFF;     /* White text over TecNM blue background */
  --tecnm-text-on-gold: #1B396A;     /* Dark text over TecNM gold */

  /* Functional / Semantic Status Palette */
  --tecnm-status-approved-bg: #D1FAE5;
  --tecnm-status-approved-text: #065F46;
  --tecnm-status-pending-bg: #FEF3C7;
  --tecnm-status-pending-text: #92400E;
  --tecnm-status-rejected-bg: #FEE2E2;
  --tecnm-status-rejected-text: #991B1B;
  --tecnm-status-info-bg: #E0F2FE;
  --tecnm-status-info-text: #075985;
}
```

---

## 3. Typography & Text Scale

- **Primary Font Family**: `'Montserrat', -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif`.
- **Text Scale Hierarchy**:
  - `H1 / Title`: `2.25rem` (36px), `font-weight: 700`, line-height `1.2`, color `var(--tecnm-blue-primary)`.
  - `H2 / Section Title`: `1.75rem` (28px), `font-weight: 600`, line-height `1.3`, color `var(--tecnm-blue-primary)`.
  - `H3 / Card Header`: `1.25rem` (20px), `font-weight: 600`, line-height `1.4`.
  - `H4 / Subtitle`: `1.1rem` (17.6px), `font-weight: 500`.
  - `Body`: `1rem` (16px), `font-weight: 400`, line-height `1.6`, color `var(--tecnm-text-primary)`.
  - `Small / Captions`: `0.875rem` (14px), `font-weight: 400`, color `var(--tecnm-text-secondary)`.

---

## 4. Centralized UI Component Classes (`main.css`)

### 4.1 Header & Brand Identity Banner (`.tecnm-header`)
- **Structure**: Deep TecNM Blue background (`#1B396A`), white text, gold border bottom (`3px solid #C5A059`).
- **Logos Safe Margin**: Minimum margin of 16px around the official TecNM logo mark on the left and institutional campus branding on the right.
- **Classes**:
  - `.tecnm-header`: Top bar wrapper.
  - `.tecnm-brand-title`: Institutional heading text ("TECNOLÓGICO NACIONAL DE MÉXICO").
  - `.tecnm-brand-subtitle`: Campus / Sub-heading text ("Sistema de Residencias Profesionales").

### 4.2 Navbar & Main Navigation (`.tecnm-navbar`)
- **Background**: `#1B396A` with active tab background `#0F2548` and top accent highlight `3px solid #C5A059`.
- **Classes**:
  - `.tecnm-nav-item`: Navigation item link.
  - `.tecnm-nav-item.active`: Active route indicator.

### 4.3 Buttons (`.tecnm-btn`)
- **Base Style**: `padding: 0.625rem 1.25rem`, `border-radius: 0.375rem`, `font-weight: 600`, `transition: all 0.2s ease-in-out`, cursor `pointer`.
- **Variants**:
  - `.tecnm-btn-primary`: Background `var(--tecnm-blue-primary)`, color `white`. Hover `var(--tecnm-blue-hover)`.
  - `.tecnm-btn-accent`: Background `var(--tecnm-gold-accent)`, color `var(--tecnm-blue-primary)`. Hover `var(--tecnm-gold-hover)`.
  - `.tecnm-btn-secondary`: Background `var(--tecnm-border-color)`, color `var(--tecnm-text-primary)`.
  - `.tecnm-btn-outline`: Background `transparent`, border `2px solid var(--tecnm-blue-primary)`, color `var(--tecnm-blue-primary)`.
  - `.tecnm-btn-danger`: Background `#DC2626`, color `white` (For soft-delete/deactivation actions).

### 4.4 Cards & Content Panels (`.tecnm-card`)
- **Style**: Background `var(--tecnm-surface-white)`, border `1px solid var(--tecnm-border-color)`, `border-radius: 0.5rem`, `box-shadow: 0 4px 6px -1px rgba(0, 0, 0, 0.05)`.
- **Header**: Gold top accent indicator (`border-top: 4px solid var(--tecnm-gold-accent)`).
- **Classes**: `.tecnm-card`, `.tecnm-card-header`, `.tecnm-card-body`, `.tecnm-card-footer`.

### 4.5 Data Tables (`.tecnm-table`)
- **Header (`<thead>`)**: Background `var(--tecnm-blue-primary)`, text `white`, text transform `uppercase`, font size `0.85rem`.
- **Rows (`<tbody>`)**: Alternating striping background `var(--tecnm-bg-main)` on even rows. Hover state `background: #EBF3FF`.
- **Classes**: `.tecnm-table`, `.tecnm-table-striped`, `.tecnm-table-responsive`.
- **Pagination (`.tecnm-pagination`)**: Every table that displays records MUST render a pager below it via `window.renderPagination(container, meta, onPage)` (`assets/js/layout.js`).
  - `.tecnm-pagination`: flex footer with `border-top`, info text and page buttons.
  - `.tecnm-pagination-info`: "Mostrando X–Y de Z registro(s)" caption (secondary text, `0.85rem`).
  - `.tecnm-pagination-btn`: page button (`min-width 2.15rem`, bordered, radius `var(--tecnm-radius-md)`); `.active` = filled `var(--tecnm-blue-primary)` with white text; `:disabled` = reduced opacity, `cursor: not-allowed`.
  - `.tecnm-pagination-ellipsis`: page-gap separator.
  - Pagination state lives per page (`pageNumber`, `pageSize` default 10); changing a filter resets to page 1.

### 4.6 Form Controls (`.tecnm-form-control`)
- **Input & Select**: Height `2.5rem`, border `1px solid #CBD5E1`, border-radius `0.375rem`, padding `0.5rem 0.75rem`.
- **Focus State**: Border color `var(--tecnm-blue-primary)`, box-shadow `0 0 0 3px rgba(27, 57, 106, 0.15)`.
- **Classes**: `.tecnm-form-group`, `.tecnm-label`, `.tecnm-form-control`, `.tecnm-form-error`.

### 4.7 Status Badges (`.tecnm-badge`)
- **Base Style**: `display: inline-flex`, `padding: 0.25rem 0.75rem`, `border-radius: 9999px`, `font-size: 0.75rem`, `font-weight: 600`.
- **Variants**:
  - `.tecnm-badge-approved`: Approved / Active (`#D1FAE5` bg, `#065F46` text).
  - `.tecnm-badge-pending`: Pending / Under Review (`#FEF3C7` bg, `#92400E` text).
  - `.tecnm-badge-rejected`: Rejected / Soft Deleted (`#FEE2E2` bg, `#991B1B` text).

### 4.8 Notifications & Alerts (`.tecnm-alert`)
- **Variants**: `.tecnm-alert-info`, `.tecnm-alert-success`, `.tecnm-alert-warning`, `.tecnm-alert-danger`.

---

## 5. Responsive Grid & Breakpoints

- **Extra Small (Mobile)**: `< 640px` (Single column layout, stacked tables).
- **Medium (Tablet)**: `640px - 1024px` (2 column grid).
- **Large (Desktop)**: `> 1024px` (Standard desktop multi-column container, max-width `1280px` centered).

---

## 6. Accessibility & Contrast Standard (WCAG 2.1 AA)

1. All primary actions (`.tecnm-btn-primary`) guarantee a contrast ratio of at least `7:1` against white text.
2. Form fields include explicit `<label>` tags with matching `for` attributes.
3. Interactive elements provide clear focus rings using `var(--tecnm-blue-primary)`.

---

## 7. Date Formatting Standard

All dates rendered in the user interface (tables, cards, modal details, audit columns) MUST follow the standardized format:
- **Format**: `DD/NombreMes/YYYY` (e.g. `10/Agosto/2026`).
- **Implementation**: Provided globally via `window.formatTecNMDate(iso)` in `public/assets/js/layout.js`.

