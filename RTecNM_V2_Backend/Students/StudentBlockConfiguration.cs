using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace TecNM.Residency.Students;

public class StudentBlockConfiguration : IEntityTypeConfiguration<StudentBlock>
{
    public void Configure(EntityTypeBuilder<StudentBlock> builder)
    {
        builder.ToTable("student_blocks");

        builder.HasKey(b => b.Id);

        builder.Property(b => b.Id)
            .HasColumnName("id")
            .ValueGeneratedOnAdd();

        builder.Property(b => b.StudentId)
            .HasColumnName("student_id")
            .IsRequired();

        builder.HasIndex(b => b.StudentId);

        builder.Property(b => b.Reason)
            .HasColumnName("reason")
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(b => b.BlockedBy)
            .HasColumnName("blocked_by")
            .IsRequired();

        builder.Property(b => b.BlockedAt)
            .HasColumnName("blocked_at")
            .IsRequired();

        builder.Property(b => b.IsActive)
            .HasColumnName("is_active")
            .HasDefaultValue(true)
            .IsRequired();

        builder.Property(b => b.UnblockedAt)
            .HasColumnName("unblocked_at");

        builder.Property(b => b.UnblockedBy)
            .HasColumnName("unblocked_by");

        builder.Property(b => b.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(b => b.CreatedBy)
            .HasColumnName("created_by");

        builder.Property(b => b.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();

        builder.Property(b => b.UpdatedBy)
            .HasColumnName("updated_by");

        builder.Property(b => b.DeletedAt)
            .HasColumnName("deleted_at");

        builder.Property(b => b.DeletedBy)
            .HasColumnName("deleted_by");

        builder.Property(b => b.IsVisible)
            .HasColumnName("is_visible")
            .HasDefaultValue(true)
            .IsRequired();

        builder.Property(b => b.DisplayOrder)
            .HasColumnName("display_order")
            .HasDefaultValue(0)
            .IsRequired();

        builder.HasOne(b => b.Student)
            .WithMany(s => s.Blocks)
            .HasForeignKey(b => b.StudentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(b => b.BlockedByUser)
            .WithMany()
            .HasForeignKey(b => b.BlockedBy)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(b => b.UnblockedByUser)
            .WithMany()
            .HasForeignKey(b => b.UnblockedBy)
            .OnDelete(DeleteBehavior.Restrict);
    }
}