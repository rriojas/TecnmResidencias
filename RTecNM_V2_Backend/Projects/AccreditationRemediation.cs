using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TecNM.Residency.Common;
using TecNM.Residency.Common.Settings;
using TecNM.Residency.Documents;
using TecNM.Residency.Evaluations;

namespace TecNM.Residency.Projects;

public static class AccreditationRemediation
{
    private const string SettingKey = "remediation.innovatec_autocompleted_fixed_v1";

    public static async Task RemediateAutocompletedAccreditationsAsync(AppDbContext dbContext, ILogger logger)
    {
        try
        {
            // 1. Verificar si ya se aplicó la remediación de forma idempotente
            var setting = await dbContext.SystemSettings.FirstOrDefaultAsync(s => s.Key == SettingKey);
            if (setting != null && setting.Value == "true")
            {
                logger.LogInformation("[Remediation] Remediación de acreditaciones InnovaTec ya fue aplicada previamente. Omitiendo.");
                return;
            }

            // 2. Buscar proyectos de acreditación indebidamente completados:
            // Criterios estrictos:
            // - Estado actual = Completed
            // - Tipo = acreditacion_innovatec o acreditacion_hackatec (o contiene innovatec/hackatec)
            // - AdvisorId == null (nunca tuvieron asesor asignado)
            var candidates = await dbContext.Projects
                .Include(p => p.Student)
                .Where(p => p.IsActive && p.Status == ProjectStatus.Completed)
                .Where(p => p.ProjectType == "acreditacion_innovatec" ||
                            p.ProjectType == "acreditacion_hackatec" ||
                            (p.ProjectType != null && (p.ProjectType.ToLower().Contains("innovatec") || p.ProjectType.ToLower().Contains("hackatec"))))
                .Where(p => p.AdvisorId == null)
                .ToListAsync();

            if (!candidates.Any())
            {
                logger.LogInformation("[Remediation] No se detectaron proyectos de acreditación indebidamente completados.");
                if (setting == null)
                {
                    dbContext.SystemSettings.Add(new SystemSetting
                    {
                        Key = SettingKey,
                        Value = "true",
                        Description = "Marca de remediación única de acreditaciones InnovaTecNM/HackaTec completadas indebidamente",
                        UpdatedAt = DateTime.UtcNow
                    });
                    await dbContext.SaveChangesAsync();
                }
                return;
            }

            int remediatedCount = 0;

            foreach (var project in candidates)
            {
                // Verificar que no cuente con documentos ordinarios (formato 29, 30, etc.)
                var ordinaryDocs = await dbContext.Documents
                    .Where(d => d.ProjectId == project.Id && d.IsActive &&
                                d.DocumentType != DocumentType.ConstanciaAcreditacion &&
                                d.DocumentType != DocumentType.Anteproyecto)
                    .ToListAsync();

                if (ordinaryDocs.Any())
                {
                    logger.LogWarning("[Remediation] Proyecto #{ProjectId} tiene documentos ordinarios subidos. Se omite para evitar sobreescritura.", project.Id);
                    continue;
                }

                // Identificar y remover evaluaciones del sistema automáticas (con feedback de acreditación)
                var autoEvaluations = await dbContext.Evaluations
                    .Where(e => e.ProjectId == project.Id)
                    .Where(e => e.Feedback != null && e.Feedback.Contains("Acreditación de Residencia Profesional por evento institucional"))
                    .ToListAsync();

                if (autoEvaluations.Any())
                {
                    dbContext.Evaluations.RemoveRange(autoEvaluations);
                }

                // Reactivar proyecto a Approved para habilitar asignación de asesor y entrega documental
                project.Status = ProjectStatus.Approved;
                project.ReviewComments = !string.IsNullOrWhiteSpace(project.ReviewComments)
                    ? $"{project.ReviewComments} [Reactivado a En Residencia para asignación de asesor y seguimiento documental]"
                    : "Reactivado a En Residencia para asignación de asesor y seguimiento documental.";
                project.UpdatedAt = DateTime.UtcNow;

                logger.LogInformation(
                    "[Remediation] Proyecto #{ProjectId} (Alumno: {ControlNumber}) reactivado a Approved. Eliminadas {EvalCount} evaluaciones automáticas.",
                    project.Id, project.Student?.ControlNumber ?? "N/A", autoEvaluations.Count);

                remediatedCount++;
            }

            // Registrar bandera para no repetir jamás
            if (setting == null)
            {
                dbContext.SystemSettings.Add(new SystemSetting
                {
                    Key = SettingKey,
                    Value = "true",
                    Description = "Marca de remediación única de acreditaciones InnovaTecNM/HackaTec completadas indebidamente",
                    UpdatedAt = DateTime.UtcNow
                });
            }
            else
            {
                setting.Value = "true";
                setting.UpdatedAt = DateTime.UtcNow;
                dbContext.SystemSettings.Update(setting);
            }

            await dbContext.SaveChangesAsync();
            logger.LogInformation("[Remediation] Remediación completada exitosamente. Total proyectos reactivados: {Count}.", remediatedCount);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[Remediation] Error durante la remediación de proyectos de acreditación.");
        }
    }
}
