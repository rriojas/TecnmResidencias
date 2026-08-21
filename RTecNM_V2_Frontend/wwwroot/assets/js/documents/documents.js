let currentProjectId = 1;
let currentDocIdForStatus = null;
let documentsPageNumber = 1;
let documentsSearch = '';
let documentsSortBy = 'UploadedAt';
let documentsSortDir = 'desc';
let documentsIncludeInactive = false;
let documentsCache = [];

window.openDocumentAuditModal = (id) => {
    const d = documentsCache.find(item => item.id === id);
    if (!d || !window.showAuditModal) return;

    window.showAuditModal(`Auditoría — Documento #${d.id}`, [
        { label: 'ID', value: d.id },
        { label: 'Archivo', value: d.fileName },
        { label: 'Estado', value: d.isActive ? 'Activo' : 'Inactivo' },
        { label: 'Visible', value: d.isVisible ? 'Sí' : 'No' },
        { label: 'Orden', value: d.displayOrder },
        { label: 'Creado el', value: window.formatAuditDate(d.createdAt || d.uploadedAt) },
        { label: 'Creado por', value: window.formatAuditUser(d.createdBy) },
        { label: 'Actualizado el', value: d.updatedBy ? window.formatAuditDate(d.updatedAt) : '—' },
        { label: 'Actualizado por', value: d.updatedBy ? window.formatAuditUser(d.updatedBy) : '—' },
        { label: 'Eliminado el', value: d.deletedAt ? window.formatAuditDate(d.deletedAt) : '—' },
        { label: 'Eliminado por', value: d.deletedBy ? window.formatAuditUser(d.deletedBy) : '—' }
    ]);
};

const documentTypeLabels = {
    'solicitud': 'Solicitud de Residencia',
    'carta_presentacion': 'Carta de Presentación',
    'carta_aceptacion': 'Carta de Aceptación',
    'anteproyecto': 'Anteproyecto Técnico',
    'dictamen': 'Dictamen de Aprobación',
    'manual_usuario': 'Manual de Usuario',
    'manual_tecnico': 'Manual Técnico',
    'libranza': 'Oficio de Liberación',
    'otro': 'Otro / Evidencia'
};

const statusBadgeClasses = {
    'uploaded': 'tecnm-badge-pending',
    'under_review': 'tecnm-badge-pending',
    'approved': 'tecnm-badge-approved',
    'rejected': 'tecnm-badge-rejected'
};

const statusLabels = {
    'uploaded': 'Cargado',
    'under_review': 'En Revisión',
    'approved': 'Aprobado',
    'rejected': 'Rechazado'
};

let uploadProjectAutocomplete = null;

async function selectProjectForDocuments(project) {
    if (!project || !project.id) return;
    currentProjectId = project.id;

    const title = project.title || project.titleText || project.name || 'Anteproyecto';
    const studentName = project.studentName || project.student_name || project.student || '';
    const controlNumber = project.studentControlNumber || project.student_control_number || project.controlNumber || '';
    const studentText = studentName ? ` (Alumno: ${escapeHtml(studentName)}${controlNumber ? ' - ' + escapeHtml(controlNumber) : ''})` : '';

    const badge = document.getElementById('selectedProjectBadge');
    if (badge) {
        badge.innerHTML = `<strong>${escapeHtml(title)}</strong>${studentText}`;
    }

    const uploadProjectId = document.getElementById('uploadProjectId');
    if (uploadProjectId) {
        uploadProjectId.value = currentProjectId;
    }

    if (uploadProjectAutocomplete && project) {
        uploadProjectAutocomplete.setValue({
            id: project.id,
            title: title,
            studentName: studentName
        });
    }

    documentsPageNumber = 1;
    loadDocuments(currentProjectId);

    if (!studentName || !controlNumber) {
        try {
            const res = await fetch(`/api/v1/projects/${project.id}`, { headers: getAuthHeaders() });
            if (res.ok) {
                const fullProj = await res.json();
                const fStudent = fullProj.studentName || fullProj.student_name || '';
                const fCtrl = fullProj.studentControlNumber || fullProj.student_control_number || '';
                if (fStudent) {
                    const enrichedText = ` (Alumno: ${escapeHtml(fStudent)}${fCtrl ? ' - ' + escapeHtml(fCtrl) : ''})`;
                    if (badge) badge.innerHTML = `<strong>${escapeHtml(fullProj.title || title)}</strong>${enrichedText}`;
                }
            }
        } catch (_) {}
    }
}

