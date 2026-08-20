using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace TecNM.Residency.Activities;

public class WeeklyActivityConfiguration : IEntityTypeConfiguration<WeeklyActivity>
{
    public void Configure(EntityTypeBuilder<WeeklyActivity> builder)
    {
        builder.ToTable("weekly_activities");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.Id)
            .HasColumnName("id")
            .ValueGeneratedOnAdd();

        builder.Property(a => a.ProjectId)
            .HasColumnName("project_id")
            .IsRequired();

        builder.Property(a => a.ActivityNumber)
            .HasColumnName("activity_number")
            .IsRequired();

        builder.Property(a => a.Title)
            .HasColumnName("title")
            .HasMaxLength(250)
            .IsRequired();

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

        builder.HasMany(a => a.Progresses)
            .WithOne(p => p.Activity)
            .HasForeignKey(p => p.ActivityId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
