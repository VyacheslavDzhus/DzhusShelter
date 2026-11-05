namespace DzhusShelter.Api.Application.Birthdays.Commands;

public sealed record CreateBirthdayCommand(string Name, int Day, int Month, int? Year);
