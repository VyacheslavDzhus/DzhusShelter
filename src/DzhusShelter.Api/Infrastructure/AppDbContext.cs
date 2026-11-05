using DzhusShelter.Api.Domain.BadHabits;
using DzhusShelter.Api.Domain.Birthdays;
using Microsoft.EntityFrameworkCore;

namespace DzhusShelter.Api.Infrastructure;

public sealed class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<HabitEntry> HabitEntries => Set<HabitEntry>();
    public DbSet<Birthday> Birthdays => Set<Birthday>();
    public DbSet<BirthdayNotification> BirthdayNotifications => Set<BirthdayNotification>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
