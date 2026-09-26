using DzhusShelter.UI.Services;

namespace DzhusShelter.UI.Components.Shared;

public static class HabitSubTypeCatalog
{
    private static readonly Dictionary<HabitType, HabitSubType[]> SubTypesByHabitType = new()
    {
        [HabitType.Smoking] = [HabitSubType.Cigarette, HabitSubType.Vape, HabitSubType.Iqos, HabitSubType.Hookah],
        [HabitType.Alcohol] = [HabitSubType.Beer, HabitSubType.Wine, HabitSubType.Vodka, HabitSubType.Whiskey, HabitSubType.Rum, HabitSubType.Gin, HabitSubType.Martini],
    };

    private static readonly Dictionary<HabitSubType, string> DisplayNames = new()
    {
        [HabitSubType.Cigarette] = "Цигарки",
        [HabitSubType.Vape] = "Вейп",
        [HabitSubType.Iqos] = "Айкос",
        [HabitSubType.Hookah] = "Кальян",
        [HabitSubType.Beer] = "Пиво",
        [HabitSubType.Wine] = "Вино",
        [HabitSubType.Vodka] = "Горілка",
        [HabitSubType.Whiskey] = "Віскі",
        [HabitSubType.Rum] = "Ром",
        [HabitSubType.Gin] = "Джин",
        [HabitSubType.Martini] = "Мартіні",
    };

