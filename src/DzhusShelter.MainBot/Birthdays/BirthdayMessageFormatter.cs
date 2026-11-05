namespace DzhusShelter.MainBot.Birthdays;

public static class BirthdayMessageFormatter
{
    public static string Format(PendingBirthdayNotification n) =>
        n.Kind == BirthdayNotificationKind.Today ? FormatToday(n.People) : FormatTomorrow(n.People);

    public static string Years(int age)
    {
        var lastTwo = age % 100;
        var last = age % 10;
        var word = lastTwo is >= 11 and <= 14 ? "років"
            : last == 1 ? "рік"
            : last is >= 2 and <= 4 ? "роки"
            : "років";
        return $"{age} {word}";
    }

    private static string FormatToday(IReadOnlyList<BirthdayPerson> people)
    {
        if (people.Count == 1)
        {
            var p = people[0];
            return p.Age is { } age
                ? $"🎂 Привіт! Сьогодні у {p.Name} день народження — {Years(age)}! 🥳"
                : $"🎂 Привіт! Сьогодні у {p.Name} день народження! 🥳";
        }

        var lines = people.Select(p => $"• {Describe(p)} 🥳");
        return "🎂 Привіт! Сьогодні день народження у:\n" + string.Join("\n", lines);
    }

    private static string FormatTomorrow(IReadOnlyList<BirthdayPerson> people)
    {
        if (people.Count == 1)
        {
            var p = people[0];
            return p.Age is { } age
                ? $"⏰ Привіт! Завтра у {p.Name} день народження — {Years(age)} 🎁"
                : $"⏰ Привіт! Завтра у {p.Name} день народження 🎁";
        }

        var lines = people.Select(p => $"• {Describe(p)}");
        return "⏰ Привіт! Завтра день народження у:\n" + string.Join("\n", lines) + "\n🎁 Не забудь привітати!";
    }

    private static string Describe(BirthdayPerson p) =>
        p.Age is { } age ? $"{p.Name} — {Years(age)}" : p.Name;
}
