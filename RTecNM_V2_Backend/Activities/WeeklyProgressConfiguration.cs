using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace TecNM.Residency.Activities;

public class WeeklyProgressConfiguration : IEntityTypeConfiguration<WeeklyProgress>
{
    public void Configure(EntityTypeBuilder<WeeklyProgress> builder)
    {
        builder.ToTable("weekly_progress");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Id)
            .HasColumnName("id")
            .ValueGeneratedOnAdd();

        builder.Property(p => p.ActivityId)
            .HasColumnName("activity_id")
            .IsRequired();

        builder.Property(p => p.WeekNumber)
            .HasColumnName("week_number")
            .IsRequired();

        builder.Property(p => p.Status)
            .HasColumnName("status")
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(p => p.Notes)
            .HasColumnName("notes");

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

        builder.HasIndex(p => new { p.ActivityId, p.WeekNumber })
            .IsUnique();
    }
}
