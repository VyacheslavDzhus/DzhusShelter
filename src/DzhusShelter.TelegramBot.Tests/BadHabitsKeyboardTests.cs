using DzhusShelter.TelegramBot;
using FluentAssertions;

namespace DzhusShelter.TelegramBot.Tests;

public class BadHabitsKeyboardTests
{
    [Fact]
    public void TopLevelCallbackData_ForSmoking_ParsesBackToHabitType()
    {
        var callbackData = BadHabitsKeyboard.HabitTypeCallbackData(HabitType.Smoking);

        var parsed = BadHabitsKeyboard.TryParseHabitType(callbackData);

        parsed.Should().Be(HabitType.Smoking);
    }

    [Fact]
    public void SubTypeCallbackData_ForCigarette_ParsesBackToHabitTypeAndSubType()
    {
        var callbackData = BadHabitsKeyboard.SubTypeCallbackData(HabitType.Smoking, HabitSubType.Cigarette);

        var parsed = BadHabitsKeyboard.TryParseSubType(callbackData);

        parsed.Should().Be((HabitType.Smoking, HabitSubType.Cigarette));
    }

    [Fact]
    public void SubTypesFor_Alcohol_ReturnsAllAlcoholSubTypes()
    {
        var subTypes = BadHabitsKeyboard.SubTypesFor(HabitType.Alcohol);

        subTypes.Should().BeEquivalentTo([
            HabitSubType.Beer, HabitSubType.Wine, HabitSubType.Vodka,
            HabitSubType.Whiskey, HabitSubType.Rum, HabitSubType.Gin, HabitSubType.Martini,
        ]);
    }

    [Fact]
    public void SubTypesFor_Smoking_ReturnsAllSmokingSubTypes()
    {
        var subTypes = BadHabitsKeyboard.SubTypesFor(HabitType.Smoking);

        subTypes.Should().BeEquivalentTo([
            HabitSubType.Cigarette, HabitSubType.Vape, HabitSubType.Iqos, HabitSubType.Hookah,
        ]);
    }

    [Theory]
    [InlineData(HabitSubType.Cigarette, "Цигарки")]
    [InlineData(HabitSubType.Vape, "Вейп")]
    [InlineData(HabitSubType.Iqos, "Айкос")]
    [InlineData(HabitSubType.Hookah, "Кальян")]
    [InlineData(HabitSubType.Beer, "Пиво")]
    [InlineData(HabitSubType.Wine, "Вино")]
    [InlineData(HabitSubType.Vodka, "Горілка")]
    [InlineData(HabitSubType.Whiskey, "Віскі")]
    [InlineData(HabitSubType.Rum, "Ром")]
    [InlineData(HabitSubType.Gin, "Джин")]
    [InlineData(HabitSubType.Martini, "Мартіні")]
    public void DisplayName_ReturnsUkrainianLabel(HabitSubType subType, string expectedLabel)
    {
        BadHabitsKeyboard.DisplayName(subType).Should().Be(expectedLabel);
    }

    [Fact]
    public void TryParseHabitType_WithUnrelatedCallbackData_ReturnsNull()
    {
        var parsed = BadHabitsKeyboard.TryParseHabitType("something-else");

        parsed.Should().BeNull();
    }

    [Fact]
    public void OpenMenuCallbackData_DoesNotCollideWithHabitTypeOrSubTypeParsing()
    {
        BadHabitsKeyboard.TryParseHabitType(BadHabitsKeyboard.OpenMenuCallbackData).Should().BeNull();
        BadHabitsKeyboard.TryParseSubType(BadHabitsKeyboard.OpenMenuCallbackData).Should().BeNull();
    }
}
