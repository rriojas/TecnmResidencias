using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace TecNM.Residency.Evaluations;

public class AdvisorySessionConfiguration : IEntityTypeConfiguration<AdvisorySession>
{
    public void Configure(EntityTypeBuilder<AdvisorySession> builder)
    {
        builder.ToTable("advisory_sessions");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.Id)
            .HasColumnName("id")
            .ValueGeneratedOnAdd();

        builder.Property(a => a.ProjectId)
            .HasColumnName("project_id")
            .IsRequired();

        builder.Property(a => a.AdvisorId)
            .HasColumnName("advisor_id")
            .IsRequired();

        builder.Property(a => a.SessionDate)
            .HasColumnName("session_date")
            .IsRequired();

        builder.Property(a => a.TopicsCovered)
            .HasColumnName("topics_covered")
            .IsRequired();

        builder.Property(a => a.StudentAgreements)
            .HasColumnName("student_agreements");

        builder.Property(a => a.ReviewStatus)
            .HasColumnName("review_status")
            .HasMaxLength(30)
            .HasDefaultValue("pending")
            .IsRequired();

        builder.Property(a => a.ReviewNotes)
            .HasColumnName("review_notes");

        builder.Property(a => a.ReviewedAt)
            .HasColumnName("reviewed_at");

        builder.Property(a => a.ReviewedBy)
            .HasColumnName("reviewed_by");

        builder.HasOne(a => a.Project)
            .WithMany()
            .HasForeignKey(a => a.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(a => a.Advisor)
            .WithMany()
            .HasForeignKey(a => a.AdvisorId)
            .OnDelete(DeleteBehavior.NoAction);

        // BaseEntity fields
        builder.Property(a => a.IsActive).HasColumnName("is_active");
        builder.Property(a => a.IsVisible).HasColumnName("is_visible");
        builder.Property(a => a.DisplayOrder).HasColumnName("display_order");
        builder.Property(a => a.CreatedBy).HasColumnName("created_by");
        builder.Property(a => a.UpdatedBy).HasColumnName("updated_by");
        builder.Property(a => a.DeletedBy).HasColumnName("deleted_by");
        builder.Property(a => a.CreatedAt).HasColumnName("created_at");
        builder.Property(a => a.UpdatedAt).HasColumnName("updated_at");
        builder.Property(a => a.DeletedAt).HasColumnName("deleted_at");
    }
}
