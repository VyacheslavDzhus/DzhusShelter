using DzhusShelter.Api.Domain.BadHabits;

namespace DzhusShelter.Api.Application.BadHabits;

public sealed record LogHabitEntryCommand(HabitType HabitType, HabitSubType SubType, DateTimeOffset OccurredAt, string? Notes);
