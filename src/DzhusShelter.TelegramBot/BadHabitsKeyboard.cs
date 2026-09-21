namespace DzhusShelter.TelegramBot;

public enum HabitType
{
    Smoking = 1,
    Alcohol = 2,
}

public enum HabitSubType
{
    Cigarette = 1,
    Vape = 2,
    Beer = 3,
    Wine = 4,
    Spirits = 5,
}

public static class BadHabitsKeyboard
{
    private const string HabitTypePrefix = "habit-type:";
    private const string SubTypePrefix = "sub-type:";

    /// <summary>
    /// Callback data for the single root-menu button ("🚫 Шкідливі звички") shown on /start.
    /// Tapping it reveals the habit-type buttons — a fixed value, not parameterized, since
    /// there's only one root menu today.
    /// </summary>
    public const string OpenMenuCallbackData = "menu:bad-habits";

    private static readonly Dictionary<HabitType, HabitSubType[]> SubTypesByHabitType = new()
    {
        [HabitType.Smoking] = [HabitSubType.Cigarette, HabitSubType.Vape],
        [HabitType.Alcohol] = [HabitSubType.Beer, HabitSubType.Wine, HabitSubType.Spirits],
    };

    public static IReadOnlyList<HabitSubType> SubTypesFor(HabitType habitType) => SubTypesByHabitType[habitType];

    public static string HabitTypeCallbackData(HabitType habitType) => $"{HabitTypePrefix}{(int)habitType}";

    public static string SubTypeCallbackData(HabitType habitType, HabitSubType subType) =>
        $"{SubTypePrefix}{(int)habitType}:{(int)subType}";

    public static HabitType? TryParseHabitType(string callbackData)
    {
        if (!callbackData.StartsWith(HabitTypePrefix, StringComparison.Ordinal))
            return null;

        var value = callbackData[HabitTypePrefix.Length..];
        return int.TryParse(value, out var raw) && Enum.IsDefined(typeof(HabitType), raw)
            ? (HabitType)raw
            : null;
    }

    public static (HabitType HabitType, HabitSubType SubType)? TryParseSubType(string callbackData)
    {
        if (!callbackData.StartsWith(SubTypePrefix, StringComparison.Ordinal))
            return null;

        var parts = callbackData[SubTypePrefix.Length..].Split(':');
        if (parts.Length != 2)
            return null;

        if (!int.TryParse(parts[0], out var habitTypeRaw) || !Enum.IsDefined(typeof(HabitType), habitTypeRaw))
            return null;

        if (!int.TryParse(parts[1], out var subTypeRaw) || !Enum.IsDefined(typeof(HabitSubType), subTypeRaw))
            return null;

        return ((HabitType)habitTypeRaw, (HabitSubType)subTypeRaw);
    }
}
