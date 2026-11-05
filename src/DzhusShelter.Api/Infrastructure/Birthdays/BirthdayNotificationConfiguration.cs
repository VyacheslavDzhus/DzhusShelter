using DzhusShelter.Api.Domain.Birthdays;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DzhusShelter.Api.Infrastructure.Birthdays;

public sealed class BirthdayNotificationConfiguration : IEntityTypeConfiguration<BirthdayNotification>
{
    public void Configure(EntityTypeBuilder<BirthdayNotification> builder)
    {
        builder.ToTable("BirthdayNotifications");
        builder.HasKey(n => n.Id);
        builder.Property(n => n.Kind).HasConversion<string>().HasMaxLength(50).IsRequired();
        builder.Property(n => n.ForDate).HasColumnType("date").IsRequired();
        builder.Property(n => n.SentAt).IsRequired();
        // Makes "mark as sent" idempotent: one reminder per kind per Kyiv date.
        builder.HasIndex(n => new { n.Kind, n.ForDate }).IsUnique();
    }
}
