namespace DzhusShelter.Api.Application.Birthdays.Dtos;

public sealed record BirthdayDto(
    Guid Id,
    string Name,
    int Day,
    int Month,
    int? Year,
    DateOnly NextOccurrence,
    int DaysUntil,
    int? TurningAge);
