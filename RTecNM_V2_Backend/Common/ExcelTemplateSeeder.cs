using System.Data;
using MiniExcelLibs;
using Microsoft.Extensions.Logging;

namespace TecNM.Residency.Common;

public static class ExcelTemplateSeeder
{
    public static void EnsureTemplatesExist(string contentRootPath, ILogger logger)
    {
        var templatesDir = Path.Combine(contentRootPath, "uploads", "templates", "excel");
        if (!Directory.Exists(templatesDir))
        {
            Directory.CreateDirectory(templatesDir);
            logger.LogInformation("Creado directorio de plantillas Excel en: {TemplatesDir}", templatesDir);
        }

        // 1. Plantilla Alumnos (Solo encabezados limpios sin filas de ejemplo ni colores)
        var studentTemplatePath = Path.Combine(templatesDir, "Plantilla_Alumnos.xlsx");
        var studentTable = new DataTable();
        studentTable.Columns.Add("Matricula");
        studentTable.Columns.Add("Apellidos");
        studentTable.Columns.Add("Nombre");
        studentTable.Columns.Add("Sexo");
        studentTable.Columns.Add("Carrera");
        studentTable.Columns.Add("Semestre");
        studentTable.Columns.Add("Email");

        MiniExcel.SaveAs(studentTemplatePath, studentTable, overwriteFile: true);
        logger.LogInformation("Generada plantilla limpia para estudiantes en: {Path}", studentTemplatePath);

        // 2. Plantilla Empresas (Solo encabezados limpios sin filas de ejemplo ni colores)
        var companyTemplatePath = Path.Combine(templatesDir, "Plantilla_Empresas.xlsx");
        var companyTable = new DataTable();
        companyTable.Columns.Add("Nombre");
        companyTable.Columns.Add("RFC");
        companyTable.Columns.Add("Sector");
        companyTable.Columns.Add("Dirección");
        companyTable.Columns.Add("NombreContacto");
        companyTable.Columns.Add("CorreoContacto");
        companyTable.Columns.Add("TeléfonoContacto");

        MiniExcel.SaveAs(companyTemplatePath, companyTable, overwriteFile: true);
        logger.LogInformation("Generada plantilla limpia para empresas en: {Path}", companyTemplatePath);

        // 3. Plantilla Asesores (Solo encabezados limpios sin filas de ejemplo ni colores)
        var advisorTemplatePath = Path.Combine(templatesDir, "Plantilla_Asesores.xlsx");
        var advisorTable = new DataTable();
        advisorTable.Columns.Add("Nombre");
        advisorTable.Columns.Add("Titulo");
        advisorTable.Columns.Add("Email");
        advisorTable.Columns.Add("Telefono");
        advisorTable.Columns.Add("Departamento");

        MiniExcel.SaveAs(advisorTemplatePath, advisorTable, overwriteFile: true);
        logger.LogInformation("Generada plantilla limpia para asesores en: {Path}", advisorTemplatePath);
    }
}
