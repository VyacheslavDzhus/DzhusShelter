namespace DzhusShelter.Api.Application.Birthdays.Commands;

public sealed record UpdateBirthdayCommand(Guid Id, string Name, int Day, int Month, int? Year);
