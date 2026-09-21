using DzhusShelter.Api.Domain.BadHabits;
using FluentAssertions;
using Microsoft.Extensions.Time.Testing;

namespace DzhusShelter.Api.Tests.Domain;

public class HabitEntryTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 15, 12, 0, 0, TimeSpan.Zero);

    private static FakeTimeProvider CreateTimeProvider() => new(Now);

    [Fact]
    public void Create_WithValidCigaretteEntry_Succeeds()
    {
        var result = HabitEntry.Create(HabitType.Smoking, HabitSubType.Cigarette, Now.AddMinutes(-5), "after lunch", CreateTimeProvider());

        result.IsSuccess.Should().BeTrue();
        result.Value.HabitType.Should().Be(HabitType.Smoking);
        result.Value.SubType.Should().Be(HabitSubType.Cigarette);
        result.Value.Notes.Should().Be("after lunch");
    }

    [Fact]
    public void Create_WithOccurredAtInTheFuture_Fails()
    {
        var result = HabitEntry.Create(HabitType.Alcohol, HabitSubType.Beer, Now.AddMinutes(5), null, CreateTimeProvider());

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(HabitEntryErrors.OccurredAtInFuture);
    }

    [Fact]
    public void Create_WithSubTypeThatDoesNotBelongToHabitType_Fails()
    {
        var result = HabitEntry.Create(HabitType.Smoking, HabitSubType.Wine, Now, null, CreateTimeProvider());

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("HabitEntry.SubTypeMismatch");
    }
}
