using DzhusShelter.Api.Application.BadHabits;
using DzhusShelter.Api.Application.BadHabits.Commands;
using DzhusShelter.Api.Domain.BadHabits;
using FluentAssertions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace DzhusShelter.Api.Tests.Application;

public class LogHabitEntryCommandHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 15, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Handle_WithValidCommand_PersistsEntryAndReturnsNewId()
    {
        var repository = Substitute.For<IHabitEntryRepository>();
        repository
            .GetAsync(Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>(), Arg.Any<HabitType>(), Arg.Any<HabitSubType>(), Arg.Any<CancellationToken>())
            .Returns(new List<HabitEntry>());
        var handler = new LogHabitEntryCommandHandler(repository, new LogHabitEntryCommandValidator(), new FakeTimeProvider(Now));
        var command = new LogHabitEntryCommand(HabitType.Alcohol, HabitSubType.Beer, Now.AddMinutes(-1), "with friends");

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().NotBeEmpty();
        result.Value.AlreadyLogged.Should().BeFalse();
        await repository.Received(1).AddAsync(
            Arg.Is<HabitEntry>(e => e.HabitType == HabitType.Alcohol && e.SubType == HabitSubType.Beer),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithMismatchedSubType_DoesNotPersistAndReturnsFailure()
    {
        var repository = Substitute.For<IHabitEntryRepository>();
        var handler = new LogHabitEntryCommandHandler(repository, new LogHabitEntryCommandValidator(), new FakeTimeProvider(Now));
        var command = new LogHabitEntryCommand(HabitType.Smoking, HabitSubType.Wine, Now, null);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        await repository.DidNotReceive().AddAsync(Arg.Any<HabitEntry>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ComputesUtcDayRangeFromNonUtcOccurredAt()
    {
        // 01:00 in UTC+3 on 2026-01-15 is 22:00 UTC on 2026-01-14 — the handler must query the
        // UTC calendar day the entry actually falls on, not the day in the original offset.
        var occurredAt = new DateTimeOffset(2026, 1, 15, 1, 0, 0, TimeSpan.FromHours(3));
        var expectedDayStart = new DateTimeOffset(2026, 1, 14, 0, 0, 0, TimeSpan.Zero);
        var expectedDayEnd = new DateTimeOffset(2026, 1, 15, 0, 0, 0, TimeSpan.Zero).AddTicks(-1);

        var repository = Substitute.For<IHabitEntryRepository>();
        repository
            .GetAsync(Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>(), Arg.Any<HabitType>(), Arg.Any<HabitSubType>(), Arg.Any<CancellationToken>())
            .Returns(new List<HabitEntry>());
        var handler = new LogHabitEntryCommandHandler(repository, new LogHabitEntryCommandValidator(), new FakeTimeProvider(Now));
        var command = new LogHabitEntryCommand(HabitType.Alcohol, HabitSubType.Beer, occurredAt, null);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        await repository.Received(1).GetAsync(
            expectedDayStart, expectedDayEnd, HabitType.Alcohol, HabitSubType.Beer, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenSameSubTypeAlreadyLoggedToday_DoesNotPersistAndReturnsAlreadyLogged()
    {
        var repository = Substitute.For<IHabitEntryRepository>();
        var existingEntry = HabitEntry.Create(HabitType.Alcohol, HabitSubType.Beer, Now.AddHours(-2), null, new FakeTimeProvider(Now)).Value;
        repository
            .GetAsync(Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>(), HabitType.Alcohol, HabitSubType.Beer, Arg.Any<CancellationToken>())
            .Returns(new List<HabitEntry> { existingEntry });
        var handler = new LogHabitEntryCommandHandler(repository, new LogHabitEntryCommandValidator(), new FakeTimeProvider(Now));
        var command = new LogHabitEntryCommand(HabitType.Alcohol, HabitSubType.Beer, Now.AddMinutes(-1), null);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(existingEntry.Id);
        result.Value.AlreadyLogged.Should().BeTrue();
        await repository.DidNotReceive().AddAsync(Arg.Any<HabitEntry>(), Arg.Any<CancellationToken>());
    }
}
