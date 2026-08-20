/**
 * Módulo JavaScript de Búsqueda Global / Universal para TecNM Residency System v2
 * Soporta filtros avanzados, visibilidad dinámica de columnas, ordenamiento por TH y selección explicita.
 */

(function () {
    let searchSources = [];
    let activeSourceKey = '';
    let currentSelectCallback = null;
    let debounceTimer = null;
    let currentPage = 1;
    let pageSize = 10;

    // Estado interno
    let selectedRowData = null;
    let visibleColumnsMap = new Map(); // colName -> boolean
    let currentSortColumn = 'id';
    let currentSortDirection = 'ASC';
    let activeStatusFilter = 'active'; // 'active' | 'inactive'

    // Elementos DOM
    let modalEl, closeBtnEl, acceptBtnEl, titleEl, subtitleEl, countBadgeEl;
    let sourceSelectEl, columnSelectEl, matchSelectEl, sortDirSelectEl, textInputEl, executeSearchBtnEl;
    let columnsBtnEl, columnsMenuEl, columnsChecklistEl, visibleColCountBadgeEl;
    let statusActiveBtnEl, statusInactiveBtnEl;
    let tableHeadEl, tableBodyEl, prevPageBtnEl, nextPageBtnEl, paginationInfoEl;

    function init() {
        modalEl = document.getElementById('tecnmGlobalSearchModal');
        if (!modalEl) return;

        closeBtnEl = document.getElementById('closeGlobalSearchModalBtn');
        acceptBtnEl = document.getElementById('acceptGlobalSearchBtn');
        titleEl = document.getElementById('globalSearchModalTitle');
        subtitleEl = document.getElementById('globalSearchSubtitle');
        countBadgeEl = document.getElementById('globalSearchCountBadge');

        sourceSelectEl = document.getElementById('globalSearchSourceSelect');
        columnSelectEl = document.getElementById('globalSearchColumnSelect');
        matchSelectEl = document.getElementById('globalSearchMatchSelect');
        sortDirSelectEl = document.getElementById('globalSearchSortDirSelect');
        textInputEl = document.getElementById('globalSearchTextInput');
        executeSearchBtnEl = document.getElementById('executeSearchBtn');

        columnsBtnEl = document.getElementById('toggleColumnsDropdownBtn');
        columnsMenuEl = document.getElementById('globalSearchColumnsMenu');
        columnsChecklistEl = document.getElementById('globalSearchColumnsChecklist');
        visibleColCountBadgeEl = document.getElementById('globalSearchVisibleColCount');

        statusActiveBtnEl = document.getElementById('statusActiveBtn');
        statusInactiveBtnEl = document.getElementById('statusInactiveBtn');

        tableHeadEl = document.getElementById('globalSearchTableHead');
        tableBodyEl = document.getElementById('globalSearchTableBody');
        prevPageBtnEl = document.getElementById('prevPageBtn');
        nextPageBtnEl = document.getElementById('nextPageBtn');
        paginationInfoEl = document.getElementById('globalSearchPaginationInfo');

        bindEvents();
        loadSources();
    }

    function bindEvents() {
        // Atajo teclado Ctrl+K / Cmd+K
        document.addEventListener('keydown', (e) => {
            if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === 'k') {
                if (typeof window.getCurrentRole === 'function' && window.getCurrentRole() === 'student') return;
                e.preventDefault();
                window.openGlobalSearch();
            } else if (e.key === 'Escape' && isModalOpen()) {
                closeModal();
            }
        });

        closeBtnEl?.addEventListener('click', closeModal);
        modalEl?.addEventListener('click', (e) => {
            if (e.target === modalEl) closeModal();
        });

        // Botón Aceptar
        acceptBtnEl?.addEventListener('click', () => {
            if (selectedRowData && currentSelectCallback) {
                currentSelectCallback(selectedRowData);
                closeModal();
            }
        });

        // Dropdown Columnas
        columnsBtnEl?.addEventListener('click', (e) => {
            e.stopPropagation();
            const isOpen = columnsMenuEl.style.display !== 'none';
            columnsMenuEl.style.display = isOpen ? 'none' : 'block';
        });

        document.addEventListener('click', (e) => {
            if (columnsMenuEl && !columnsMenuEl.contains(e.target) && e.target !== columnsBtnEl) {
                columnsMenuEl.style.display = 'none';
            }
        });

        // Selección de Fuente
        sourceSelectEl?.addEventListener('change', () => {
            activeSourceKey = sourceSelectEl.value;
            resetColumnsVisibility();
            updateColumnOptions();
            currentPage = 1;
            executeSearch();
        });

        columnSelectEl?.addEventListener('change', () => {
            currentSortColumn = columnSelectEl.value;
            currentPage = 1;
            executeSearch();
        });

        matchSelectEl?.addEventListener('change', () => {
            currentPage = 1;
            executeSearch();
        });

        sortDirSelectEl?.addEventListener('change', () => {
            currentSortDirection = sortDirSelectEl.value;
            executeSearch();
        });

        executeSearchBtnEl?.addEventListener('click', () => {
            currentPage = 1;
            executeSearch();
        });

        // Debounce escritura
        textInputEl?.addEventListener('input', () => {
            clearTimeout(debounceTimer);
            debounceTimer = setTimeout(() => {
                currentPage = 1;
                executeSearch();
            }, 350);
        });

        // Status Pills
        statusActiveBtnEl?.addEventListener('click', () => {
            statusActiveBtnEl.classList.add('active');
            statusInactiveBtnEl.classList.remove('active');
            activeStatusFilter = 'active';
            currentPage = 1;
            executeSearch();
        });

        statusInactiveBtnEl?.addEventListener('click', () => {
            statusInactiveBtnEl.classList.add('active');
            statusActiveBtnEl.classList.remove('active');
            activeStatusFilter = 'inactive';
            currentPage = 1;
            executeSearch();
        });

        // Paginación
        prevPageBtnEl?.addEventListener('click', () => {
            if (currentPage > 1) {
                currentPage--;
                executeSearch();
            }
        });

        nextPageBtnEl?.addEventListener('click', () => {
            currentPage++;
            executeSearch();
        });
    }

    function isModalOpen() {
        return modalEl && modalEl.style.display !== 'none';
    }

    function openModal(options = {}) {
        if (!modalEl) return;
        currentSelectCallback = options.onSelect || null;
        selectedRowData = null;
        updateAcceptButtonState();

        modalEl.style.display = 'flex';
        modalEl.setAttribute('aria-hidden', 'false');

        if (options.initialSource && searchSources.some(s => s.key === options.initialSource)) {
            sourceSelectEl.value = options.initialSource;
            activeSourceKey = options.initialSource;
            if (sourceSelectEl) sourceSelectEl.disabled = true;
        } else {
            if (sourceSelectEl.options.length > 0 && !sourceSelectEl.value) {
                sourceSelectEl.selectedIndex = 0;
            }
            activeSourceKey = sourceSelectEl.value;
            if (sourceSelectEl) sourceSelectEl.disabled = false;
        }

        resetColumnsVisibility();
        updateColumnOptions();

        textInputEl.value = options.initialText || '';
        textInputEl.focus();
        currentPage = 1;
        executeSearch();
    }

    function closeModal() {
        if (!modalEl) return;
        modalEl.style.display = 'none';
        modalEl.setAttribute('aria-hidden', 'true');
        selectedRowData = null;
        currentSelectCallback = null;
        if (sourceSelectEl) sourceSelectEl.disabled = false;
        if (columnsMenuEl) columnsMenuEl.style.display = 'none';
    }

    async function loadSources() {
        try {
            const token = localStorage.getItem('jwt_token') || sessionStorage.getItem('jwt_token');
            const headers = { 'Content-Type': 'application/json' };
            if (token) headers['Authorization'] = `Bearer ${token}`;

            const response = await fetch('/api/v1/searches/sources', { headers });
            if (!response.ok) return;

            searchSources = await response.json();
            populateSourceSelect();
        } catch (err) {
            console.error('[GlobalSearch] Error cargando fuentes:', err);
        }
    }

    function populateSourceSelect() {
        if (!sourceSelectEl) return;
        sourceSelectEl.innerHTML = '';
        searchSources.forEach(src => {
            const opt = document.createElement('option');
            opt.value = src.key;
            opt.textContent = src.displayName;
            sourceSelectEl.appendChild(opt);
        });

        if (searchSources.length > 0 && !activeSourceKey) {
            activeSourceKey = searchSources[0].key;
            resetColumnsVisibility();
            updateColumnOptions();
        }
    }

    function resetColumnsVisibility() {
        visibleColumnsMap.clear();
        const source = searchSources.find(s => s.key === activeSourceKey);
        if (!source) return;

        source.columns.forEach(col => {
            visibleColumnsMap.set(col.name, true); // por defecto todas visibles
        });
    }

    function updateColumnOptions() {
        const source = searchSources.find(s => s.key === activeSourceKey);
        if (!source) return;

        // Actualizar Select de Columna de Filtro
        if (columnSelectEl) {
            columnSelectEl.innerHTML = '';
            source.columns.filter(c => c.isSearchable).forEach(col => {
                const opt = document.createElement('option');
                opt.value = col.name;
                opt.textContent = col.displayName;
                columnSelectEl.appendChild(opt);
            });
            currentSortColumn = columnSelectEl.value || source.keyColumn;
        }

        // Actualizar Subtítulo
        if (subtitleEl) {
            subtitleEl.textContent = `${source.displayName} (${source.columns.length} columnas)`;
        }

        // Actualizar Checklist de Columnas (Dropdown)
        renderColumnsChecklist(source);
    }

    function renderColumnsChecklist(source) {
        if (!columnsChecklistEl) return;
        columnsChecklistEl.innerHTML = '';

        let visibleCount = 0;

        source.columns.forEach(col => {
            const isVisible = visibleColumnsMap.get(col.name) !== false;
            if (isVisible) visibleCount++;

            const label = document.createElement('label');
            label.className = 'tecnm-search-checkbox-label';

            const chk = document.createElement('input');
            chk.type = 'checkbox';
            chk.checked = isVisible;
            chk.addEventListener('change', () => {
                visibleColumnsMap.set(col.name, chk.checked);
                updateTableColumnsVisibility();
                updateVisibleColumnsBadge(source.columns.length);
            });

            const span = document.createElement('span');
            span.textContent = col.displayName;

            label.appendChild(chk);
            label.appendChild(span);
            columnsChecklistEl.appendChild(label);
        });

        updateVisibleColumnsBadge(source.columns.length);
    }

    function updateVisibleColumnsBadge(total) {
        let count = 0;
        visibleColumnsMap.forEach((v) => { if (v) count++; });
        if (visibleColCountBadgeEl) {
            visibleColCountBadgeEl.textContent = count;
        }
    }

    function updateTableColumnsVisibility() {
        const source = searchSources.find(s => s.key === activeSourceKey);
        if (!source) return;

        source.columns.forEach((col, index) => {
            const isVisible = visibleColumnsMap.get(col.name) !== false;
            // Alternar th
            const th = tableHeadEl.querySelectorAll('th')[index];
            if (th) th.style.display = isVisible ? '' : 'none';

            // Alternar td en cada fila
            const rows = tableBodyEl.querySelectorAll('tr');
            rows.forEach(tr => {
                const td = tr.querySelectorAll('td')[index];
                if (td) td.style.display = isVisible ? '' : 'none';
            });
        });
    }

    async function executeSearch() {
        if (!activeSourceKey) return;

        selectedRowData = null;
        updateAcceptButtonState();

        const token = localStorage.getItem('jwt_token') || sessionStorage.getItem('jwt_token');
        const headers = { 'Content-Type': 'application/json' };
        if (token) headers['Authorization'] = `Bearer ${token}`;

        const payload = {
            sourceKey: activeSourceKey,
            searchColumn: columnSelectEl.value || '',
            searchText: textInputEl.value.trim(),
            matchOption: matchSelectEl.value || 'Contains',
            pageNumber: currentPage,
            pageSize: pageSize,
            sortColumn: currentSortColumn,
            sortDirection: currentSortDirection,
            statusFilter: activeStatusFilter
        };

        tableBodyEl.innerHTML = `<tr><td colspan="15" class="tecnm-search-empty">Cargando resultados...</td></tr>`;

        try {
            const res = await fetch('/api/v1/searches/filter-paged', {
                method: 'POST',
                headers,
                body: JSON.stringify(payload)
            });

            if (!res.ok) {
                tableBodyEl.innerHTML = `<tr><td colspan="15" class="tecnm-search-empty">Error al realizar la búsqueda.</td></tr>`;
                return;
            }

            const data = await res.json();
            renderResults(data);
        } catch (err) {
            console.error('[GlobalSearch] Error ejecutando búsqueda:', err);
            tableBodyEl.innerHTML = `<tr><td colspan="15" class="tecnm-search-empty">Error de conexión con el servidor.</td></tr>`;
        }
    }

    function renderResults(data) {
        const source = data.source;
        let rows = data.rows || [];
        const meta = data.pagination || {};

        // Actualizar badges
        if (countBadgeEl) countBadgeEl.textContent = meta.totalCount || 0;

        // Render Head
        tableHeadEl.innerHTML = '';
        const trHead = document.createElement('tr');
        source.columns.forEach(col => {
            const th = document.createElement('th');
            th.className = 'tecnm-search-th-sortable';
            const isVisible = visibleColumnsMap.get(col.name) !== false;
            if (!isVisible) th.style.display = 'none';

            let sortIcon = '';
            if (col.name.toLowerCase() === currentSortColumn.toLowerCase()) {
                sortIcon = currentSortDirection === 'ASC' ? ' ↑' : ' ↓';
                th.classList.add('sorted');
            }

            th.innerHTML = `<span>${col.displayName}${sortIcon}</span>`;
            th.addEventListener('click', () => {
                if (currentSortColumn.toLowerCase() === col.name.toLowerCase()) {
                    currentSortDirection = currentSortDirection === 'ASC' ? 'DESC' : 'ASC';
                } else {
                    currentSortColumn = col.name;
                    currentSortDirection = 'ASC';
                }
                if (sortDirSelectEl) sortDirSelectEl.value = currentSortDirection;
                executeSearch();
            });

            trHead.appendChild(th);
        });
        tableHeadEl.appendChild(trHead);

        // Render Body
        tableBodyEl.innerHTML = '';
        if (rows.length === 0) {
            tableBodyEl.innerHTML = `<tr><td colspan="${source.columns.length}" class="tecnm-search-empty">No se encontraron registros.</td></tr>`;
        } else {
            rows.forEach(row => {
                const tr = document.createElement('tr');
                tr.className = 'tecnm-search-row';

                source.columns.forEach(col => {
                    const td = document.createElement('td');
                    const isVisible = visibleColumnsMap.get(col.name) !== false;
                    if (!isVisible) td.style.display = 'none';

                    const val = row[col.name] ?? row[col.name.toLowerCase()];
                    td.textContent = val !== null && val !== undefined ? val : '-';
                    tr.appendChild(td);
                });

                // Clic simple para seleccionar
                tr.addEventListener('click', () => {
                    tableBodyEl.querySelectorAll('tr').forEach(r => r.classList.remove('selected'));
                    tr.classList.add('selected');
                    selectedRowData = row;
                    updateAcceptButtonState();
                });

                // Doble clic para confirmar de inmediato
                tr.addEventListener('dblclick', () => {
                    selectedRowData = row;
                    if (currentSelectCallback) {
                        currentSelectCallback(row);
                    }
                    closeModal();
                });

                tableBodyEl.appendChild(tr);
            });
        }

        // Paginación footer
        const totalPages = meta.totalPages || 1;
        const totalCount = meta.totalCount || 0;
        const startRow = totalCount === 0 ? 0 : (currentPage - 1) * pageSize + 1;
        const endRow = Math.min(currentPage * pageSize, totalCount);

        if (paginationInfoEl) {
            paginationInfoEl.textContent = `Página ${currentPage} de ${totalPages} • Mostrando ${startRow}-${endRow} de ${totalCount} registros`;
        }

        if (prevPageBtnEl) prevPageBtnEl.disabled = !meta.hasPreviousPage;
        if (nextPageBtnEl) nextPageBtnEl.disabled = !meta.hasNextPage;
    }

    function updateAcceptButtonState() {
        if (!acceptBtnEl) return;
        acceptBtnEl.disabled = !selectedRowData;
    }

    // Exponer API global
    window.openGlobalSearch = function (options = {}) {
        if (typeof window.getCurrentRole === 'function' && window.getCurrentRole() === 'student') return;
        openModal(options);
    };

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
})();