window.selectProjectForDocuments = selectProjectForDocuments;

function initDocumentsPage() {
    setupEventListeners();

    if (window.initTecNMAutocomplete && document.getElementById('uploadProjectAutocompleteWrapper')) {
        uploadProjectAutocomplete = window.initTecNMAutocomplete({
            containerId: 'uploadProjectAutocompleteWrapper',
            hiddenInputId: 'uploadProjectId',
            placeholder: 'Buscar anteproyecto por título o estudiante...',
            endpoint: '/api/v1/projects',
            globalSearchSource: 'PROJECTS',
            titleExtractor: (p) => p.title || `Proyecto #${p.id}`,
            subtitleExtractor: (p) => p.studentName ? `Alumno: ${p.studentName}${p.studentControlNumber ? ' • ' + p.studentControlNumber : ''}` : ''
        });
    }

    const isStudent = window.hasRole && window.hasRole('student') && !window.hasRole('admin', 'departmenthead', 'advisor');
    const isAdvisor = window.hasRole && window.hasRole('advisor') && !window.hasRole('admin', 'departmenthead');

    const searchBtn = document.getElementById('searchProjectBtn');

    if (isStudent) {
        if (searchBtn) searchBtn.style.display = 'none';
        resolveCurrentStudentProject();
    } else {
        if (searchBtn) {
            searchBtn.style.display = 'inline-flex';
            searchBtn.addEventListener('click', () => {
                if (window.openGlobalSearch) {
                    window.openGlobalSearch({
                        initialSource: 'PROJECTS',
                        onSelect: (item) => {
                            if (item && item.id) {
                                selectProjectForDocuments(item);
                            }
                        }
                    });
                }
            });
        }
        loadInitialProjectForDocuments(isAdvisor);
    }
}

function getAuthHeaders() {
    const headers = { 'Content-Type': 'application/json' };
    const token = sessionStorage.getItem('authToken');
    if (token) headers['Authorization'] = `Bearer ${token}`;
    return headers;
}

async function resolveCurrentStudentProject() {
    try {
        const res = await fetch('/api/v1/projects/me/current', { headers: getAuthHeaders() });
        if (!res.ok) throw new Error();
        const project = await res.json();
        if (project && project.id) {
            populateUploadModalOptions([project]);
            selectProjectForDocuments(project);
        } else {
            loadDocuments(null);
        }
    } catch {
        loadDocuments(null);
    }
}

async function loadInitialProjectForDocuments(isAdvisor) {
    const badge = document.getElementById('selectedProjectBadge');
    const tbody = document.getElementById('documentsTableBody');

    try {
        const endpoint = isAdvisor ? '/api/v1/projects/advisor/me?pageSize=50' : '/api/v1/projects?pageSize=50';
        const res = await fetch(endpoint, { headers: getAuthHeaders() });
        if (!res.ok) throw new Error();

        const rawData = await res.json();
        let projects = Array.isArray(rawData) ? rawData : (rawData && Array.isArray(rawData.items) ? rawData.items : []);
        projects = projects.filter(p => (p.status || '').toLowerCase() !== 'draft');

        if (projects.length === 0) {
            if (badge) badge.innerText = 'Sin anteproyectos asignados';
            if (tbody) tbody.innerHTML = `<tr><td colspan="6" class="tecnm-table-empty">No se encontraron anteproyectos asignados. Utilice el botón "Buscar Anteproyecto" para seleccionar uno.</td></tr>`;
            currentProjectId = null;
            return;
        }

        populateUploadModalOptions(projects);
        selectProjectForDocuments(projects[0]);
    } catch {
        if (badge) badge.innerText = 'Seleccione un anteproyecto';
        if (tbody) tbody.innerHTML = `<tr><td colspan="6" class="tecnm-table-empty">Haga clic en "Buscar Anteproyecto" para cargar documentos.</td></tr>`;
        currentProjectId = null;
    }
}

