using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace TecNM.Residency.Evaluations;

public class EvaluationConfiguration : IEntityTypeConfiguration<Evaluation>
{
    public void Configure(EntityTypeBuilder<Evaluation> builder)
    {
        builder.ToTable("evaluations");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnName("id")
            .ValueGeneratedOnAdd();

        builder.Property(e => e.ProjectId)
            .HasColumnName("project_id")
            .IsRequired();

        builder.Property(e => e.EvaluatorId)
            .HasColumnName("evaluator_id")
            .IsRequired();

        builder.Property(e => e.EvaluationPeriod)
            .HasColumnName("evaluation_period")
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(e => e.Score)
            .HasColumnName("score")
            .HasPrecision(5, 2)
            .IsRequired();

        builder.Property(e => e.Feedback)
            .HasColumnName("feedback");

        builder.HasOne(e => e.Project)
            .WithMany()
            .HasForeignKey(e => e.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        // BaseEntity fields
        builder.Property(e => e.IsActive).HasColumnName("is_active");
        builder.Property(e => e.IsVisible).HasColumnName("is_visible");
        builder.Property(e => e.DisplayOrder).HasColumnName("display_order");
        builder.Property(e => e.CreatedBy).HasColumnName("created_by");
        builder.Property(e => e.UpdatedBy).HasColumnName("updated_by");
        builder.Property(e => e.DeletedBy).HasColumnName("deleted_by");
        builder.Property(e => e.CreatedAt).HasColumnName("created_at");
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at");
        builder.Property(e => e.DeletedAt).HasColumnName("deleted_at");
    }
}
