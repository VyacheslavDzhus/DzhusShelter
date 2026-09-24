using DzhusShelter.Api.Application.Abstractions;
using DzhusShelter.Api.Application.BadHabits.Commands;
using DzhusShelter.Api.Application.BadHabits.Dtos;
using DzhusShelter.Api.Application.BadHabits.Queries;
using DzhusShelter.Api.Controllers;
using DzhusShelter.Api.Domain.BadHabits;
using DzhusShelter.Api.Domain.Common;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;

namespace DzhusShelter.Api.Tests.Controllers;

public class BadHabitsControllerTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 15, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task LogEntry_WhenNewlyLogged_ReturnsOkWithAlreadyLoggedFalse()
    {
        var commandHandler = Substitute.For<ICommandHandler<LogHabitEntryCommand, Result<LogHabitEntryResult>>>();
        var entryId = Guid.NewGuid();
        commandHandler.Handle(Arg.Any<LogHabitEntryCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(new LogHabitEntryResult(entryId, AlreadyLogged: false)));
        var queryHandler = Substitute.For<IQueryHandler<GetHabitEntriesQuery, Result<IReadOnlyList<HabitEntryDto>>>>();
        var controller = new BadHabitsController(commandHandler, queryHandler);

        var response = await controller.LogEntry(
            new LogHabitEntryCommand(HabitType.Smoking, HabitSubType.Cigarette, Now, null), CancellationToken.None);

        var okResult = response.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().BeEquivalentTo(new { id = entryId, alreadyLogged = false });
    }

    [Fact]
    public async Task LogEntry_WhenAlreadyLoggedToday_ReturnsOkWithAlreadyLoggedTrue()
    {
        var commandHandler = Substitute.For<ICommandHandler<LogHabitEntryCommand, Result<LogHabitEntryResult>>>();
        var existingId = Guid.NewGuid();
        commandHandler.Handle(Arg.Any<LogHabitEntryCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(new LogHabitEntryResult(existingId, AlreadyLogged: true)));
        var queryHandler = Substitute.For<IQueryHandler<GetHabitEntriesQuery, Result<IReadOnlyList<HabitEntryDto>>>>();
        var controller = new BadHabitsController(commandHandler, queryHandler);

        var response = await controller.LogEntry(
            new LogHabitEntryCommand(HabitType.Smoking, HabitSubType.Cigarette, Now, null), CancellationToken.None);

        var okResult = response.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().BeEquivalentTo(new { id = existingId, alreadyLogged = true });
    }

    [Fact]
    public async Task LogEntry_WhenHandlerFails_ReturnsBadRequest()
    {
        var commandHandler = Substitute.For<ICommandHandler<LogHabitEntryCommand, Result<LogHabitEntryResult>>>();
        commandHandler.Handle(Arg.Any<LogHabitEntryCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure<LogHabitEntryResult>(new Error("BadHabits.Validation", "bad input")));
        var queryHandler = Substitute.For<IQueryHandler<GetHabitEntriesQuery, Result<IReadOnlyList<HabitEntryDto>>>>();
        var controller = new BadHabitsController(commandHandler, queryHandler);

        var response = await controller.LogEntry(
            new LogHabitEntryCommand(HabitType.Smoking, HabitSubType.Cigarette, Now, null), CancellationToken.None);

        response.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task GetEntries_WhenHandlerSucceeds_ReturnsOkWithEntries()
    {
        var commandHandler = Substitute.For<ICommandHandler<LogHabitEntryCommand, Result<LogHabitEntryResult>>>();
        var queryHandler = Substitute.For<IQueryHandler<GetHabitEntriesQuery, Result<IReadOnlyList<HabitEntryDto>>>>();
        var dtos = new List<HabitEntryDto>
        {
            new(Guid.NewGuid(), HabitType.Smoking, HabitSubType.Cigarette, Now, null),
        };
        queryHandler.Handle(Arg.Any<GetHabitEntriesQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success<IReadOnlyList<HabitEntryDto>>(dtos));
        var controller = new BadHabitsController(commandHandler, queryHandler);

        var response = await controller.GetEntries(
            new GetHabitEntriesQuery(Now.AddDays(-1), Now, null, null), CancellationToken.None);

        var okResult = response.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().BeEquivalentTo(dtos);
    }
}
