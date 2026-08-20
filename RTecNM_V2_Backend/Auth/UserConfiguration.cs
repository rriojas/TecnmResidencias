using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TecNM.Residency.Common;

namespace TecNM.Residency.Auth;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");

        builder.HasKey(u => u.Id);

        builder.Property(u => u.Id)
            .HasColumnName("id")
            .ValueGeneratedOnAdd();

        builder.Property(u => u.Email)
            .HasColumnName("email")
            .HasMaxLength(150)
            .IsRequired();

        builder.HasIndex(u => u.Email)
            .IsUnique();

        builder.Property(u => u.PasswordHash)
            .HasColumnName("password_hash")
            .HasMaxLength(255)
            .IsRequired();

        var roleConverter = new Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter<UserRole, string>(
            v => RoleToString(v),
            v => StringToRole(v)
        );

        builder.Property(u => u.Role)
            .HasColumnName("role")
            .HasConversion(roleConverter)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(u => u.AvatarPath)
            .HasColumnName("avatar_path")
            .HasMaxLength(255);

        builder.Property(u => u.IsAdmin)
            .HasColumnName("is_admin")
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(u => u.IsActive)
            .HasColumnName("is_active")
            .HasDefaultValue(true)
            .IsRequired();

        builder.Property(u => u.IsVisible)
            .HasColumnName("is_visible")
            .HasDefaultValue(true)
            .IsRequired();

        builder.Property(u => u.DisplayOrder)
            .HasColumnName("display_order")
            .HasDefaultValue(0)
            .IsRequired();

        builder.Property(u => u.CreatedBy)
            .HasColumnName("created_by");

        builder.Property(u => u.UpdatedBy)
            .HasColumnName("updated_by");

        builder.Property(u => u.DeletedBy)
            .HasColumnName("deleted_by");

        builder.Property(u => u.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(u => u.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();

        builder.Property(u => u.DeletedAt)
            .HasColumnName("deleted_at");
    }

    private static string RoleToString(UserRole role) => role switch
    {
        UserRole.Student => "student",
        UserRole.Advisor => "advisor",
        UserRole.Academic => "academico",
        UserRole.Vinculacion => "vinculacion",
        UserRole.Director => "director",
        UserRole.Admin => "admin",
        _ => "student"
    };

    private static UserRole StringToRole(string str) => (str ?? "").ToLowerInvariant() switch
    {
        "student" or "estudiante" => UserRole.Student,
        "advisor" or "asesor" => UserRole.Advisor,
        "academico" or "academic" or "department_head" or "departmenthead" or "jefatura" => UserRole.Academic,
        "vinculacion" => UserRole.Vinculacion,
        "director" => UserRole.Director,
        "admin" or "administrador" or "superadmin" => UserRole.Admin,
        _ => UserRole.Student
    };
}
