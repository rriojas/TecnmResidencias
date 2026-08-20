using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace TecNM.Residency.Projects;

public class ProjectConfiguration : IEntityTypeConfiguration<Project>
{
    public void Configure(EntityTypeBuilder<Project> builder)
    {
        builder.ToTable("projects");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Id)
            .HasColumnName("id")
            .ValueGeneratedOnAdd();

        builder.Property(p => p.StudentId)
            .HasColumnName("student_id")
            .IsRequired();

        builder.Property(p => p.AdvisorId)
            .HasColumnName("advisor_id");

        builder.Property(p => p.CompanyId)
            .HasColumnName("company_id")
            .IsRequired();

        builder.HasOne(p => p.Student)
            .WithMany()
            .HasForeignKey(p => p.StudentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(p => p.Advisor)
            .WithMany()
            .HasForeignKey(p => p.AdvisorId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(p => p.Company)
            .WithMany()
            .HasForeignKey(p => p.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(p => p.Title)
            .HasColumnName("title")
            .HasMaxLength(250)
            .IsRequired();

        builder.Property(p => p.ProjectType)
            .HasColumnName("project_type")
            .HasMaxLength(100);

        builder.Property(p => p.ProblemStatement)
            .HasColumnName("problem_statement")
            .IsRequired();

        builder.Property(p => p.Justification)
            .HasColumnName("justification")
            .IsRequired();

        builder.Property(p => p.GeneralObjective)
            .HasColumnName("general_objective")
            .IsRequired();

        builder.Property(p => p.Status)
            .HasColumnName("status")
            .HasConversion(
                v => StatusToString(v),
                v => StringToStatus(v))
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(p => p.StartDate)
            .HasColumnName("start_date");

        builder.Property(p => p.EndDate)
            .HasColumnName("end_date");

        // BaseEntity fields
        builder.Property(p => p.IsActive).HasColumnName("is_active");
        builder.Property(p => p.IsVisible).HasColumnName("is_visible");
        builder.Property(p => p.DisplayOrder).HasColumnName("display_order");
        builder.Property(p => p.CreatedBy).HasColumnName("created_by");
        builder.Property(p => p.UpdatedBy).HasColumnName("updated_by");
        builder.Property(p => p.DeletedBy).HasColumnName("deleted_by");
        builder.Property(p => p.CreatedAt).HasColumnName("created_at");
        builder.Property(p => p.UpdatedAt).HasColumnName("updated_at");
        builder.Property(p => p.DeletedAt).HasColumnName("deleted_at");

        builder.HasMany(p => p.Objectives)
            .WithOne(o => o.Project)
            .HasForeignKey(o => o.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);
    }

    private static string StatusToString(ProjectStatus status) => status switch
    {
        ProjectStatus.Draft => "draft",
        ProjectStatus.Pending => "pending",
        ProjectStatus.Proposed => "proposed",
        ProjectStatus.UnderReview => "under_review",
        ProjectStatus.Approved => "approved",
        ProjectStatus.InProgress => "in_progress",
        ProjectStatus.Completed => "completed",
        ProjectStatus.Rejected => "rejected",
        ProjectStatus.Cancelled => "cancelled",
        _ => status.ToString().ToLowerInvariant()
    };

    private static ProjectStatus StringToStatus(string value) => (value ?? "").ToLowerInvariant() switch
    {
        "draft" or "borrador" => ProjectStatus.Draft,
        "pending" or "pendiente" => ProjectStatus.Pending,
        "proposed" or "propuesto" => ProjectStatus.Proposed,
        "under_review" or "underreview" or "en_revision" or "en revisión" or "en revision" => ProjectStatus.UnderReview,
        "approved" or "aprobado" => ProjectStatus.Approved,
        "in_progress" or "inprogress" or "en_progreso" => ProjectStatus.InProgress,
        "completed" or "completado" => ProjectStatus.Completed,
        "rejected" or "rechazado" => ProjectStatus.Rejected,
        "cancelled" or "cancelado" => ProjectStatus.Cancelled,
        _ => ProjectStatus.Pending
    };
}
