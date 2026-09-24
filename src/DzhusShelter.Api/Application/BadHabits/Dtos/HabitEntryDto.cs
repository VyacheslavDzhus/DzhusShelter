using DzhusShelter.Api.Domain.BadHabits;

namespace DzhusShelter.Api.Application.BadHabits.Dtos;

public sealed record HabitEntryDto(Guid Id, HabitType HabitType, HabitSubType SubType, DateTimeOffset OccurredAt, string? Notes);
