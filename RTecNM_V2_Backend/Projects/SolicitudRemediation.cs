using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TecNM.Residency.Common;
using TecNM.Residency.Documents;

namespace TecNM.Residency.Projects;

public static class SolicitudRemediation
{
    public static async Task RemediateActiveProjectSolicitudesAsync(AppDbContext dbContext, ILogger logger)
    {
        try
        {
            // Recorrer proyectos que ya enviaron anteproyecto y no están cancelados ni eliminados
            var activeProjects = await dbContext.Projects
                .Where(p => p.IsActive && p.DeletedAt == null && p.Status != ProjectStatus.Cancelled && p.Status != ProjectStatus.Draft)
                .ToListAsync();

            if (!activeProjects.Any())
            {
                logger.LogInformation("[SolicitudRemediation] No hay anteproyectos activos pendientes de confirmación de solicitud.");
                return;
            }

            var projectIds = activeProjects.Select(p => p.Id).ToList();

            var existingSolicitudes = await dbContext.Documents
                .Where(d => projectIds.Contains(d.ProjectId) && d.DocumentType == DocumentType.Solicitud && d.IsActive)
                .ToListAsync();

            var solicitudesByProject = existingSolicitudes
                .GroupBy(d => d.ProjectId)
                .ToDictionary(g => g.Key, g => g.First());

            int confirmedCount = 0;
            int updatedCount = 0;

            foreach (var project in activeProjects)
            {
                if (!solicitudesByProject.TryGetValue(project.Id, out var existingDoc))
                {
                    var newDoc = new Document
                    {
                        ProjectId = project.Id,
                        DocumentType = DocumentType.Solicitud,
                        FileName = $"Solicitud_Anteproyecto_{project.Id}.pdf",
                        FilePath = $"uploads/generated/solicitud_{project.Id}.pdf",
                        FileSize = 125_000,
                        ContentType = "application/pdf",
                        Status = DocumentStatus.Approved,
                        IsActive = true,
                        IsVisible = true,
                        DisplayOrder = 1,
                        UploadedAt = project.CreatedAt,
                        CreatedAt = project.CreatedAt,
                        UpdatedAt = DateTime.UtcNow,
                        CreatedBy = project.StudentId
                    };
                    dbContext.Documents.Add(newDoc);
                    confirmedCount++;
                }
                else if (existingDoc.Status != DocumentStatus.Approved)
                {
                    existingDoc.Status = DocumentStatus.Approved;
                    existingDoc.UpdatedAt = DateTime.UtcNow;
                    updatedCount++;
                }
            }

            if (confirmedCount > 0 || updatedCount > 0)
            {
                await dbContext.SaveChangesAsync();
                logger.LogInformation(
                    "[SolicitudRemediation] Solicitudes confirmadas exitosamente. Creadas: {Created}, Actualizadas a Aprobadas: {Updated}.",
                    confirmedCount, updatedCount);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[SolicitudRemediation] Error durante la confirmación de solicitudes de anteproyectos activos.");
        }
    }
}