function populateUploadModalOptions(projects) {
    const uploadProjectId = document.getElementById('uploadProjectId');
    if (!uploadProjectId) return;
    uploadProjectId.innerHTML = projects.map(p => `<option value="${p.id}">#${p.id} - ${escapeHtml(p.title)}</option>`).join('');
}

function setupEventListeners() {

    if (window.bindTableSearch) {
        window.bindTableSearch('documentsSearchInput', (term) => {
            documentsSearch = term;
            documentsPageNumber = 1;
            loadDocuments(currentProjectId);
        });
    }

    if (window.initSortableHeaders) {
        window.initSortableHeaders('documentsTable', (field, dir) => {
            documentsSortBy = field;
            documentsSortDir = dir;
            documentsPageNumber = 1;
            loadDocuments(currentProjectId);
        });
    }

    const inactiveToggle = document.getElementById('documentsIncludeInactiveToggle');
    if (inactiveToggle) {
        inactiveToggle.addEventListener('change', () => {
            documentsIncludeInactive = inactiveToggle.checked;
            documentsPageNumber = 1;
            loadDocuments(currentProjectId);
        });
    }

    const openUploadModalBtn = document.getElementById('openUploadModalBtn');
    const uploadModal = document.getElementById('uploadModal');
    const closeUploadModalBtn = document.getElementById('closeUploadModalBtn');
    const cancelUploadBtn = document.getElementById('cancelUploadBtn');

    if (openUploadModalBtn && uploadModal) {
        if (window.isReadOnlyUser && window.isReadOnlyUser()) {
            openUploadModalBtn.classList.add('tecnm-hidden');
        } else {
            openUploadModalBtn.addEventListener('click', () => {
                uploadModal.classList.add('active');
            });
        }
    }

    const hideUploadModal = () => {
        if (uploadModal) uploadModal.classList.remove('active');
        const form = document.getElementById('uploadDocumentForm');
        if (form) form.reset();
        clearUploadPreview();
    };

    if (closeUploadModalBtn) closeUploadModalBtn.addEventListener('click', hideUploadModal);
    if (cancelUploadBtn) cancelUploadBtn.addEventListener('click', hideUploadModal);

    const uploadForm = document.getElementById('uploadDocumentForm');
    if (uploadForm) {
        uploadForm.addEventListener('submit', handleUploadSubmit);
    }

    const fileInput = document.getElementById('documentFile');
    if (fileInput) {
        fileInput.addEventListener('change', (e) => {
            const file = e.target.files[0];
            if (!file) return;

            const allowedExtensions = ['.pdf', '.jpg', '.jpeg', '.png'];
            const ext = '.' + (file.name.split('.').pop() || '').toLowerCase();
            if (!allowedExtensions.includes(ext)) {
                showAlert('Solo se permiten archivos en formato PDF, JPG o PNG.', 'danger');
                e.target.value = '';
                clearUploadPreview();
                return;
            }

            if (file.size > 10 * 1024 * 1024) {
                showAlert('El archivo seleccionado excede el límite máximo de 10MB.', 'danger');
                e.target.value = '';
                clearUploadPreview();
                return;
            }

            renderLocalPreview(file);
        });
    }

    const statusModal = document.getElementById('statusModal');
    const closeStatusModalBtn = document.getElementById('closeStatusModalBtn');
    const cancelStatusBtn = document.getElementById('cancelStatusBtn');
    const saveStatusBtn = document.getElementById('saveStatusBtn');

    const hideStatusModal = () => {
        if (statusModal) statusModal.classList.remove('active');
        currentDocIdForStatus = null;
        const container = document.getElementById('statusPreviewContainer');
        if (container) container.innerHTML = '';
    };

    if (closeStatusModalBtn) closeStatusModalBtn.addEventListener('click', hideStatusModal);
    if (cancelStatusBtn) cancelStatusBtn.addEventListener('click', hideStatusModal);
    if (saveStatusBtn) saveStatusBtn.addEventListener('click', handleSaveStatus);

    const previewModal = document.getElementById('previewModal');
    const closePreviewModalBtn = document.getElementById('closePreviewModalBtn');
    if (closePreviewModalBtn && previewModal) {
        closePreviewModalBtn.addEventListener('click', () => {
            previewModal.classList.remove('active');
            const container = document.getElementById('previewContainer');
            if (container) container.innerHTML = '';
        });
    }
}

