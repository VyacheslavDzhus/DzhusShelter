using DzhusShelter.Api.Domain.Common;

namespace DzhusShelter.Api.Domain.Birthdays;

/// <summary>
/// A person's birthday. The year is optional (often unknown, can be added later).
/// Methods that depend on "today" take it as a parameter — the Kyiv date comes from
/// IBirthdayClock in the Application layer, the domain never reads a clock.
/// </summary>
public sealed class Birthday
{
    // Any leap year: lets Feb 29 pass validation when the birth year is unknown.
    private const int LeapYearForUnknownYear = 2000;

    private Birthday()
    {
        // EF Core materialization
        Name = string.Empty;
    }

    private Birthday(Guid id, string name, int day, int month, int? year, DateTimeOffset createdAt)
    {
        Id = id;
        Name = name;
        Day = day;
        Month = month;
        Year = year;
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }
    public string Name { get; private set; }
    public int Day { get; private set; }
    public int Month { get; private set; }
    public int? Year { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public static Result<Birthday> Create(string name, int day, int month, int? year, DateOnly today, DateTimeOffset createdAt)
    {
        var trimmedName = name?.Trim() ?? string.Empty;
        var error = Validate(trimmedName, day, month, year, today);
        return error is null
            ? Result.Success(new Birthday(Guid.NewGuid(), trimmedName, day, month, year, createdAt))
            : Result.Failure<Birthday>(error);
    }

    public Result Update(string name, int day, int month, int? year, DateOnly today)
    {
        var trimmedName = name?.Trim() ?? string.Empty;
        var error = Validate(trimmedName, day, month, year, today);
        if (error is not null)
            return Result.Failure(error);

        Name = trimmedName;
        Day = day;
        Month = month;
        Year = year;
        return Result.Success();
    }

    /// <summary>Today if the birthday is today, otherwise its next date. Feb 29 falls on Feb 28 in non-leap years.</summary>
    public DateOnly NextOccurrence(DateOnly today)
    {
        var thisYear = OccurrenceIn(today.Year);
        return thisYear >= today ? thisYear : OccurrenceIn(today.Year + 1);
    }

    public int? AgeOn(DateOnly occurrence) => Year is null ? null : occurrence.Year - Year.Value;

    private DateOnly OccurrenceIn(int year)
    {
        var day = Month == 2 && Day == 29 && !DateTime.IsLeapYear(year) ? 28 : Day;
        return new DateOnly(year, Month, day);
    }

    private static Error? Validate(string trimmedName, int day, int month, int? year, DateOnly today)
    {
        if (trimmedName.Length == 0)
            return BirthdayErrors.NameRequired;
        if (trimmedName.Length > BirthdayErrors.MaxNameLength)
            return BirthdayErrors.NameTooLong;
        // Year range checks run first: DateTime.DaysInMonth and DateOnly throw outside 1..9999,
        // and any year above today's is in the future anyway.
        if (year is not null && year < BirthdayErrors.MinYear)
            return BirthdayErrors.YearTooEarly;
        if (year is not null && year > today.Year)
            return BirthdayErrors.DateInFuture;
        if (month is < 1 or > 12 || day < 1 || day > DateTime.DaysInMonth(year ?? LeapYearForUnknownYear, month))
            return BirthdayErrors.InvalidDate;
        if (year is not null && new DateOnly(year.Value, month, day) > today)
            return BirthdayErrors.DateInFuture;
        return null;
    }
}
