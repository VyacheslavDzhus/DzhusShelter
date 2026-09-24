using DzhusShelter.Api.Domain.BadHabits;

namespace DzhusShelter.Api.Application.BadHabits.Queries;

public sealed record GetHabitEntriesQuery(DateTimeOffset From, DateTimeOffset To, HabitType? HabitType, HabitSubType? SubType);