let uploadPreviewUrl = null;

function renderLocalPreview(file) {
    const container = document.getElementById('uploadPreviewContainer');
    if (!container) return;

    if (uploadPreviewUrl) {
        URL.revokeObjectURL(uploadPreviewUrl);
        uploadPreviewUrl = null;
    }

    uploadPreviewUrl = URL.createObjectURL(file);

    if (file.type.startsWith('image/')) {
        container.innerHTML = `<img src="${uploadPreviewUrl}" alt="Vista previa del archivo seleccionado" />`;
    } else {
        container.innerHTML = `<embed src="${uploadPreviewUrl}" type="application/pdf" />`;
    }

    container.classList.remove('tecnm-hidden');
}

function clearUploadPreview() {
    const container = document.getElementById('uploadPreviewContainer');
    if (container) {
        container.innerHTML = '';
        container.classList.add('tecnm-hidden');
    }
    if (uploadPreviewUrl) {
        URL.revokeObjectURL(uploadPreviewUrl);
        uploadPreviewUrl = null;
    }
}

async function renderDocPreview(containerId, docId) {
    const container = document.getElementById(containerId);
    if (!container) return;

    container.innerHTML = `<p class="tecnm-table-empty">Cargando vista previa...</p>`;

    try {
        const response = await fetch(`/api/v1/documents/${docId}/download`);
        if (!response.ok) throw new Error('No se pudo cargar el documento.');

        const blob = await response.blob();
        const contentType = response.headers.get('Content-Type') || blob.type || '';
        const objectUrl = URL.createObjectURL(blob);

        if (contentType.startsWith('image/')) {
            container.innerHTML = `<img src="${objectUrl}" alt="Vista previa del documento" />`;
        } else {
            container.innerHTML = `<embed src="${objectUrl}" type="application/pdf" />`;
        }
    } catch (err) {
        container.innerHTML = `<p class="tecnm-table-empty tecnm-text-danger">${escapeHtml(err.message || 'Error al cargar la vista previa.')}</p>`;
    }
}

