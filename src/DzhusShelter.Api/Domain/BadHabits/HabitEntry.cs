using DzhusShelter.Api.Domain.Common;

namespace DzhusShelter.Api.Domain.BadHabits;

public sealed class HabitEntry
{
    private static readonly Dictionary<HabitType, HabitSubType[]> ValidSubTypes = new()
    {
        [HabitType.Smoking] = [HabitSubType.Cigarette, HabitSubType.Vape],
        [HabitType.Alcohol] = [HabitSubType.Beer, HabitSubType.Wine, HabitSubType.Spirits],
    };

    private HabitEntry()
    {
        // EF Core materialization
    }

    private HabitEntry(Guid id, HabitType habitType, HabitSubType subType, DateTimeOffset occurredAt, string? notes)
    {
        Id = id;
        HabitType = habitType;
        SubType = subType;
        OccurredAt = occurredAt;
        Notes = notes;
    }

    public Guid Id { get; private set; }
    public HabitType HabitType { get; private set; }
    public HabitSubType SubType { get; private set; }
    public DateTimeOffset OccurredAt { get; private set; }
    public string? Notes { get; private set; }

    public static Result<HabitEntry> Create(
        HabitType habitType,
        HabitSubType subType,
        DateTimeOffset occurredAt,
        string? notes,
        TimeProvider timeProvider)
    {
        if (occurredAt > timeProvider.GetUtcNow())
            return Result.Failure<HabitEntry>(HabitEntryErrors.OccurredAtInFuture);

        if (!ValidSubTypes.TryGetValue(habitType, out var allowedSubTypes) || !allowedSubTypes.Contains(subType))
            return Result.Failure<HabitEntry>(HabitEntryErrors.SubTypeMismatch(habitType, subType));

        return Result.Success(new HabitEntry(Guid.NewGuid(), habitType, subType, occurredAt, notes));
    }
}
