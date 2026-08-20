# 07 - Administration and Reports Domain Specification (Backend C# .NET 10 + EF Core 10)

Module: `src/Admin/`  
Domain: System Dashboard Metrics, Statistics & Official Document Release  
Tech Stack: **C# .NET 10** (ASP.NET Core Web API), **Entity Framework Core 10**, **PostgreSQL 18**

---

## 1. Colocated Files (Vertical Slice C#)

- `src/Admin/AdminController.cs`
- `src/Admin/DashboardMetricsService.cs`
- `src/Admin/ReportGeneratorService.cs`
- `src/Admin/DashboardMetricsResponseDto.cs`

---

## 2. API Endpoints

### 2.1 Get Dashboard Statistics
- **Endpoint**: `GET /api/v1/admin/dashboard`
- **Controller Action**: `[HttpGet("dashboard")] public async Task<ActionResult<DashboardMetricsResponseDto>> GetDashboard()`

### 2.2 Issue Official Release Letter (Libranza)
- **Endpoint**: `POST /api/v1/admin/reports/release-letter/{projectId}`
- **Response**: PDF Binary Stream or Signed Document Metadata (`DocumentDto`).

---

## 3. Business Rules & EF Core PostgreSQL 18 Queries

1. **Dashboard Metrics Query**: Calculated efficiently using LINQ with EF Core 10 async queries (`CountAsync`, `GroupBy`) on PostgreSQL 18.
2. **Release Criteria**: A residency release letter (`Libranza`) can only be generated if all partial evaluations are approved and final grade >= 70.
