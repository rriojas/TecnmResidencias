using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TecNM.Residency.Common;

namespace TecNM.Residency.Careers;

public class CareerConfiguration : IEntityTypeConfiguration<Career>
{
    public void Configure(EntityTypeBuilder<Career> builder)
    {
        builder.ToTable("careers");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(c => c.Code).HasColumnName("code").HasMaxLength(50).IsRequired();
        builder.Property(c => c.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
        builder.Property(c => c.Acronym).HasColumnName("acronym").HasMaxLength(20).IsRequired();
        builder.Property(c => c.DepartmentId).HasColumnName("department_id");

        builder.Property(c => c.IsActive).HasColumnName("is_active").HasDefaultValue(true);
        builder.Property(c => c.IsVisible).HasColumnName("is_visible").HasDefaultValue(true);
        builder.Property(c => c.DisplayOrder).HasColumnName("display_order").HasDefaultValue(0);
        builder.Property(c => c.CreatedBy).HasColumnName("created_by");
        builder.Property(c => c.UpdatedBy).HasColumnName("updated_by");
        builder.Property(c => c.DeletedBy).HasColumnName("deleted_by");
        builder.Property(c => c.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("CURRENT_TIMESTAMP");
        builder.Property(c => c.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("CURRENT_TIMESTAMP");
        builder.Property(c => c.DeletedAt).HasColumnName("deleted_at");
    }
}
