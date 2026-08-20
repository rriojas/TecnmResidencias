# 09 - Documents and File Storage Domain Specification (Backend C# .NET 10 + EF Core 10)

Module: `src/Documents/`  
Domain: File Storage, Expediente Digital & Institutional Document Release  
Tech Stack: **C# .NET 10** (ASP.NET Core Web API), **Entity Framework Core 10**, **PostgreSQL 18**

---

## 1. Colocated Files (Vertical Slice C#)

- `src/Documents/DocumentsController.cs`
- `src/Documents/DocumentService.cs`
- `src/Documents/IDocumentRepository.cs`
- `src/Documents/DocumentRepository.cs`
- `src/Documents/UploadDocumentDto.cs`
- `src/Documents/DocumentResponseDto.cs`
- `src/Documents/DocumentConfiguration.cs` (EF Core `IEntityTypeConfiguration<Document>`)
- `src/Documents/Document.cs`
- `src/Documents/DocumentType.cs`

---

## 2. API Endpoints

### 2.1 Upload Project Document / Evidencia
- **Endpoint**: `POST /api/v1/documents`
- **Content-Type**: `multipart/form-data`
- **Request Parameters**:
  - `projectId`: `long` (FK `projects.id`)
  - `documentType`: `string` (`solicitud`, `carta_presentacion`, `anteproyecto`, `dictamen`, `manual_usuario`, `manual_tecnico`, `libranza`)
  - `file`: `IFormFile` (PDF / Max 10MB)
- **Response (`DocumentResponseDto`)**: `201 Created`

### 2.2 List Documents by Project
- **Endpoint**: `GET /api/v1/documents/project/{projectId}`
- **Controller Action**: `[HttpGet("project/{projectId}")] public async Task<ActionResult<IEnumerable<DocumentResponseDto>>> GetByProject(long projectId)`

### 2.3 Download Document File
- **Endpoint**: `GET /api/v1/documents/{id}/download`
- **Response**: File Stream (`application/pdf`)

---

## 3. Business Rules & EF Core PostgreSQL 18 Mapping

1. **Document Types Enum**:
   - `solicitud`: Solicitud formal de residencia profesional.
   - `carta_presentacion`: Carta de presentación institucional.
   - `anteproyecto`: Propuesta y anteproyecto técnico.
   - `dictamen`: Dictamen de aprobación emitido por la academia/división.
   - `manual_usuario`: Manual de usuario final.
   - `manual_tecnico`: Manual técnico / arquitectónico.
   - `libranza`: Oficio de liberación de residencia.

2. **Database Schema (PostgreSQL 18 `documents` table)**:
   - `id` (`BIGSERIAL`, PK)
   - `project_id` (`BIGINT`, FK `projects.id`, NOT NULL)
   - `document_type` (`VARCHAR(50)`, NOT NULL)
   - `file_name` (`VARCHAR(255)`, NOT NULL)
   - `file_path` (`VARCHAR(500)`, NOT NULL)
   - `file_size` (`BIGINT`, NOT NULL)
   - `content_type` (`VARCHAR(100)`, DEFAULT 'application/pdf', NOT NULL)
   - Audit columns (`is_active`, `is_visible`, `display_order`, `created_by`, `updated_by`, `deleted_by`, `created_at`, `updated_at`, `deleted_at`).

3. **File Storage Location**: Uploaded files are stored OUTSIDE the web root (default `<ContentRoot>/uploads`, configurable via `Uploads:Path` / `Uploads__Path`). They are never served by static files; the only access path is `GET /api/v1/documents/{id}/download` (authenticated).
