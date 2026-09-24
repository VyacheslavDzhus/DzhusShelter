using DzhusShelter.Api.Application.Abstractions;
using DzhusShelter.Api.Application.BadHabits.Commands;
using DzhusShelter.Api.Application.BadHabits.Dtos;
using DzhusShelter.Api.Application.BadHabits.Queries;
using DzhusShelter.Api.Domain.Common;
using Microsoft.AspNetCore.Mvc;

namespace DzhusShelter.Api.Controllers;

[ApiController]
[Route("api/bad-habits")]
public sealed class BadHabitsController : ControllerBase
{
    private readonly ICommandHandler<LogHabitEntryCommand, Result<Guid>> _logHandler;
    private readonly IQueryHandler<GetHabitEntriesQuery, Result<IReadOnlyList<HabitEntryDto>>> _getHandler;

    public BadHabitsController(
        ICommandHandler<LogHabitEntryCommand, Result<Guid>> logHandler,
        IQueryHandler<GetHabitEntriesQuery, Result<IReadOnlyList<HabitEntryDto>>> getHandler)
    {
        _logHandler = logHandler;
        _getHandler = getHandler;
    }

    [HttpPost("entries")]
    public async Task<IActionResult> LogEntry([FromBody] LogHabitEntryCommand command, CancellationToken cancellationToken)
    {
        var result = await _logHandler.Handle(command, cancellationToken);
        return result.IsSuccess
            ? Created(string.Empty, new { id = result.Value })
            : BadRequest(new { error = result.Error.Message });
    }

    [HttpGet("entries")]
    public async Task<IActionResult> GetEntries([FromQuery] GetHabitEntriesQuery query, CancellationToken cancellationToken)
    {
        var result = await _getHandler.Handle(query, cancellationToken);
        return result.IsSuccess
            ? Ok(result.Value)
            : BadRequest(new { error = result.Error.Message });
    }
}
