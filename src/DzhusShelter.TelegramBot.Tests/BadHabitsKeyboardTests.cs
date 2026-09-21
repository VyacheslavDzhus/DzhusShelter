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
    public void SubTypesFor_Alcohol_ReturnsBeerWineSpirits()
    {
        var subTypes = BadHabitsKeyboard.SubTypesFor(HabitType.Alcohol);

        subTypes.Should().BeEquivalentTo([HabitSubType.Beer, HabitSubType.Wine, HabitSubType.Spirits]);
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
