using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace TecNM.Residency.Students;

public class StudentConfiguration : IEntityTypeConfiguration<Student>
{
    public void Configure(EntityTypeBuilder<Student> builder)
    {
        builder.ToTable("students");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.Id)
            .HasColumnName("id")
            .ValueGeneratedOnAdd();

        builder.Property(s => s.UserId)
            .HasColumnName("user_id")
            .IsRequired();

        builder.HasIndex(s => s.UserId)
            .IsUnique();

        builder.Property(s => s.ControlNumber)
            .HasColumnName("control_number")
            .HasMaxLength(20)
            .IsRequired();

        builder.HasIndex(s => s.ControlNumber)
            .IsUnique();

        builder.Property(s => s.FirstName)
            .HasColumnName("first_name")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(s => s.LastName)
            .HasColumnName("last_name_1")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(s => s.LastName2)
            .HasColumnName("last_name_2")
            .HasMaxLength(100);

        builder.Property(s => s.Curp)
            .HasColumnName("curp")
            .HasMaxLength(20);

        builder.Property(s => s.Gender)
            .HasColumnName("gender")
            .HasMaxLength(20);

        builder.Property(s => s.CareerId)
            .HasColumnName("career_id")
            .IsRequired();

        builder.Property(s => s.AcademicPeriodId)
            .HasColumnName("academic_period_id");

        builder.Property(s => s.Gpa)
            .HasColumnName("gpa")
            .HasColumnType("numeric(5,2)")
            .IsRequired();

        builder.Property(s => s.IsActive)
            .HasColumnName("is_active")
            .HasDefaultValue(true)
            .IsRequired();

        builder.Property(s => s.IsVisible)
            .HasColumnName("is_visible")
            .HasDefaultValue(true)
            .IsRequired();

        builder.Property(s => s.DisplayOrder)
            .HasColumnName("display_order")
            .HasDefaultValue(0)
            .IsRequired();

        builder.Property(s => s.CreatedBy)
            .HasColumnName("created_by");

        builder.Property(s => s.UpdatedBy)
            .HasColumnName("updated_by");

        builder.Property(s => s.DeletedBy)
            .HasColumnName("deleted_by");

        builder.Property(s => s.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(s => s.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();

        builder.Property(s => s.DeletedAt)
            .HasColumnName("deleted_at");

        builder.HasOne(s => s.User)
            .WithMany()
            .HasForeignKey(s => s.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
