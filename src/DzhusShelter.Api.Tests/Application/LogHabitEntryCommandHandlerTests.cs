using DzhusShelter.Api.Application.BadHabits;
using DzhusShelter.Api.Domain.BadHabits;
using FluentAssertions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace DzhusShelter.Api.Tests.Application;

public class LogHabitEntryCommandHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 15, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Handle_WithValidCommand_PersistsEntryAndReturnsItsId()
    {
        var repository = Substitute.For<IHabitEntryRepository>();
        var handler = new LogHabitEntryCommandHandler(repository, new LogHabitEntryCommandValidator(), new FakeTimeProvider(Now));
        var command = new LogHabitEntryCommand(HabitType.Alcohol, HabitSubType.Beer, Now.AddMinutes(-1), "with friends");

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeEmpty();
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
}
