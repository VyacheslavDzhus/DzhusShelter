using DzhusShelter.Api.Domain.Birthdays;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DzhusShelter.Api.Infrastructure.Birthdays;

public sealed class BirthdayConfiguration : IEntityTypeConfiguration<Birthday>
{
    public void Configure(EntityTypeBuilder<Birthday> builder)
    {
        builder.ToTable("Birthdays");
        builder.HasKey(b => b.Id);
        builder.Property(b => b.Name).HasMaxLength(100).IsRequired();
        builder.Property(b => b.Day).IsRequired();
        builder.Property(b => b.Month).IsRequired();
        builder.Property(b => b.Year);
        builder.Property(b => b.CreatedAt).IsRequired();
    }
}
