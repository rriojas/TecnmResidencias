using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace TecNM.Residency.Notifications;

public class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("notifications");

        builder.HasKey(n => n.Id);
        builder.Property(n => n.Id).HasColumnName("id");
        builder.Property(n => n.Title).HasColumnName("title").HasMaxLength(200).IsRequired();
        builder.Property(n => n.Description).HasColumnName("description").IsRequired();
        builder.Property(n => n.ExpiresAt).HasColumnName("expires_at").IsRequired();
        builder.Property(n => n.TargetRoles).HasColumnName("target_roles").HasMaxLength(255).IsRequired();
        builder.Property(n => n.CareerId).HasColumnName("career_id");
        builder.Property(n => n.ResidencyModality).HasColumnName("residency_modality").HasMaxLength(100);
        builder.Property(n => n.SenderId).HasColumnName("sender_id").IsRequired();
        builder.Property(n => n.Type).HasColumnName("type").HasMaxLength(50).IsRequired().HasDefaultValue("manual");

        builder.Property(n => n.IsActive).HasColumnName("is_active").HasDefaultValue(true);
        builder.Property(n => n.IsVisible).HasColumnName("is_visible").HasDefaultValue(true);
        builder.Property(n => n.DisplayOrder).HasColumnName("display_order").HasDefaultValue(0);
        builder.Property(n => n.CreatedBy).HasColumnName("created_by");
        builder.Property(n => n.UpdatedBy).HasColumnName("updated_by");
        builder.Property(n => n.DeletedBy).HasColumnName("deleted_by");
        builder.Property(n => n.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("CURRENT_TIMESTAMP");
        builder.Property(n => n.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("CURRENT_TIMESTAMP");
        builder.Property(n => n.DeletedAt).HasColumnName("deleted_at");

        builder.Property(n => n.TargetUserId).HasColumnName("target_user_id");

        builder.HasOne(n => n.Sender)
               .WithMany()
               .HasForeignKey(n => n.SenderId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(n => n.TargetUser)
               .WithMany()
               .HasForeignKey(n => n.TargetUserId)
               .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(n => n.Career)
               .WithMany()
               .HasForeignKey(n => n.CareerId)
               .OnDelete(DeleteBehavior.SetNull);

        builder.HasMany(n => n.Reads)
               .WithOne(r => r.Notification)
               .HasForeignKey(r => r.NotificationId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(n => n.ExpiresAt);
        builder.HasIndex(n => n.Type);
    }
}

public class UserNotificationReadConfiguration : IEntityTypeConfiguration<UserNotificationRead>
{
    public void Configure(EntityTypeBuilder<UserNotificationRead> builder)
    {
        builder.ToTable("user_notification_reads");

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).HasColumnName("id");
        builder.Property(r => r.NotificationId).HasColumnName("notification_id").IsRequired();
        builder.Property(r => r.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(r => r.ReadAt).HasColumnName("read_at").HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.HasOne(r => r.User)
               .WithMany()
               .HasForeignKey(r => r.UserId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(r => new { r.UserId, r.NotificationId }).IsUnique();
    }
}
