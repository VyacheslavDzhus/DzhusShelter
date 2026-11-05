using DzhusShelter.Api.Application.Abstractions;
using DzhusShelter.Api.Application.Birthdays.Commands;
using DzhusShelter.Api.Application.Birthdays.Dtos;
using DzhusShelter.Api.Application.Birthdays.Queries;
using DzhusShelter.Api.Domain.Common;
using DzhusShelter.Api.Infrastructure.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DzhusShelter.Api.Controllers;

// Policy on the class, never per action: any action added here later is UI-only by default
// (fail closed) instead of silently falling back to the global FallbackPolicy, which would let
// the bot/Shortcuts keys read birthday data. The main bot's endpoints live in
// BirthdayNotificationsController for the same reason — class-level policies are AND-ed with
// action-level ones, so the two disjoint groups can't share one class.
[ApiController]
[Route("api/birthdays")]
[Authorize(Policy = AuthPolicies.UiOnly)]
public sealed class BirthdaysController : ControllerBase
{
    private readonly IQueryHandler<GetBirthdaysQuery, Result<IReadOnlyList<BirthdayDto>>> _getHandler;
    private readonly ICommandHandler<CreateBirthdayCommand, Result<Guid>> _createHandler;
    private readonly ICommandHandler<UpdateBirthdayCommand, Result> _updateHandler;
    private readonly ICommandHandler<DeleteBirthdayCommand, Result> _deleteHandler;

    public BirthdaysController(
        IQueryHandler<GetBirthdaysQuery, Result<IReadOnlyList<BirthdayDto>>> getHandler,
        ICommandHandler<CreateBirthdayCommand, Result<Guid>> createHandler,
        ICommandHandler<UpdateBirthdayCommand, Result> updateHandler,
        ICommandHandler<DeleteBirthdayCommand, Result> deleteHandler)
    {
        _getHandler = getHandler;
        _createHandler = createHandler;
        _updateHandler = updateHandler;
        _deleteHandler = deleteHandler;
    }

    /// <summary>PUT body — the id comes from the route, not the payload.</summary>
    public sealed record UpdateBirthdayRequest(string Name, int Day, int Month, int? Year);

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var result = await _getHandler.Handle(new GetBirthdaysQuery(), cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : result.Error.ToActionResult();
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateBirthdayCommand command, CancellationToken cancellationToken)
    {
        var result = await _createHandler.Handle(command, cancellationToken);

        // Plain 201 without a Location header: there is no GET-by-id endpoint to point at.
        return result.IsSuccess
            ? StatusCode(StatusCodes.Status201Created, new { id = result.Value })
            : result.Error.ToActionResult();
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateBirthdayRequest request, CancellationToken cancellationToken)
    {
        var result = await _updateHandler.Handle(
            new UpdateBirthdayCommand(id, request.Name, request.Day, request.Month, request.Year), cancellationToken);
        return result.IsSuccess ? NoContent() : result.Error.ToActionResult();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var result = await _deleteHandler.Handle(new DeleteBirthdayCommand(id), cancellationToken);
        return result.IsSuccess ? NoContent() : result.Error.ToActionResult();
    }
}
