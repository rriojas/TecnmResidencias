using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace TecNM.Residency.Auth;

public class UserCareerConfiguration : IEntityTypeConfiguration<UserCareer>
{
    public void Configure(EntityTypeBuilder<UserCareer> builder)
    {
        builder.ToTable("user_careers");

        builder.HasKey(uc => uc.Id);

        builder.Property(uc => uc.Id)
            .HasColumnName("id")
            .ValueGeneratedOnAdd();

        builder.Property(uc => uc.UserId)
            .HasColumnName("user_id")
            .IsRequired();

        builder.Property(uc => uc.CareerId)
            .HasColumnName("career_id")
            .IsRequired();

        // BaseEntity audit fields
        builder.Property(uc => uc.IsActive).HasColumnName("is_active");
        builder.Property(uc => uc.IsVisible).HasColumnName("is_visible");
        builder.Property(uc => uc.DisplayOrder).HasColumnName("display_order");
        builder.Property(uc => uc.CreatedBy).HasColumnName("created_by");
        builder.Property(uc => uc.UpdatedBy).HasColumnName("updated_by");
        builder.Property(uc => uc.DeletedBy).HasColumnName("deleted_by");
        builder.Property(uc => uc.CreatedAt).HasColumnName("created_at");
        builder.Property(uc => uc.UpdatedAt).HasColumnName("updated_at");
        builder.Property(uc => uc.DeletedAt).HasColumnName("deleted_at");

        builder.HasOne(uc => uc.User)
            .WithMany(u => u.UserCareers)
            .HasForeignKey(uc => uc.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(uc => uc.Career)
            .WithMany()
            .HasForeignKey(uc => uc.CareerId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(uc => new { uc.UserId, uc.CareerId })
            .IsUnique();
    }
}
