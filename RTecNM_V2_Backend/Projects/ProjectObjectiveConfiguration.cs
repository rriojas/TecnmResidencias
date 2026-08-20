using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace TecNM.Residency.Projects;

public class ProjectObjectiveConfiguration : IEntityTypeConfiguration<ProjectObjective>
{
    public void Configure(EntityTypeBuilder<ProjectObjective> builder)
    {
        builder.ToTable("project_objectives");

        builder.HasKey(o => o.Id);

        builder.Property(o => o.Id)
            .HasColumnName("id")
            .ValueGeneratedOnAdd();

        builder.Property(o => o.ProjectId)
            .HasColumnName("project_id")
            .IsRequired();

        builder.Property(o => o.ObjectiveNumber)
            .HasColumnName("objective_number")
            .IsRequired();

        builder.Property(o => o.Description)
            .HasColumnName("description")
            .IsRequired();

        builder.Property(o => o.Status)
            .HasColumnName("status")
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(o => o.Notes)
            .HasColumnName("notes");

        // BaseEntity fields
        builder.Property(o => o.IsActive).HasColumnName("is_active");
        builder.Property(o => o.IsVisible).HasColumnName("is_visible");
        builder.Property(o => o.DisplayOrder).HasColumnName("display_order");
        builder.Property(o => o.CreatedBy).HasColumnName("created_by");
        builder.Property(o => o.UpdatedBy).HasColumnName("updated_by");
        builder.Property(o => o.DeletedBy).HasColumnName("deleted_by");
        builder.Property(o => o.CreatedAt).HasColumnName("created_at");
        builder.Property(o => o.UpdatedAt).HasColumnName("updated_at");
        builder.Property(o => o.DeletedAt).HasColumnName("deleted_at");

        builder.HasIndex(o => new { o.ProjectId, o.ObjectiveNumber })
            .IsUnique();
    }
}
