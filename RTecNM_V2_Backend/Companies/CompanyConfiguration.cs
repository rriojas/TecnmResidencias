using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace TecNM.Residency.Companies;

public class CompanyConfiguration : IEntityTypeConfiguration<Company>
{
    public void Configure(EntityTypeBuilder<Company> builder)
    {
        builder.ToTable("companies");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Id)
            .HasColumnName("id")
            .ValueGeneratedOnAdd();

        builder.Property(c => c.Name)
            .HasColumnName("name")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(c => c.Rfc)
            .HasColumnName("rfc")
            .HasMaxLength(13)
            .IsRequired(false);

        builder.HasIndex(c => c.Rfc)
            .HasDatabaseName("ix_companies_rfc");

        builder.Property(c => c.Sector)
            .HasColumnName("sector")
            .HasMaxLength(100);

        builder.Property(c => c.Address)
            .HasColumnName("address")
            .HasMaxLength(300);

        builder.Property(c => c.ContactName)
            .HasColumnName("contact_name")
            .HasMaxLength(150)
            .IsRequired();

        builder.Property(c => c.ContactEmail)
            .HasColumnName("contact_email")
            .HasMaxLength(150)
            .IsRequired();

        builder.Property(c => c.ContactPhone)
            .HasColumnName("contact_phone")
            .HasMaxLength(30);

        // BaseEntity fields
        builder.Property(c => c.IsActive).HasColumnName("is_active");
        builder.Property(c => c.IsVisible).HasColumnName("is_visible");
        builder.Property(c => c.DisplayOrder).HasColumnName("display_order");
        builder.Property(c => c.CreatedBy).HasColumnName("created_by");
        builder.Property(c => c.UpdatedBy).HasColumnName("updated_by");
        builder.Property(c => c.DeletedBy).HasColumnName("deleted_by");
        builder.Property(c => c.CreatedAt).HasColumnName("created_at");
        builder.Property(c => c.UpdatedAt).HasColumnName("updated_at");
        builder.Property(c => c.DeletedAt).HasColumnName("deleted_at");
    }
}