async function loadDocuments(projectId) {
    const tableBody = document.getElementById('documentsTableBody');
    const paginationContainer = document.getElementById('documentsPagination');
    if (!tableBody) return;

    if (!projectId) {
        tableBody.innerHTML = `<tr><td colspan="6" class="tecnm-table-empty">No hay proyectos registrados en la base de datos.</td></tr>`;
        if (paginationContainer) paginationContainer.innerHTML = '';
        return;
    }

    tableBody.innerHTML = `<tr><td colspan="6" class="tecnm-table-empty">Cargando documentos...</td></tr>`;

    try {
        const params = new URLSearchParams({
            pageNumber: documentsPageNumber,
            pageSize: 10,
            search: documentsSearch,
            sortBy: documentsSortBy,
            sortDir: documentsSortDir,
            includeInactive: documentsIncludeInactive
        });
        const response = await fetch(`/api/v1/documents/project/${projectId}?${params}`);
        if (!response.ok) {
            throw new Error(`Error al obtener documentos: ${response.statusText}`);
        }

        const data = await response.json();
        const documents = (data && data.items) || [];
        documentsCache = documents;

        if (window.canSeeAudit && window.canSeeAudit()) {
            await window.loadAuditUserNames(window.collectAuditUserIds(documents));
        }

        if (documents.length === 0 && documentsPageNumber > 1 && data.totalPages > 0) {
            documentsPageNumber = data.totalPages;
            return loadDocuments(projectId);
        }

        if (documents.length === 0) {
            tableBody.innerHTML = `<tr><td colspan="6" class="tecnm-table-empty">No hay documentos registrados para este proyecto.</td></tr>`;
        } else {
            tableBody.innerHTML = documents.map(doc => {
                const typeLabel = documentTypeLabels[doc.documentType] || doc.documentType;
                const badgeClass = statusBadgeClasses[doc.status] || 'tecnm-badge-pending';
                const statusText = statusLabels[doc.status] || doc.status;
                const formattedSize = formatFileSize(doc.fileSize);
                const formattedDate = window.formatTecNMDate(doc.uploadedAt);
                const canEvaluate = window.canGrade ? window.canGrade() : true;
                const canDelete = window.canManageRegistry ? window.canManageRegistry() : true;
                const canSeeAudit = window.canSeeAudit ? window.canSeeAudit() : false;

                return `
                    <tr>
                        <td><strong>${escapeHtml(typeLabel)}</strong></td>
                        <td>${escapeHtml(doc.fileName)}</td>
                        <td>${formattedSize}</td>
                        <td>${formattedDate}</td>
                        <td><span class="tecnm-badge ${badgeClass}">${escapeHtml(statusText)}</span></td>
                        <td>
                            <div class="tecnm-row-actions">
                                <button type="button" class="tecnm-btn tecnm-btn-secondary tecnm-btn-sm" onclick="previewDocument(${doc.id}, '${escapeHtml(doc.fileName)}')">Vista Previa</button>
                                <a href="/api/v1/documents/${doc.id}/download" download="${escapeHtml(doc.fileName)}" target="_blank" class="tecnm-btn tecnm-btn-secondary tecnm-btn-sm" title="Descargar">⬇ Descargar</a>
                                ${canEvaluate ? `<button type="button" class="tecnm-btn tecnm-btn-secondary tecnm-btn-sm" onclick="openStatusModal(${doc.id}, '${escapeHtml(typeLabel)}', '${escapeHtml(doc.fileName)}', '${doc.status}', '${escapeHtml(doc.rejectionReason || '')}')">Evaluar</button>` : ''}
                                ${canSeeAudit ? `<button type="button" class="tecnm-btn tecnm-btn-secondary tecnm-btn-sm" onclick="openDocumentAuditModal(${doc.id})">Auditoría</button>` : ''}
                                ${canDelete ? `<button type="button" class="tecnm-btn tecnm-btn-danger tecnm-btn-sm" onclick="deleteDocument(${doc.id})">Eliminar</button>` : ''}
                            </div>
                        </td>
                    </tr>
                `;
            }).join('');
        }

        if (window.renderPagination) {
            window.renderPagination(paginationContainer, data, (page) => {
                documentsPageNumber = page;
                loadDocuments(projectId);
            });
        }
    } catch (err) {
        tableBody.innerHTML = `<tr><td colspan="6" class="tecnm-table-empty tecnm-text-danger">Error: ${escapeHtml(err.message)}</td></tr>`;
        if (paginationContainer) paginationContainer.innerHTML = '';
    }
}

window.previewDocument = async function(id, fileName) {
    const modal = document.getElementById('previewModal');
    const nameEl = document.getElementById('previewDocName');
    if (!modal) return;

    if (nameEl) nameEl.textContent = fileName || `Documento #${id}`;
    modal.classList.add('active');

    await renderDocPreview('previewContainer', id);
};