    // Every icon shares a 16x16 viewBox so callers can size them uniformly.
    // Flat rects only (no strokes/gradients) for a consistent chunky pixel-art look.
    private static readonly Dictionary<HabitSubType, string> Icons = new()
    {
        [HabitSubType.Cigarette] =
            "<svg viewBox=\"0 0 16 16\" width=\"14\" height=\"14\">" +
            "<rect x=\"1\" y=\"7\" width=\"1\" height=\"2\" fill=\"#cccccc\"/>" +
            "<rect x=\"2\" y=\"7\" width=\"10\" height=\"2\" fill=\"#ffffff\"/>" +
            "<rect x=\"12\" y=\"7\" width=\"2\" height=\"2\" fill=\"#ff5533\"/>" +
            "<rect x=\"13\" y=\"3\" width=\"1\" height=\"1\" fill=\"#dddddd\"/>" +
            "<rect x=\"14\" y=\"2\" width=\"1\" height=\"1\" fill=\"#dddddd\"/>" +
            "</svg>",
        [HabitSubType.Vape] =
            "<svg viewBox=\"0 0 16 16\" width=\"14\" height=\"14\">" +
            "<rect x=\"6\" y=\"3\" width=\"4\" height=\"10\" fill=\"#4a6b8a\"/>" +
            "<rect x=\"7\" y=\"1\" width=\"2\" height=\"2\" fill=\"#333333\"/>" +
            "<rect x=\"7\" y=\"11\" width=\"2\" height=\"1\" fill=\"#ffaa33\"/>" +
            "</svg>",
        [HabitSubType.Iqos] =
            "<svg viewBox=\"0 0 16 16\" width=\"14\" height=\"14\">" +
            "<rect x=\"6\" y=\"2\" width=\"4\" height=\"12\" fill=\"#333333\"/>" +
            "<rect x=\"6\" y=\"9\" width=\"4\" height=\"2\" fill=\"#c0c0c0\"/>" +
            "</svg>",
        [HabitSubType.Hookah] =
            "<svg viewBox=\"0 0 16 16\" width=\"14\" height=\"14\">" +
            "<rect x=\"6\" y=\"1\" width=\"4\" height=\"2\" fill=\"#333333\"/>" +
            "<rect x=\"7\" y=\"3\" width=\"2\" height=\"6\" fill=\"#c0c0c0\"/>" +
            "<rect x=\"6\" y=\"8\" width=\"4\" height=\"1\" fill=\"#1f8080\"/>" +
            "<rect x=\"5\" y=\"9\" width=\"6\" height=\"5\" fill=\"#1f8080\"/>" +
            "</svg>",
        [HabitSubType.Beer] =
            "<svg viewBox=\"0 0 16 16\" width=\"14\" height=\"14\">" +
            "<rect x=\"4\" y=\"3\" width=\"7\" height=\"2\" fill=\"#ffffff\"/>" +
            "<rect x=\"4\" y=\"5\" width=\"7\" height=\"9\" fill=\"#f2a900\"/>" +
            "<rect x=\"11\" y=\"6\" width=\"2\" height=\"6\" fill=\"#f2a900\"/>" +
            "</svg>",
        [HabitSubType.Wine] =
            "<svg viewBox=\"0 0 16 16\" width=\"14\" height=\"14\">" +
            "<rect x=\"5\" y=\"3\" width=\"6\" height=\"5\" fill=\"#7b1030\"/>" +
            "<rect x=\"7\" y=\"8\" width=\"2\" height=\"4\" fill=\"#cfcfcf\"/>" +
            "<rect x=\"5\" y=\"12\" width=\"6\" height=\"2\" fill=\"#cfcfcf\"/>" +
            "</svg>",
        [HabitSubType.Vodka] =
            "<svg viewBox=\"0 0 16 16\" width=\"14\" height=\"14\">" +
            "<rect x=\"6\" y=\"1\" width=\"4\" height=\"2\" fill=\"#000000\"/>" +
            "<rect x=\"7\" y=\"3\" width=\"2\" height=\"3\" fill=\"#bfe6ff\"/>" +
            "<rect x=\"5\" y=\"6\" width=\"6\" height=\"8\" fill=\"#bfe6ff\"/>" +
            "</svg>",
        [HabitSubType.Whiskey] =
            "<svg viewBox=\"0 0 16 16\" width=\"14\" height=\"14\">" +
            "<rect x=\"5\" y=\"5\" width=\"6\" height=\"3\" fill=\"#cccccc\"/>" +
            "<rect x=\"5\" y=\"8\" width=\"6\" height=\"6\" fill=\"#b06a1e\"/>" +
            "</svg>",
        [HabitSubType.Rum] =
            "<svg viewBox=\"0 0 16 16\" width=\"14\" height=\"14\">" +
            "<rect x=\"6\" y=\"1\" width=\"4\" height=\"2\" fill=\"#000000\"/>" +
            "<rect x=\"7\" y=\"3\" width=\"2\" height=\"3\" fill=\"#4a2a10\"/>" +
            "<rect x=\"5\" y=\"6\" width=\"6\" height=\"8\" fill=\"#4a2a10\"/>" +
            "</svg>",
        [HabitSubType.Gin] =
            "<svg viewBox=\"0 0 16 16\" width=\"14\" height=\"14\">" +
            "<rect x=\"6\" y=\"1\" width=\"4\" height=\"2\" fill=\"#000000\"/>" +
            "<rect x=\"7\" y=\"3\" width=\"2\" height=\"3\" fill=\"#2f6e3f\"/>" +
            "<rect x=\"5\" y=\"6\" width=\"6\" height=\"8\" fill=\"#2f6e3f\"/>" +
            "</svg>",
        [HabitSubType.Martini] =
            "<svg viewBox=\"0 0 16 16\" width=\"14\" height=\"14\">" +
            "<rect x=\"3\" y=\"3\" width=\"10\" height=\"2\" fill=\"#d8d8d8\"/>" +
            "<rect x=\"5\" y=\"5\" width=\"6\" height=\"2\" fill=\"#d8d8d8\"/>" +
            "<rect x=\"7\" y=\"5\" width=\"2\" height=\"2\" fill=\"#4a7c2f\"/>" +
            "<rect x=\"7\" y=\"7\" width=\"2\" height=\"2\" fill=\"#d8d8d8\"/>" +
            "<rect x=\"7\" y=\"9\" width=\"2\" height=\"4\" fill=\"#d8d8d8\"/>" +
            "<rect x=\"5\" y=\"13\" width=\"6\" height=\"1\" fill=\"#d8d8d8\"/>" +
            "</svg>",
    };

    public static IReadOnlyList<HabitSubType> SubTypesFor(HabitType habitType) => SubTypesByHabitType[habitType];

    public static string DisplayName(HabitSubType subType) => DisplayNames[subType];

    public static string IconSvg(HabitSubType subType) => Icons[subType];
}
