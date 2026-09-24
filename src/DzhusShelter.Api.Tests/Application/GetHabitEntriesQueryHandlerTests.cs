using DzhusShelter.Api.Application.BadHabits;
using DzhusShelter.Api.Application.BadHabits.Dtos;
using DzhusShelter.Api.Application.BadHabits.Queries;
using DzhusShelter.Api.Domain.BadHabits;
using FluentAssertions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace DzhusShelter.Api.Tests.Application;

public class GetHabitEntriesQueryHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 15, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Handle_WithEntriesInRange_ReturnsThemAsDtos()
    {
        var timeProvider = new FakeTimeProvider(Now);
        var entry = HabitEntry.Create(HabitType.Smoking, HabitSubType.Cigarette, Now.AddHours(-1), null, timeProvider).Value;
        var repository = Substitute.For<IHabitEntryRepository>();
        repository
            .GetAsync(Now.AddDays(-1), Now, HabitType.Smoking, null, Arg.Any<CancellationToken>())
            .Returns(new List<HabitEntry> { entry });
        var handler = new GetHabitEntriesQueryHandler(repository, new GetHabitEntriesQueryValidator());
        var query = new GetHabitEntriesQuery(Now.AddDays(-1), Now, HabitType.Smoking, null);

        var result = await handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().ContainSingle(dto => dto.Id == entry.Id && dto.SubType == HabitSubType.Cigarette);
    }

    [Fact]
    public async Task Handle_WithFromAfterTo_ReturnsFailure()
    {
        var repository = Substitute.For<IHabitEntryRepository>();
        var handler = new GetHabitEntriesQueryHandler(repository, new GetHabitEntriesQueryValidator());
        var query = new GetHabitEntriesQuery(Now, Now.AddDays(-1), null, null);

        var result = await handler.Handle(query, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        await repository.DidNotReceive().GetAsync(
            Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>(), Arg.Any<HabitType?>(), Arg.Any<HabitSubType?>(), Arg.Any<CancellationToken>());
    }
}
