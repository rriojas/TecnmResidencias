using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace TecNM.Residency.Documents;

public class DocumentConfiguration : IEntityTypeConfiguration<Document>
{
    public void Configure(EntityTypeBuilder<Document> builder)
    {
        builder.ToTable("documents");

        builder.HasKey(d => d.Id);

        builder.Property(d => d.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(d => d.ProjectId).HasColumnName("project_id").IsRequired();
        builder.Property(d => d.DocumentType).HasColumnName("document_type").HasMaxLength(50).IsRequired();
        builder.Property(d => d.FileName).HasColumnName("file_name").HasMaxLength(255).IsRequired();
        builder.Property(d => d.FilePath).HasColumnName("file_path").HasMaxLength(500).IsRequired();
        builder.Property(d => d.FileSize).HasColumnName("file_size").IsRequired();
        builder.Property(d => d.ContentType).HasColumnName("content_type").HasMaxLength(100).HasDefaultValue("application/pdf").IsRequired();
        builder.Property(d => d.Status).HasColumnName("status").HasMaxLength(50).HasDefaultValue(DocumentStatus.Uploaded).IsRequired();
        builder.Property(d => d.RejectionReason).HasColumnName("rejection_reason");
        builder.Property(d => d.UploadedAt).HasColumnName("uploaded_at").HasDefaultValueSql("CURRENT_TIMESTAMP").IsRequired();

        builder.Property(d => d.IsActive).HasColumnName("is_active").HasDefaultValue(true).IsRequired();
        builder.Property(d => d.IsVisible).HasColumnName("is_visible").HasDefaultValue(true).IsRequired();
        builder.Property(d => d.DisplayOrder).HasColumnName("display_order").HasDefaultValue(0).IsRequired();
        builder.Property(d => d.CreatedBy).HasColumnName("created_by");
        builder.Property(d => d.UpdatedBy).HasColumnName("updated_by");
        builder.Property(d => d.DeletedBy).HasColumnName("deleted_by");
        builder.Property(d => d.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("CURRENT_TIMESTAMP").IsRequired();
        builder.Property(d => d.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("CURRENT_TIMESTAMP").IsRequired();
        builder.Property(d => d.DeletedAt).HasColumnName("deleted_at");

        builder.HasOne(d => d.Project)
            .WithMany()
            .HasForeignKey(d => d.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
