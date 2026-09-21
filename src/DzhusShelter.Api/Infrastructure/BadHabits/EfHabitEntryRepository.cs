using DzhusShelter.Api.Application.BadHabits;
using DzhusShelter.Api.Domain.BadHabits;
using Microsoft.EntityFrameworkCore;

namespace DzhusShelter.Api.Infrastructure.BadHabits;

public sealed class EfHabitEntryRepository : IHabitEntryRepository
{
    private readonly AppDbContext _dbContext;

    public EfHabitEntryRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddAsync(HabitEntry entry, CancellationToken cancellationToken)
    {
        await _dbContext.HabitEntries.AddAsync(entry, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<HabitEntry>> GetAsync(
        DateTimeOffset from,
        DateTimeOffset to,
        HabitType? habitType,
        HabitSubType? subType,
        CancellationToken cancellationToken)
    {
        IQueryable<HabitEntry> query = _dbContext.HabitEntries
            .Where(e => e.OccurredAt >= from && e.OccurredAt <= to);

        if (habitType is not null)
            query = query.Where(e => e.HabitType == habitType);

        if (subType is not null)
            query = query.Where(e => e.SubType == subType);

        return await query.OrderBy(e => e.OccurredAt).ToListAsync(cancellationToken);
    }
}
