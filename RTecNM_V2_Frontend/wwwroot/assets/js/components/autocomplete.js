/**
 * Componente Reutilizable de Autocomplete para TecNM Residency System v2
 * Soporta búsqueda en tiempo real con debounce, navegación por teclado,
 * estado visual de selección, soporte para grandes volúmenes de datos y picker modal.
 */

(function () {
  'use strict';

  function escapeHtml(text) {
    if (!text) return '';
    return text.toString()
      .replace(/&/g, "&amp;")
      .replace(/</g, "&lt;")
      .replace(/>/g, "&gt;")
      .replace(/"/g, "&quot;")
      .replace(/'/g, "&#039;");
  }

  function highlightMatch(text, query) {
    if (!query || !text) return escapeHtml(text);
    const escapedText = escapeHtml(text);
    const regex = new RegExp(`(${query.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')})`, 'gi');
    return escapedText.replace(regex, '<span class="tecnm-autocomplete-highlight">$1</span>');
  }

  function getAuthHeaders() {
    const headers = { 'Content-Type': 'application/json' };
    const token = sessionStorage.getItem('authToken');
    if (token) headers['Authorization'] = `Bearer ${token}`;
    return headers;
  }

  window.initTecNMAutocomplete = function (options) {
    const {
      containerId,
      hiddenInputId,
      placeholder = 'Escriba para buscar...',
      endpoint,
      searchFn,
      searchParam = 'search',
      extraParams = {},
      minChars = 2,
      debounceMs = 250,
      titleExtractor = (item) => item.fullName || item.name || item.title || `Item #${item.id}`,
      subtitleExtractor = (item) => {
        if (item.controlNumber) return `No. Control: ${item.controlNumber}${item.career ? ' • ' + item.career : ''}`;
        if (item.rfc) return `RFC: ${item.rfc}${item.sector ? ' • ' + item.sector : ''}`;
        if (item.departmentName) return `Depto: ${item.departmentName}`;
        if (item.userEmail || item.email) return item.userEmail || item.email;
        return '';
      },
      valueExtractor = (item) => item.id,
      globalSearchSource = null,
      onSelect = null,
      onClear = null,
      initialItem = null
    } = options;

    const container = typeof containerId === 'string' ? document.getElementById(containerId) : containerId;
    if (!container) {
      console.warn(`[TecNM Autocomplete] Container "${containerId}" no encontrado.`);
      return null;
    }

    let hiddenInput = typeof hiddenInputId === 'string' ? document.getElementById(hiddenInputId) : hiddenInputId;
    if (!hiddenInput) {
      hiddenInput = document.createElement('input');
      hiddenInput.type = 'hidden';
      hiddenInput.id = typeof hiddenInputId === 'string' ? hiddenInputId : 'autocomplete_val_' + Date.now();
      container.appendChild(hiddenInput);
    }

    let selectedItem = null;
    let debounceTimer = null;
    let currentResults = [];
    let focusedIndex = -1;
    let currentAbortController = null;

    // Render component skeleton
    container.classList.add('tecnm-autocomplete-wrapper');
    container.innerHTML = `
      <div class="tecnm-autocomplete-input-group" id="${container.id}_inputGroup">
        <div style="position:relative; width:100%;">
          <input type="text" class="tecnm-autocomplete-input" placeholder="${escapeHtml(placeholder)}" autocomplete="off" spellcheck="false">
          <div class="tecnm-autocomplete-spinner" aria-hidden="true"></div>
        </div>
        ${globalSearchSource ? `
          <button type="button" class="tecnm-btn tecnm-btn-outline tecnm-btn-sm tecnm-autocomplete-picker-btn" title="Buscar en tabla completa">
            <svg xmlns="http://www.w3.org/2000/svg" width="14" height="14" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
              <path stroke-linecap="round" stroke-linejoin="round" d="m21 21-5.197-5.197m0 0A7.5 7.5 0 1 0 5.196 5.196a7.5 7.5 0 0 0 10.607 10.607Z" />
            </svg>
            <span>Buscar</span>
          </button>
        ` : ''}
      </div>
      <div class="tecnm-autocomplete-selected" id="${container.id}_selectedCard">
        <div class="tecnm-autocomplete-selected-info">
          <span class="tecnm-autocomplete-selected-title"></span>
          <span class="tecnm-autocomplete-selected-subtitle"></span>
        </div>
        <button type="button" class="tecnm-autocomplete-clear-btn" title="Cambiar selección" aria-label="Cambiar selección">&times;</button>
      </div>
      <div class="tecnm-autocomplete-dropdown" id="${container.id}_dropdown" role="listbox"></div>
    `;

    // Re-attach hidden input inside wrapper if it was replaced
    if (!container.contains(hiddenInput)) {
      container.appendChild(hiddenInput);
    }

    const inputGroup = container.querySelector('.tecnm-autocomplete-input-group');
    const input = container.querySelector('.tecnm-autocomplete-input');
    const selectedCard = container.querySelector('.tecnm-autocomplete-selected');
    const selectedTitle = selectedCard.querySelector('.tecnm-autocomplete-selected-title');
    const selectedSubtitle = selectedCard.querySelector('.tecnm-autocomplete-selected-subtitle');
    const clearBtn = selectedCard.querySelector('.tecnm-autocomplete-clear-btn');
    const dropdown = container.querySelector('.tecnm-autocomplete-dropdown');
    const pickerBtn = container.querySelector('.tecnm-autocomplete-picker-btn');

    function showDropdown(html) {
      dropdown.innerHTML = html;
      dropdown.classList.add('active');
    }

    function hideDropdown() {
      dropdown.classList.remove('active');
      dropdown.innerHTML = '';
      focusedIndex = -1;
    }

    function setSelected(item, triggerCallback = true) {
      if (!item) {
        clearSelection(triggerCallback);
        return;
      }

      selectedItem = item;
      const val = valueExtractor(item);
      const title = titleExtractor(item);
      const subtitle = subtitleExtractor(item);

      hiddenInput.value = val !== undefined && val !== null ? val : '';
      selectedTitle.textContent = title || `#${val}`;
      selectedSubtitle.textContent = subtitle || '';

      inputGroup.classList.add('has-selected');
      selectedCard.classList.add('active');
      input.value = '';
      hideDropdown();

      if (triggerCallback && typeof onSelect === 'function') {
        onSelect(item);
      }
    }

    function clearSelection(triggerCallback = true) {
      selectedItem = null;
      hiddenInput.value = '';
      selectedTitle.textContent = '';
      selectedSubtitle.textContent = '';

      selectedCard.classList.remove('active');
      inputGroup.classList.remove('has-selected');
      input.value = '';
      hideDropdown();

      if (triggerCallback && typeof onClear === 'function') {
        onClear();
      }
    }

    async function performSearch(query) {
      if (currentAbortController) {
        currentAbortController.abort();
      }

      container.classList.add('is-loading');
      showDropdown('<div class="tecnm-autocomplete-loading-msg">Buscando coincidencias...</div>');

      currentAbortController = new AbortController();

      try {
        let items = [];

        if (typeof searchFn === 'function') {
          items = await searchFn(query, currentAbortController.signal);
        } else if (endpoint) {
          const params = new URLSearchParams({
            [searchParam]: query,
            pageSize: 10,
            ...extraParams
          });
          const res = await fetch(`${endpoint}?${params}`, {
            headers: getAuthHeaders(),
            signal: currentAbortController.signal
          });

          if (!res.ok) throw new Error();
          const data = await res.json();
          items = Array.isArray(data) ? data : (data && Array.isArray(data.items) ? data.items : []);
        }

        container.classList.remove('is-loading');
        currentResults = items;
        focusedIndex = -1;

        if (items.length === 0) {
          showDropdown('<div class="tecnm-autocomplete-empty">No se encontraron resultados para "<strong>' + escapeHtml(query) + '</strong>"</div>');
          return;
        }

        const itemsHtml = items.map((item, index) => {
          const title = titleExtractor(item);
          const subtitle = subtitleExtractor(item);
          return `
            <div class="tecnm-autocomplete-item" data-index="${index}" role="option">
              <span class="tecnm-autocomplete-item-title">${highlightMatch(title, query)}</span>
              ${subtitle ? `<span class="tecnm-autocomplete-item-subtitle">${highlightMatch(subtitle, query)}</span>` : ''}
            </div>
          `;
        }).join('');

        showDropdown(itemsHtml);

        // Bind clicks on items
        dropdown.querySelectorAll('.tecnm-autocomplete-item').forEach(el => {
          el.addEventListener('click', () => {
            const idx = parseInt(el.getAttribute('data-index'), 10);
            if (currentResults[idx]) {
              setSelected(currentResults[idx], true);
            }
          });
        });
      } catch (err) {
        if (err.name === 'AbortError') return;
        container.classList.remove('is-loading');
        showDropdown('<div class="tecnm-autocomplete-empty">Error al consultar datos. Intente de nuevo.</div>');
      }
    }

    // Input events
    input.addEventListener('input', () => {
      const q = input.value.trim();
      clearTimeout(debounceTimer);

      if (q.length < minChars) {
        hideDropdown();
        return;
      }

      debounceTimer = setTimeout(() => {
        performSearch(q);
      }, debounceMs);
    });

    input.addEventListener('keydown', (e) => {
      const items = dropdown.querySelectorAll('.tecnm-autocomplete-item');
      if (!dropdown.classList.contains('active') || items.length === 0) return;

      if (e.key === 'ArrowDown') {
        e.preventDefault();
        focusedIndex = (focusedIndex + 1) % items.length;
        updateItemFocus(items);
      } else if (e.key === 'ArrowUp') {
        e.preventDefault();
        focusedIndex = (focusedIndex - 1 + items.length) % items.length;
        updateItemFocus(items);
      } else if (e.key === 'Enter') {
        e.preventDefault();
        if (focusedIndex >= 0 && currentResults[focusedIndex]) {
          setSelected(currentResults[focusedIndex], true);
        }
      } else if (e.key === 'Escape') {
        hideDropdown();
      }
    });

    function updateItemFocus(items) {
      items.forEach((item, idx) => {
        if (idx === focusedIndex) {
          item.classList.add('is-focused');
          item.scrollIntoView({ block: 'nearest' });
        } else {
          item.classList.remove('is-focused');
        }
      });
    }

    // Click outside to close dropdown
    document.addEventListener('click', (e) => {
      if (!container.contains(e.target)) {
        hideDropdown();
      }
    });

    // Clear button
    clearBtn.addEventListener('click', () => {
      clearSelection(true);
      input.focus();
    });

    // Modal Picker button (if configured)
    if (pickerBtn && globalSearchSource && window.openGlobalSearch) {
      pickerBtn.addEventListener('click', () => {
        window.openGlobalSearch({
          initialSource: globalSearchSource,
          onSelect: (item) => {
            if (item) {
              setSelected(item, true);
            }
          }
        });
      });
    }

    // Initial state
    if (initialItem) {
      setSelected(initialItem, false);
    }

    return {
      setValue: (item) => setSelected(item, false),
      clear: () => clearSelection(false),
      getValue: () => hiddenInput.value,
      getItem: () => selectedItem,
      focus: () => input.focus()
    };
  };
})();
