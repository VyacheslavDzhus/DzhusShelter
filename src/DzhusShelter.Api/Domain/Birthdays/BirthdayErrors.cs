using DzhusShelter.Api.Domain.Common;

namespace DzhusShelter.Api.Domain.Birthdays;

public static class BirthdayErrors
{
    public const int MaxNameLength = 100;
    public const int MinYear = 1900;

    public static readonly Error NameRequired = new("Birthday.NameRequired", "Ім'я не може бути порожнім.");

    public static readonly Error NameTooLong =
        new("Birthday.NameTooLong", $"Ім'я не може бути довшим за {MaxNameLength} символів.");

    public static readonly Error InvalidDate = new("Birthday.InvalidDate", "Такої дати не існує.");

    public static readonly Error YearTooEarly = new("Birthday.YearTooEarly", $"Рік не може бути раніше {MinYear}.");

    public static readonly Error DateInFuture = new("Birthday.DateInFuture", "Дата народження не може бути в майбутньому.");

    // The ".NotFound" suffix is what BirthdaysController maps to 404 — keep it.
    public static Error NotFound(Guid id) => new("Birthday.NotFound", $"Запис {id} не знайдено.");
}
