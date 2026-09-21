using DzhusShelter.Api.Domain.BadHabits;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DzhusShelter.Api.Infrastructure.BadHabits;

public sealed class HabitEntryConfiguration : IEntityTypeConfiguration<HabitEntry>
{
    public void Configure(EntityTypeBuilder<HabitEntry> builder)
    {
        builder.ToTable("HabitEntries");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.HabitType).HasConversion<string>().HasMaxLength(50).IsRequired();
        builder.Property(e => e.SubType).HasConversion<string>().HasMaxLength(50).IsRequired();
        builder.Property(e => e.OccurredAt).IsRequired();
        builder.Property(e => e.Notes).HasMaxLength(500);
        builder.HasIndex(e => e.OccurredAt);
    }
}