async function handleUploadSubmit(e) {
    e.preventDefault();
    const projectId = document.getElementById('uploadProjectId').value;
    const documentType = document.getElementById('uploadDocumentType').value;
    const fileInput = document.getElementById('documentFile');

    if (!fileInput.files || fileInput.files.length === 0) {
        showAlert('Seleccione un archivo PDF o imagen válido.', 'danger');
        return;
    }

    const file = fileInput.files[0];
    if (file.size > 10 * 1024 * 1024) {
        showAlert('El archivo excede el tamaño máximo permitido de 10MB.', 'danger');
        return;
    }

    const allowedExtensions = ['.pdf', '.jpg', '.jpeg', '.png'];
    const ext = '.' + (file.name.split('.').pop() || '').toLowerCase();
    if (!allowedExtensions.includes(ext)) {
        showAlert('Solo se permiten archivos en formato PDF, JPG o PNG.', 'danger');
        return;
    }

    const formData = new FormData();
    formData.append('projectId', projectId);
    formData.append('documentType', documentType);
    formData.append('file', file);

    const submitBtn = document.getElementById('submitUploadBtn');
    submitBtn.disabled = true;
    submitBtn.textContent = 'Subiendo...';

    try {
        const response = await fetch('/api/v1/documents', {
            method: 'POST',
            body: formData
        });

        if (!response.ok) {
            const errData = await response.json();
            throw new Error(errData.message || 'Error al subir documento');
        }

        showAlert('¡Documento subido correctamente al expediente!', 'success');
        document.getElementById('uploadModal').classList.remove('active');
        document.getElementById('uploadDocumentForm').reset();
        loadDocuments(currentProjectId);
    } catch (err) {
        showAlert(err.message, 'danger');
    } finally {
        submitBtn.disabled = false;
        submitBtn.textContent = 'Subir Documento';
    }
}

window.openStatusModal = function(id, typeLabel, fileName, currentStatus, currentReason) {
    currentDocIdForStatus = id;
    document.getElementById('statusDocId').textContent = id;
    document.getElementById('statusDocType').textContent = typeLabel;
    document.getElementById('statusDocName').textContent = fileName;
    document.getElementById('statusSelect').value = currentStatus;
    document.getElementById('rejectionReasonInput').value = currentReason || '';
    document.getElementById('statusModal').classList.add('active');
    renderDocPreview('statusPreviewContainer', id);
};

async function handleSaveStatus() {
    if (!currentDocIdForStatus) return;

    const status = document.getElementById('statusSelect').value;
    const rejectionReason = document.getElementById('rejectionReasonInput').value;

    try {
        const response = await fetch(`/api/v1/documents/${currentDocIdForStatus}/status`, {
            method: 'PATCH',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ status, rejectionReason })
        });

        if (!response.ok) {
            const errData = await response.json();
            throw new Error(errData.message || 'Error al actualizar estado del documento.');
        }

        showAlert('Estado del documento actualizado correctamente.', 'success');
        document.getElementById('statusModal').classList.remove('active');
        loadDocuments(currentProjectId);
    } catch (err) {
        showAlert(err.message, 'danger');
    }
}

window.deleteDocument = async function(id) {
    const confirmed = await window.tecnmConfirm('¿Está seguro de eliminar este documento del expediente?', 'Eliminar Documento');
    if (!confirmed) return;

    try {
        const response = await fetch(`/api/v1/documents/${id}`, {
            method: 'DELETE'
        });

        if (!response.ok) {
            throw new Error('Error al eliminar el documento.');
        }

        showAlert('Documento eliminado del expediente.', 'warning');
        loadDocuments(currentProjectId);
    } catch (err) {
        showAlert(err.message, 'danger');
    }
};

function formatFileSize(bytes) {
    if (bytes === 0) return '0 Bytes';
    const k = 1024;
    const sizes = ['Bytes', 'KB', 'MB', 'GB'];
    const i = Math.floor(Math.log(bytes) / Math.log(k));
    return parseFloat((bytes / Math.pow(k, i)).toFixed(2)) + ' ' + sizes[i];
}

function escapeHtml(text) {
    if (!text) return '';
    return text.toString()
        .replace(/&/g, "&amp;")
        .replace(/</g, "&lt;")
        .replace(/>/g, "&gt;")
        .replace(/"/g, "&quot;")
        .replace(/'/g, "&#039;");
}
