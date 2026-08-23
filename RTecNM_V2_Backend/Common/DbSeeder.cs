using Microsoft.EntityFrameworkCore;
using TecNM.Residency.Activities;
using TecNM.Residency.Advisors;
using TecNM.Residency.Auth;
using TecNM.Residency.Companies;
using TecNM.Residency.Projects;
using TecNM.Residency.Students;

namespace TecNM.Residency.Common;

public static class DbSeeder
{
    // Bootstrap del sistema: catálogos RBAC imprescindibles (módulos, permisos,
    // roles y sus asignaciones) más el usuario administrador inicial.
    // Se ejecuta SIEMPRE, incluida producción.
    //   admin@monclova.tecnm.mx / contraseña configurable vía Seed:AdminPassword
    public static async Task BootstrapSystemAsync(AppDbContext db, string adminPassword)
    {
        // 0. Compatibilidad de esquema de columnas existentes
        await EnsureSchemaCompatibilityAsync(db);

        // 1. Sembrado Idempotente de Módulos
        var existingModules = await db.Modules.ToListAsync();
        var moduleDefs = new List<(string Name, string Slug)>
        {
            ("Estudiantes", "students"),
            ("Anteproyectos", "projects"),
            ("Cronograma y Actividades", "activities"),
            ("Bitácora de Asesorías", "advisories"),
            ("Evaluaciones", "evaluations"),
            ("Expediente Documental", "documents"),
            ("Empresas e Instituciones", "companies"),
            ("Reportes y Métricas", "reports"),
            ("Administración del Sistema", "admin")
        };

        foreach (var def in moduleDefs)
        {
            if (!existingModules.Any(m => m.Slug == def.Slug))
            {
                db.Modules.Add(new Module { Name = def.Name, Slug = def.Slug, IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
            }
        }
        await db.SaveChangesAsync();
        existingModules = await db.Modules.ToListAsync();

        // 2. Sembrado Idempotente de Permisos
        var existingPerms = await db.Permissions.ToListAsync();
        var permDefs = new List<(string ModuleSlug, string Name, string Slug)>
        {
            // Estudiantes
            ("students", "Ver Perfil de Estudiante", "students.profile.view"),
            ("students", "Actualizar Perfil", "students.profile.update"),
            ("students", "Verificar Elegibilidad", "students.eligibility.verify"),
            ("students", "Gestión de Estudiantes", "students.manage"),
            ("students", "Gestión de Asesores", "advisors.manage"),

            // Anteproyectos
            ("projects", "Solicitud y Registro de Anteproyecto", "projects.proposals"),
            ("projects", "Crear Propuesta de Anteproyecto", "projects.proposal.create"),
            ("projects", "Editar Anteproyecto", "projects.proposal.update"),
            ("projects", "Dictamen de División", "projects.review"),
            ("projects", "Eliminación Lógica de Anteproyectos", "projects.delete"),
            ("projects", "Asignar Asesor Académico", "projects.advisor.assign"),
            ("projects", "Ver Mis Anteproyectos", "projects.my"),
            ("projects", "Ver Anteproyectos Asignados", "projects.advisor"),
            ("projects", "Gestión de Anteproyectos", "projects.manage"),

            // Actividades
            ("activities", "Cronograma de Actividades 26 Semanas", "activities.schedule"),
            ("activities", "Reportar Avance Semanal", "activities.progress.report"),
            ("activities", "Validar Avance Semanal", "activities.progress.validate"),

            // Asesorías
            ("advisories", "Bitácora de Asesorías", "evaluations.advisories"),
            ("advisories", "Registrar Sesión de Asesoría", "advisories.session.record"),
            ("advisories", "Ver Bitácora de Asesorías", "advisories.session.view"),
            ("advisories", "Subir Evidencia de Asesoría", "advisories.evidence.upload"),

            // Evaluaciones
            ("evaluations", "Evaluaciones y Calificaciones", "evaluations.grading"),
            ("evaluations", "Evaluar Parciales 1 y 2", "evaluations.grade.partial"),
            ("evaluations", "Evaluar Reporte Final", "evaluations.grade.final"),
            ("evaluations", "Ver Resumen de Evaluaciones", "evaluations.summary.view"),

            // Documentos
            ("documents", "Expediente Digital", "documents.digital"),
            ("documents", "Subir Documentos de Expediente", "documents.upload"),
            ("documents", "Verificar Expediente", "documents.verify"),
            ("documents", "Generar Cartas Presentación/Liberación", "documents.letters.generate"),
            ("documents", "Ver Mis Documentos", "documents.my"),

            // Empresas
            ("companies", "Ver Catálogo de Empresas", "companies.view"),
            ("companies", "Alta de Empresa", "companies.create"),
            ("companies", "Gestión de Empresas", "companies.manage"),

            // Reportes
            ("reports", "Reportes y Métricas", "admin.reports"),
            ("reports", "Exportar Reportes en Excel", "reports.export.excel"),

            // Admin
            ("admin", "Administración de Usuarios", "admin.users.manage"),
            ("admin", "Administración de Roles y Permisos", "admin.roles"),
            ("admin", "Administración de Catálogos", "admin.catalogs.manage")
        };

        foreach (var pdef in permDefs)
        {
            if (!existingPerms.Any(p => p.Slug == pdef.Slug))
            {
                var mod = existingModules.FirstOrDefault(m => m.Slug == pdef.ModuleSlug);
                if (mod != null)
                {
                    db.Permissions.Add(new Permission { ModuleId = mod.Id, Name = pdef.Name, Slug = pdef.Slug, IsActive = true });
                }
            }
        }
        await db.SaveChangesAsync();
        existingPerms = await db.Permissions.ToListAsync();

        // 3. Sembrado Idempotente de los 6 Roles Oficiales del Sistema
        var existingRoles = await db.Roles.ToListAsync();
        var existingUserRoles = await db.UserRoles.ToListAsync();
        var existingRolePerms = await db.RolePermissions.ToListAsync();

        var validRoleCodes = new HashSet<string> { "admin", "academico", "vinculacion", "director", "advisor", "student" };
        var obsoleteRoles = existingRoles.Where(r => !validRoleCodes.Contains(r.Code)).ToList();
        foreach (var obs in obsoleteRoles)
        {
            var userRolesForObs = existingUserRoles.Where(ur => ur.RoleId == obs.Id).ToList();
            db.UserRoles.RemoveRange(userRolesForObs);
            var rolePermsForObs = existingRolePerms.Where(rp => rp.RoleId == obs.Id).ToList();
            db.RolePermissions.RemoveRange(rolePermsForObs);
            db.Roles.Remove(obs);
        }
        if (obsoleteRoles.Count > 0)
        {
            await db.SaveChangesAsync();
            existingRoles = await db.Roles.ToListAsync();
            existingUserRoles = await db.UserRoles.ToListAsync();
            existingRolePerms = await db.RolePermissions.ToListAsync();
        }

        var roleDefs = new List<(string Name, string Code, string Desc)>
        {
            ("Super Administrador", "admin", "Control total del sistema y gestión global"),
            ("Académicos y Jefatura", "academico", "Alta y gestión de alumnos/asesores, dictamen y elegibilidad"),
            ("Gestión Tecnológica y Vinculación", "vinculacion", "Alta de empresas, solicitudes de perfiles, cartas de presentación y expedientes"),
            ("Director / Directivos", "director", "Acceso de solo lectura global a todos los módulos"),
            ("Asesores Académicos", "advisor", "Asesoría académica, revisión de 26 semanas y evaluación"),
            ("Estudiantes", "student", "Registro de anteproyectos vinculados a empresas, cronograma y expediente")
        };

        foreach (var rdef in roleDefs)
        {
            var existing = existingRoles.FirstOrDefault(r => r.Code == rdef.Code);
            if (existing is null)
            {
                db.Roles.Add(new Role { Name = rdef.Name, Code = rdef.Code, Description = rdef.Desc, IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
            }
            else if (existing.Name != rdef.Name || existing.Description != rdef.Desc)
            {
                existing.Name = rdef.Name;
                existing.Description = rdef.Desc;
                existing.UpdatedAt = DateTime.UtcNow;
            }
        }
        await db.SaveChangesAsync();
        existingRoles = await db.Roles.ToListAsync();

        // 4. Sembrado Idempotente de Mapeo role_permissions para los 6 Roles
        var academicSlugs = new HashSet<string>
        {
            "students.profile.view", "students.profile.update", "students.eligibility.verify", "students.manage", "advisors.manage",
            "projects.proposals", "projects.proposal.update", "projects.review", "projects.delete", "projects.advisor.assign", "projects.manage",
            "activities.schedule", "activities.progress.validate",
            "evaluations.advisories", "advisories.session.view", "evaluations.summary.view",
            "documents.digital", "documents.upload", "documents.verify",
            "companies.view"
        };

        var vinculacionSlugs = new HashSet<string>
        {
            "companies.view", "companies.create", "companies.manage",
            "documents.digital", "documents.verify", "documents.letters.generate",
            "students.profile.view", "advisors.manage", "projects.proposals", "projects.review",
            "admin.reports", "reports.export.excel"
        };

        var directorSlugs = new HashSet<string>
        {
            "students.profile.view", "advisors.manage", "projects.proposals", "activities.schedule",
            "advisories.session.view", "evaluations.summary.view", "documents.digital", "companies.view",
            "admin.reports", "reports.export.excel", "admin.roles"
        };

        var advisorSlugs = new HashSet<string>
        {
            "projects.review", "projects.advisor",
            "activities.schedule", "activities.progress.validate",
            "evaluations.advisories", "advisories.session.record", "advisories.session.view",
            "evaluations.grading", "evaluations.grade.partial", "evaluations.grade.final", "evaluations.summary.view",
            "documents.digital", "documents.verify"
        };

        var studentSlugs = new HashSet<string>
        {
            "students.profile.view", "students.profile.update",
            "projects.proposals", "projects.proposal.create", "projects.proposal.update", "projects.my",
            "activities.schedule", "activities.progress.report",
            "evaluations.advisories", "advisories.session.view", "advisories.evidence.upload",
            "documents.digital", "documents.upload", "documents.my",
            "companies.view"
        };

        foreach (var role in existingRoles)
        {
            HashSet<string> allowedSlugs;
            if (role.Code == "admin") allowedSlugs = existingPerms.Select(p => p.Slug).ToHashSet();
            else if (role.Code == "academico") allowedSlugs = academicSlugs;
            else if (role.Code == "vinculacion") allowedSlugs = vinculacionSlugs;
            else if (role.Code == "director") allowedSlugs = directorSlugs;
            else if (role.Code == "advisor") allowedSlugs = advisorSlugs;
            else if (role.Code == "student") allowedSlugs = studentSlugs;
            else continue;

            // Purgar permisos no permitidos para este rol
            var currentRolePerms = existingRolePerms.Where(rp => rp.RoleId == role.Id).ToList();
            foreach (var rp in currentRolePerms)
            {
                var perm = existingPerms.FirstOrDefault(p => p.Id == rp.PermissionId);
                if (perm == null || !allowedSlugs.Contains(perm.Slug))
                {
                    db.RolePermissions.Remove(rp);
                }
            }

            // Agregar permisos faltantes autorizados
            foreach (var perm in existingPerms.Where(p => allowedSlugs.Contains(p.Slug)))
            {
                if (!currentRolePerms.Any(rp => rp.PermissionId == perm.Id))
                {
                    db.RolePermissions.Add(new RolePermission { RoleId = role.Id, PermissionId = perm.Id, IsActive = true });
                }
            }
        }
        await db.SaveChangesAsync();

        // 5. Sembrado de Usuarios por Defecto (si no existen)
        var adminUser = await db.Users.FirstOrDefaultAsync(u => u.Email == "admin@monclova.tecnm.mx");
        if (adminUser is null)
        {
            adminUser = new User
            {
                Email = "admin@monclova.tecnm.mx",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(adminPassword),
                Role = UserRole.Admin,
                IsAdmin = true,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            db.Users.Add(adminUser);
            await db.SaveChangesAsync();

            var adminRole = existingRoles.First(r => r.Code == "admin");
            db.UserRoles.Add(new UserRoleAssignment { UserId = adminUser.Id, RoleId = adminRole.Id, IsActive = true });
            await db.SaveChangesAsync();
        }
        else if (!BCrypt.Net.BCrypt.Verify(adminPassword, adminUser.PasswordHash))
        {
            adminUser.PasswordHash = BCrypt.Net.BCrypt.HashPassword(adminPassword);
            adminUser.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
        }

        // 6. Reconciliación idempotente de asignaciones rol-usuario
        var allUsers = await db.Users.Where(u => u.IsActive).ToListAsync();

        foreach (var user in allUsers)
        {
            var roleCode = user.Role switch
            {
                UserRole.Admin => "admin",
                UserRole.Academic => "academico",
                UserRole.Vinculacion => "vinculacion",
                UserRole.Director => "director",
                UserRole.Advisor => "advisor",
                _ => "student"
            };

            var role = existingRoles.FirstOrDefault(r => r.Code == roleCode);
            if (role is null) continue;

            if (!existingUserRoles.Any(ur => ur.UserId == user.Id && ur.RoleId == role.Id))
            {
                db.UserRoles.Add(new UserRoleAssignment
                {
                    UserId = user.Id,
                    RoleId = role.Id,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                });
            }
        }

        var superAdmin = allUsers.FirstOrDefault(u => u.Email == "admin@monclova.tecnm.mx");
        if (superAdmin is not null && !superAdmin.IsAdmin)
        {
            superAdmin.IsAdmin = true;
            superAdmin.UpdatedAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync();

        // 4. Crear o actualizar Vistas de Búsqueda Global en PostgreSQL
        await EnsureSearchViewsCreatedAsync(db);
    }

    private static async Task EnsureSchemaCompatibilityAsync(AppDbContext db)
    {
        var sql = @"
            DO $$ BEGIN
                -- 1. Drop search views if they exist to allow column alterations
                DROP VIEW IF EXISTS vw_search_students CASCADE;
                DROP VIEW IF EXISTS vw_search_advisors CASCADE;
                DROP VIEW IF EXISTS vw_search_projects CASCADE;
                DROP VIEW IF EXISTS vw_search_companies CASCADE;

                IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name='students') THEN
                    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='students' AND column_name='advisor_id') THEN
                        ALTER TABLE students ADD COLUMN advisor_id bigint NULL;
                    END IF;
                END IF;

                IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name='companies') THEN
                    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='companies' AND column_name='name') THEN
                        IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='companies' AND column_name='legal_name') THEN
                            ALTER TABLE companies RENAME COLUMN legal_name TO name;
                        ELSE
                            ALTER TABLE companies ADD COLUMN name character varying(200) NOT NULL DEFAULT '';
                        END IF;
                    END IF;
                    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='companies' AND column_name='sector') THEN
                        IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='companies' AND column_name='industry_sector') THEN
                            ALTER TABLE companies RENAME COLUMN industry_sector TO sector;
                        ELSE
                            ALTER TABLE companies ADD COLUMN sector character varying(100) NULL;
                        END IF;
                    END IF;
                    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='companies' AND column_name='contact_name') THEN
                        IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='companies' AND column_name='contact_person') THEN
                            ALTER TABLE companies RENAME COLUMN contact_person TO contact_name;
                        ELSE
                            ALTER TABLE companies ADD COLUMN contact_name character varying(150) NOT NULL DEFAULT '';
                        END IF;
                    END IF;
                    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='companies' AND column_name='contact_email') THEN
                        IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='companies' AND column_name='email') THEN
                            ALTER TABLE companies RENAME COLUMN email TO contact_email;
                        ELSE
                            ALTER TABLE companies ADD COLUMN contact_email character varying(150) NOT NULL DEFAULT '';
                        END IF;
                    END IF;
                    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='companies' AND column_name='contact_phone') THEN
                        IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='companies' AND column_name='phone') THEN
                            ALTER TABLE companies RENAME COLUMN phone TO contact_phone;
                        ELSE
                            ALTER TABLE companies ADD COLUMN contact_phone character varying(30) NULL;
                        END IF;
                    END IF;
                END IF;

                IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name='projects') THEN
                    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='projects' AND column_name='review_comments') THEN
                        ALTER TABLE projects ADD COLUMN review_comments TEXT NULL;
                    END IF;
                END IF;

                -- Convertir columnas de tipo ENUM de PostgreSQL a VARCHAR para compatibilidad total con EF Core
                IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='projects' AND column_name='status' AND data_type='USER-DEFINED') THEN
                    ALTER TABLE projects ALTER COLUMN status TYPE VARCHAR(50) USING status::text;
                END IF;
                IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='users' AND column_name='role' AND data_type='USER-DEFINED') THEN
                    ALTER TABLE users ALTER COLUMN role TYPE VARCHAR(50) USING role::text;
                END IF;
                IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='project_objectives' AND column_name='status' AND data_type='USER-DEFINED') THEN
                    ALTER TABLE project_objectives ALTER COLUMN status TYPE VARCHAR(50) USING status::text;
                END IF;
                IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='weekly_progress' AND column_name='status' AND data_type='USER-DEFINED') THEN
                    ALTER TABLE weekly_progress ALTER COLUMN status TYPE VARCHAR(50) USING status::text;
                END IF;
                IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='documents' AND column_name='status' AND data_type='USER-DEFINED') THEN
                    ALTER TABLE documents ALTER COLUMN status TYPE VARCHAR(50) USING status::text;
                END IF;
                IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='documents' AND column_name='document_type' AND data_type='USER-DEFINED') THEN
                    ALTER TABLE documents ALTER COLUMN document_type TYPE VARCHAR(50) USING document_type::text;
                END IF;
                IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='evaluations' AND column_name='period' AND data_type='USER-DEFINED') THEN
                    ALTER TABLE evaluations ALTER COLUMN period TYPE VARCHAR(50) USING period::text;
                END IF;
                IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='advisors' AND column_name='advisor_type' AND data_type='USER-DEFINED') THEN
                    ALTER TABLE advisors ALTER COLUMN advisor_type TYPE VARCHAR(50) USING advisor_type::text;
                END IF;
                IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='students' AND column_name='gender' AND data_type='USER-DEFINED') THEN
                    ALTER TABLE students ALTER COLUMN gender TYPE VARCHAR(50) USING gender::text;
                ELSE
                    ALTER TABLE students ADD COLUMN IF NOT EXISTS gender VARCHAR(50);
                END IF;
            END $$;
        ";
        try
        {
            await db.Database.ExecuteSqlRawAsync(sql);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DbSeeder Warning] Error en compatibilidad de esquema: {ex.Message}");
        }
    }

    private static async Task EnsureSearchViewsCreatedAsync(AppDbContext db)
    {
        var viewStatements = new string[]
        {
            "DROP VIEW IF EXISTS vw_search_students CASCADE;",
            @"CREATE VIEW vw_search_students AS
            SELECT 
                s.id AS id,
                s.control_number AS control_number,
                CONCAT(s.first_name, ' ', s.last_name_1, COALESCE(' ' || s.last_name_2, '')) AS full_name,
                COALESCE(u.email, '') AS email,
                COALESCE(s.curp, '') AS curp,
                s.career_id AS career_id,
                s.is_active AS is_active
            FROM students s
            LEFT JOIN users u ON s.user_id = u.id;",

            "DROP VIEW IF EXISTS vw_search_advisors CASCADE;",
            @"CREATE VIEW vw_search_advisors AS
            SELECT 
                a.id AS id,
                a.full_name AS full_name,
                COALESCE(a.title, '') AS title,
                a.advisor_type::text AS advisor_type,
                a.department_id AS department_id,
                COALESCE(u.email, '') AS email,
                COALESCE(a.phone, '') AS phone,
                a.is_active AS is_active
            FROM advisors a
            LEFT JOIN users u ON a.user_id = u.id;",

            "DROP VIEW IF EXISTS vw_search_projects CASCADE;",
            @"CREATE VIEW vw_search_projects AS
            SELECT 
                p.id AS id,
                p.title AS title,
                COALESCE(p.project_type, '') AS project_type,
                p.status::text AS status,
                CONCAT(s.first_name, ' ', s.last_name_1) AS student_name,
                COALESCE(a.full_name, 'Sin Asignar') AS advisor_name,
                COALESCE(c.name, 'Sin Empresa') AS company_name,
                p.is_active AS is_active
            FROM projects p
            LEFT JOIN students s ON p.student_id = s.id
            LEFT JOIN advisors a ON p.advisor_id = a.id
            LEFT JOIN companies c ON p.company_id = c.id
            WHERE p.status::text <> 'draft';",

            "DROP VIEW IF EXISTS vw_search_companies CASCADE;",
            @"CREATE VIEW vw_search_companies AS
            SELECT 
                c.id AS id,
                c.name AS name,
                c.rfc AS rfc,
                COALESCE(c.sector, '') AS sector,
                COALESCE(c.contact_name, '') AS contact_name,
                COALESCE(c.contact_email, '') AS contact_email,
                c.is_active AS is_active
            FROM companies c;"
        };

        try
        {
            foreach (var stmt in viewStatements)
            {
                await db.Database.ExecuteSqlRawAsync(stmt);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DbSeeder Warning] Error creando vistas de búsqueda: {ex.Message}");
        }
    }

    // Datos demo exclusivos de desarrollo: usuarios de prueba y anteproyecto
    // con cronograma vinculado (student_id -> project_id -> cronograma).
    //   juan.perez@monclova.tecnm.mx / 20680123  (número de control)
    //   fernando.rivera@monclova.tecnm.mx / Advisor2026!
    public static async Task SeedDemoDataAsync(AppDbContext db)
    {
        var roles = await db.Roles.ToListAsync();

        var studentUser = await db.Users.FirstOrDefaultAsync(u => u.Email == "juan.perez@monclova.tecnm.mx");
        if (studentUser is null)
        {
            studentUser = new User
            {
                Email = "juan.perez@monclova.tecnm.mx",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("20680123"),
                Role = UserRole.Student,
                IsAdmin = false,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            db.Users.Add(studentUser);
            await db.SaveChangesAsync();

            var studentRole = roles.First(r => r.Code == "student");
            db.UserRoles.Add(new UserRoleAssignment { UserId = studentUser.Id, RoleId = studentRole.Id, IsActive = true });
            await db.SaveChangesAsync();

            var studentProfile = new Student
            {
                UserId = studentUser.Id,
                ControlNumber = "20680123",
                FirstName = "Juan",
                LastName = "Pérez",
                LastName2 = "Gómez",
                Curp = "PEGJ020101HMCRRR01",
                Gender = "Masculino",
                CareerId = 1,
                AcademicPeriodId = 1,
                Gpa = 92.5m,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            db.Students.Add(studentProfile);
            await db.SaveChangesAsync();
        }
        else if (!BCrypt.Net.BCrypt.Verify("20680123", studentUser.PasswordHash))
        {
            studentUser.PasswordHash = BCrypt.Net.BCrypt.HashPassword("20680123");
            studentUser.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
        }

        var advisorUser = await db.Users.FirstOrDefaultAsync(u => u.Email == "fernando.rivera@monclova.tecnm.mx");
        if (advisorUser is null)
        {
            advisorUser = new User
            {
                Email = "fernando.rivera@monclova.tecnm.mx",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Advisor2026!"),
                Role = UserRole.Advisor,
                IsAdmin = false,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            db.Users.Add(advisorUser);
            await db.SaveChangesAsync();

            var advisorRole = roles.First(r => r.Code == "advisor");
            db.UserRoles.Add(new UserRoleAssignment { UserId = advisorUser.Id, RoleId = advisorRole.Id, IsActive = true });
            await db.SaveChangesAsync();

            var advisorProfile = new Advisor
            {
                UserId = advisorUser.Id,
                Title = "M.C.",
                FullName = "Fernando Rivera López",
                Phone = "8661234567",
                DepartmentId = 1,
                AdvisorType = AdvisorType.Internal,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            db.Advisors.Add(advisorProfile);
            await db.SaveChangesAsync();
        }
        else if (!BCrypt.Net.BCrypt.Verify("Advisor2026!", advisorUser.PasswordHash))
        {
            advisorUser.PasswordHash = BCrypt.Net.BCrypt.HashPassword("Advisor2026!");
            advisorUser.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
        }

        // 7. Sembrado Idempotente de Empresas Receptoras de muestra.
        if (!await db.Companies.AnyAsync())
        {
            db.Companies.AddRange(
                new Company
                {
                    Name = "Servicios Tecnológicos e Innovación Industrial S.A. de C.V.",
                    Rfc = "STI150610MH2",
                    Sector = "Desarrollo de Software / TI",
                    Address = "Av. Tecnológico #1200, Monclova, Coahuila",
                    ContactName = "Ing. Miguel Ángel Perales",
                    ContactEmail = "mperales@sti-coahuila.mx",
                    ContactPhone = "866-632-9900",
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                },
                new Company
                {
                    Name = "Altos Hornos de México S.A.B. de C.V. (AHMSA)",
                    Rfc = "AHM441231AB1",
                    Sector = "Siderúrgico / Metalmecánico",
                    Address = "Prolongación Juárez s/n, Monclova, Coahuila",
                    ContactName = "Ing. Carlos Mendoza Silva",
                    ContactEmail = "cmendoza@ahmsa.com",
                    ContactPhone = "866-649-3000",
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                },
                new Company
                {
                    Name = "Ternium México S.A. de C.V.",
                    Rfc = "TME050209TX1",
                    Sector = "Siderúrgico / Industrial",
                    Address = "Carretera 57 Km 12, Monclova, Coahuila",
                    ContactName = "Lic. Elena Torres Cantú",
                    ContactEmail = "etorres@ternium.com.mx",
                    ContactPhone = "866-649-8000",
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                },
                new Company
                {
                    Name = "Gunderson-GIMSA S.A. de C.V.",
                    Rfc = "GGI930815KL9",
                    Sector = "Ferroviario / Manufactura",
                    Address = "Av. Industrial #850, Monclova, Coahuila",
                    ContactName = "Ing. Roberto Ramírez Ramos",
                    ContactEmail = "rramirez@gunderson.com.mx",
                    ContactPhone = "866-633-1200",
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                },
                new Company
                {
                    Name = "Technotrim de México S. de R.L. de C.V.",
                    Rfc = "TME981102RT4",
                    Sector = "Automotriz / Textil",
                    Address = "Parque Industrial Monclova, Coahuila",
                    ContactName = "Lic. Sofia Villarreal Reyes",
                    ContactEmail = "svillarreal@technotrim.com",
                    ContactPhone = "866-641-5500",
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                }
            );
            await db.SaveChangesAsync();
        }

        // Empresa receptora por defecto para los anteproyectos demo.
        var defaultCompany = await db.Companies
            .Where(c => c.IsActive)
            .OrderBy(c => c.Id)
            .FirstOrDefaultAsync();

        // 8. Sembrado Idempotente del anteproyecto vigente y cronograma del estudiante semilla.
        //    Garantiza la cadena referencial student_id -> project_id -> cronograma (weekly_activities).
        var seedStudent = await db.Students.FirstOrDefaultAsync(s => s.ControlNumber == "20680123");
        if (seedStudent != null && !await db.Projects.AnyAsync(p => p.StudentId == seedStudent.Id && p.IsActive))
        {
            var seedProject = new Project
            {
                StudentId = seedStudent.Id,
                CompanyId = defaultCompany?.Id ?? 0,
                Title = "Sistema Integral de Gestión de Residencias Profesionales",
                ProjectType = "Desarrollo Tecnológico",
                ProblemStatement = "La División carece de una herramienta unificada para el registro, dictamen y seguimiento de residencias profesionales.",
                Justification = "Automatizar el proceso de residencia profesional reduce tiempos administrativos y mejora la trazabilidad de los expedientes.",
                GeneralObjective = "Desarrollar un sistema web integral para la gestión del proceso de residencia profesional del TecNM.",
                Status = ProjectStatus.InProgress,
                StartDate = new DateTime(2026, 1, 15, 0, 0, 0, DateTimeKind.Utc),
                EndDate = new DateTime(2026, 6, 15, 0, 0, 0, DateTimeKind.Utc),
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            seedProject.Objectives.Add(new ProjectObjective
            {
                ObjectiveNumber = 1,
                Description = "Analizar el proceso actual de gestión de residencias profesionales.",
                Status = "completed",
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
            seedProject.Objectives.Add(new ProjectObjective
            {
                ObjectiveNumber = 2,
                Description = "Diseñar la arquitectura del sistema y el modelo de datos relacional.",
                Status = "in_progress",
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
            seedProject.Objectives.Add(new ProjectObjective
            {
                ObjectiveNumber = 3,
                Description = "Implementar y desplegar los módulos del sistema en el campus.",
                Status = "pending",
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });

            db.Projects.Add(seedProject);
            await db.SaveChangesAsync();

            var seedActivities = new List<(int Number, string Title)>
            {
                (1, "Análisis de requerimientos del sistema de residencias"),
                (2, "Diseño de base de datos y arquitectura de módulos"),
                (3, "Implementación del registro y dictamen de anteproyectos"),
                (4, "Implementación del cronograma de actividades de 26 semanas")
            };

            foreach (var (number, title) in seedActivities)
            {
                var activity = new WeeklyActivity
                {
                    ProjectId = seedProject.Id,
                    ActivityNumber = number,
                    Title = title,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                for (int w = 1; w <= 26; w++)
                {
                    activity.Progresses.Add(new WeeklyProgress
                    {
                        WeekNumber = w,
                        Status = "pending",
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    });
                }

                db.WeeklyActivities.Add(activity);
            }

            await db.SaveChangesAsync();
        }

        // Asignar empresa por defecto a anteproyectos huérfanos o con empresa inexistente.
        var validCompanyIds = (await db.Companies.Select(c => (long?)c.Id).ToListAsync()).ToHashSet();
        var orphanProjects = await db.Projects
            .Where(p => p.CompanyId == 0 || !validCompanyIds.Contains(p.CompanyId))
            .ToListAsync();
        if (orphanProjects.Count > 0 && defaultCompany != null)
        {
            foreach (var p in orphanProjects)
            {
                p.CompanyId = defaultCompany.Id;
            }
            await db.SaveChangesAsync();
        }
    }
}
