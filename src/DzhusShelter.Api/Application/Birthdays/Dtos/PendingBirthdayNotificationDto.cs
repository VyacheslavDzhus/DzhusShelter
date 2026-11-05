using DzhusShelter.Api.Domain.Birthdays;

namespace DzhusShelter.Api.Application.Birthdays.Dtos;

public sealed record BirthdayPersonDto(string Name, int? Age);

public sealed record PendingBirthdayNotificationDto(
    BirthdayNotificationKind Kind,
    DateOnly ForDate,
    IReadOnlyList<BirthdayPersonDto> People);
