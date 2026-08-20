# 01 - Authentication Domain Specification (Backend C# .NET 10 + EF Core 10)

Module: `src/Auth/`  
Domain: Authentication, Session Control & Credential Upgrades  
Tech Stack: **C# .NET 10** (ASP.NET Core Web API), **Entity Framework Core 10**, **PostgreSQL 18**

---

## 1. Colocated Files (Vertical Slice C#)

- `src/Auth/AuthController.cs`
- `src/Auth/AuthService.cs`
- `src/Auth/IAuthRepository.cs`
- `src/Auth/AuthRepository.cs`
- `src/Auth/LoginRequestDto.cs`
- `src/Auth/AuthTokenResponseDto.cs`
- `src/Auth/UserConfiguration.cs` (EF Core `IEntityTypeConfiguration<User>`)
- `src/Auth/User.cs`

---

## 2. API Endpoints

### 2.1 User Login
- **Endpoint**: `POST /api/v1/auth/login`
- **Controller Action**: `[HttpPost("login")] public async Task<IActionResult> Login([FromBody] LoginRequestDto dto)`
- **Request Body (`LoginRequestDto`)**:
  ```json
  {
    "email": "user@institution.edu.mx",
    "password": "UserPassword2026!"
  }
  ```
- **Response (`AuthTokenResponseDto`)**: `200 OK`
  ```json
  {
    "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
    "expiresIn": 86400,
    "user": {
      "id": 1,
      "email": "user@institution.edu.mx",
      "role": "student",
      "isActive": true
    }
  }
  ```

### 2.2 User Logout
- **Endpoint**: `POST /api/v1/auth/logout`
- **Header**: `Authorization: Bearer <token>`
- **Response**: `200 OK` (`{"message": "Sesión cerrada correctamente"}`)

---

## 3. Business Rules & EF Core PostgreSQL 18 Mapping

1. **Legacy Password Re-hashing**: If password hash matches legacy format `$legacy_sha256$`, evaluate legacy password. On successful match, transparently re-hash password using BCrypt / ASP.NET Core PasswordHasher and update `users` table via EF Core 10 `DbContext.SaveChangesAsync()`.
2. **Account Inactivity**: Inactive users (`is_active = FALSE`) cannot log in. Return `403 Forbidden` with message *"Cuenta desactivada. Contacte a la administración"*.
3. **Database Schema (PostgreSQL 18 `users` table)**:
   - `id` (`BIGSERIAL`, PK)
   - `email` (`VARCHAR(255)`, UNIQUE, NOT NULL)
   - `password_hash` (`VARCHAR(255)`, NOT NULL)
   - `role` (`VARCHAR(50)`, NOT NULL)
   - Audit columns (`is_active`, `is_visible`, `display_order`, `created_by`, `updated_by`, `deleted_by`, `created_at`, `updated_at`, `deleted_at`).
