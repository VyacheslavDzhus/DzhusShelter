using DzhusShelter.Api.Domain.Common;

namespace DzhusShelter.Api.Domain.BadHabits;

public static class HabitEntryErrors
{
    public static readonly Error OccurredAtInFuture =
        new("HabitEntry.OccurredAtInFuture", "The occurrence time cannot be in the future.");

    public static Error SubTypeMismatch(HabitType habitType, HabitSubType subType) =>
        new("HabitEntry.SubTypeMismatch", $"'{subType}' is not a valid sub-type for habit type '{habitType}'.");
}
