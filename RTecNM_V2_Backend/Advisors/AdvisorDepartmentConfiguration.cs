using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace TecNM.Residency.Advisors;

public class AdvisorDepartmentConfiguration : IEntityTypeConfiguration<AdvisorDepartment>
{
    public void Configure(EntityTypeBuilder<AdvisorDepartment> builder)
    {
        builder.ToTable("advisor_departments");

        builder.HasKey(ad => ad.Id);

        builder.Property(ad => ad.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(ad => ad.AdvisorId).HasColumnName("advisor_id").IsRequired();
        builder.Property(ad => ad.DepartmentId).HasColumnName("department_id").IsRequired();

        builder.Property(ad => ad.IsActive).HasColumnName("is_active").HasDefaultValue(true);
        builder.Property(ad => ad.IsVisible).HasColumnName("is_visible").HasDefaultValue(true);
        builder.Property(ad => ad.DisplayOrder).HasColumnName("display_order").HasDefaultValue(0);
        builder.Property(ad => ad.CreatedBy).HasColumnName("created_by");
        builder.Property(ad => ad.UpdatedBy).HasColumnName("updated_by");
        builder.Property(ad => ad.DeletedBy).HasColumnName("deleted_by");
        builder.Property(ad => ad.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("CURRENT_TIMESTAMP");
        builder.Property(ad => ad.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("CURRENT_TIMESTAMP");
        builder.Property(ad => ad.DeletedAt).HasColumnName("deleted_at");

        builder.HasOne(ad => ad.Advisor)
            .WithMany()
            .HasForeignKey(ad => ad.AdvisorId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
