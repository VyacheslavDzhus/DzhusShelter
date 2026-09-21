using DzhusShelter.Api.Domain.BadHabits;

namespace DzhusShelter.Api.Application.BadHabits;

public interface IHabitEntryRepository
{
    Task AddAsync(HabitEntry entry, CancellationToken cancellationToken);

    Task<IReadOnlyList<HabitEntry>> GetAsync(
        DateTimeOffset from,
        DateTimeOffset to,
        HabitType? habitType,
        HabitSubType? subType,
        CancellationToken cancellationToken);
}
