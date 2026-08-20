using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace TecNM.Residency.Auth;

public class UserRoleAssignmentConfiguration : IEntityTypeConfiguration<UserRoleAssignment>
{
    public void Configure(EntityTypeBuilder<UserRoleAssignment> builder)
    {
        builder.ToTable("user_roles");

        builder.HasKey(ur => ur.Id);

        builder.Property(ur => ur.Id)
            .HasColumnName("id")
            .ValueGeneratedOnAdd();

        builder.Property(ur => ur.UserId)
            .HasColumnName("user_id")
            .IsRequired();

        builder.Property(ur => ur.RoleId)
            .HasColumnName("role_id")
            .IsRequired();

        // BaseEntity audit fields
        builder.Property(ur => ur.IsActive).HasColumnName("is_active");
        builder.Property(ur => ur.IsVisible).HasColumnName("is_visible");
        builder.Property(ur => ur.DisplayOrder).HasColumnName("display_order");
        builder.Property(ur => ur.CreatedBy).HasColumnName("created_by");
        builder.Property(ur => ur.UpdatedBy).HasColumnName("updated_by");
        builder.Property(ur => ur.DeletedBy).HasColumnName("deleted_by");
        builder.Property(ur => ur.CreatedAt).HasColumnName("created_at");
        builder.Property(ur => ur.UpdatedAt).HasColumnName("updated_at");
        builder.Property(ur => ur.DeletedAt).HasColumnName("deleted_at");

        builder.HasOne(ur => ur.User)
            .WithMany(u => u.UserRoles)
            .HasForeignKey(ur => ur.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(ur => ur.Role)
            .WithMany(r => r.UserRoles)
            .HasForeignKey(ur => ur.RoleId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(ur => new { ur.UserId, ur.RoleId }).IsUnique();
    }
}
