using MiniExcelLibs;
using Microsoft.Extensions.Logging;

namespace TecNM.Residency.Common;

public static class ExcelTemplateSeeder
{
    public static void EnsureTemplatesExist(string contentRootPath, ILogger logger)
    {
        var templatesDir = Path.Combine(contentRootPath, "Templates", "Excel");
        if (!Directory.Exists(templatesDir))
        {
            Directory.CreateDirectory(templatesDir);
            logger.LogInformation("Creado directorio de plantillas Excel en: {TemplatesDir}", templatesDir);
        }

        var studentTemplatePath = Path.Combine(templatesDir, "Plantilla_Alumnos.xlsx");
        if (!File.Exists(studentTemplatePath))
        {
            var studentDemoRows = new[]
            {
                new {
                    Matricula = "20040101",
                    Apellidos = "García López",
                    Nombre = "Juan Carlos",
                    Sexo = "M",
                    Carrera = "Sistemas Computacionales",
                    Semestre = "9",
                    Email = "l20040101@monclova.tecnm.mx"
                },
                new {
                    Matricula = "20040102",
                    Apellidos = "Hernández Martínez",
                    Nombre = "María Fernanda",
                    Sexo = "F",
                    Carrera = "Industrial",
                    Semestre = "9",
                    Email = "l20040102@monclova.tecnm.mx"
                }
            };
            MiniExcel.SaveAs(studentTemplatePath, studentDemoRows, overwriteFile: true);
            logger.LogInformation("Generada plantilla por defecto para estudiantes en: {Path}", studentTemplatePath);
        }

        var companyTemplatePath = Path.Combine(templatesDir, "Plantilla_Empresas.xlsx");
        if (!File.Exists(companyTemplatePath))
        {
            var companyDemoRows = new[]
            {
                new {
                    Nombre = "AHMSA - Altos Hornos de México",
                    RFC = "AHM800101AAA",
                    Sector = "Privado",
                    Dirección = "Av. Prolongación Juárez S/N, Monclova, Coah.",
                    NombreContacto = "Ing. Roberto Treviño",
                    CorreoContacto = "rtrevino@ahmsa.com",
                    TeléfonoContacto = "866-649-2000"
                },
                new {
                    Nombre = "Teksid Hierro de México",
                    RFC = "THM950512BBB",
                    Sector = "Privado",
                    Dirección = "Carr. 57 Km 12, Frontera, Coah.",
                    NombreContacto = "Lic. Ana Sofia Morales",
                    CorreoContacto = "amorales@teksid.com",
                    TeléfonoContacto = "866-649-5000"
                }
            };
            MiniExcel.SaveAs(companyTemplatePath, companyDemoRows, overwriteFile: true);
            logger.LogInformation("Generada plantilla por defecto para empresas en: {Path}", companyTemplatePath);
        }
    }
}
