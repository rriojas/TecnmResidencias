using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace TecNM.Residency.Companies;

public class CompanyAgreementConfiguration : IEntityTypeConfiguration<CompanyAgreement>
{
    public void Configure(EntityTypeBuilder<CompanyAgreement> builder)
    {
        builder.ToTable("company_agreements");

        builder.HasKey(ca => ca.Id);

        builder.Property(ca => ca.Id)
            .HasColumnName("id")
            .ValueGeneratedOnAdd();

        builder.Property(ca => ca.CompanyId)
            .HasColumnName("company_id")
            .IsRequired();

        builder.Property(ca => ca.ArchiveId)
            .HasColumnName("archive_id")
            .HasMaxLength(50);

        builder.Property(ca => ca.Status)
            .HasColumnName("status")
            .HasMaxLength(50)
            .HasDefaultValue("VIGENTE")
            .IsRequired();

        builder.Property(ca => ca.PitCode)
            .HasColumnName("pit_code")
            .HasMaxLength(20);

        builder.Property(ca => ca.CiaType)
            .HasColumnName("cia_type")
            .HasMaxLength(100);

        builder.Property(ca => ca.AgreementScope)
            .HasColumnName("agreement_scope")
            .HasMaxLength(50);

        builder.Property(ca => ca.Sector)
            .HasColumnName("sector")
            .HasMaxLength(50);

        builder.Property(ca => ca.BusinessLine)
            .HasColumnName("business_line")
            .HasMaxLength(150);

        builder.Property(ca => ca.CompanySize)
            .HasColumnName("company_size")
            .HasMaxLength(50);

        builder.Property(ca => ca.GeographicScope)
            .HasColumnName("geographic_scope")
            .HasMaxLength(50);

        builder.Property(ca => ca.Notes)
            .HasColumnName("notes");

        builder.HasIndex(ca => ca.Status)
            .HasDatabaseName("ix_company_agreements_status");

        builder.HasIndex(ca => ca.CompanyId)
            .IsUnique()
            .HasDatabaseName("ix_company_agreements_company_id");

        // BaseEntity fields
        builder.Property(ca => ca.IsActive).HasColumnName("is_active");
        builder.Property(ca => ca.IsVisible).HasColumnName("is_visible");
        builder.Property(ca => ca.DisplayOrder).HasColumnName("display_order");
        builder.Property(ca => ca.CreatedBy).HasColumnName("created_by");
        builder.Property(ca => ca.UpdatedBy).HasColumnName("updated_by");
        builder.Property(ca => ca.DeletedBy).HasColumnName("deleted_by");
        builder.Property(ca => ca.CreatedAt).HasColumnName("created_at");
        builder.Property(ca => ca.UpdatedAt).HasColumnName("updated_at");
        builder.Property(ca => ca.DeletedAt).HasColumnName("deleted_at");

        builder.HasOne(ca => ca.Company)
            .WithOne(c => c.Agreement)
            .HasForeignKey<CompanyAgreement>(ca => ca.CompanyId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
