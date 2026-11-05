using DzhusShelter.Api.Domain.Birthdays;

namespace DzhusShelter.Api.Application.Birthdays.Commands;

public sealed record MarkBirthdayNotificationSentCommand(BirthdayNotificationKind Kind, DateOnly ForDate);
