using DzhusShelter.Api.Application.Abstractions;
using DzhusShelter.Api.Application.Birthdays.Commands;
using DzhusShelter.Api.Application.Birthdays.Dtos;
using DzhusShelter.Api.Application.Birthdays.Queries;
using DzhusShelter.Api.Domain.Common;
using DzhusShelter.Api.Infrastructure.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DzhusShelter.Api.Controllers;

// Authorization is per action, never on the class: a class-level policy would be AND-ed with
// the action's, and the two groups here are deliberately disjoint (UI vs main bot). An action
// added later without an attribute falls back to the global FallbackPolicy.
[ApiController]
[Route("api/birthdays")]
public sealed class BirthdaysController : ControllerBase
{
    private readonly IQueryHandler<GetBirthdaysQuery, Result<IReadOnlyList<BirthdayDto>>> _getHandler;
    private readonly ICommandHandler<CreateBirthdayCommand, Result<Guid>> _createHandler;
    private readonly ICommandHandler<UpdateBirthdayCommand, Result> _updateHandler;
    private readonly ICommandHandler<DeleteBirthdayCommand, Result> _deleteHandler;
    private readonly IQueryHandler<GetPendingBirthdayNotificationsQuery, Result<IReadOnlyList<PendingBirthdayNotificationDto>>> _pendingHandler;
    private readonly ICommandHandler<MarkBirthdayNotificationSentCommand, Result> _markSentHandler;

    public BirthdaysController(
        IQueryHandler<GetBirthdaysQuery, Result<IReadOnlyList<BirthdayDto>>> getHandler,
        ICommandHandler<CreateBirthdayCommand, Result<Guid>> createHandler,
        ICommandHandler<UpdateBirthdayCommand, Result> updateHandler,
        ICommandHandler<DeleteBirthdayCommand, Result> deleteHandler,
        IQueryHandler<GetPendingBirthdayNotificationsQuery, Result<IReadOnlyList<PendingBirthdayNotificationDto>>> pendingHandler,
        ICommandHandler<MarkBirthdayNotificationSentCommand, Result> markSentHandler)
    {
        _getHandler = getHandler;
        _createHandler = createHandler;
        _updateHandler = updateHandler;
        _deleteHandler = deleteHandler;
        _pendingHandler = pendingHandler;
        _markSentHandler = markSentHandler;
    }

    /// <summary>PUT body — the id comes from the route, not the payload.</summary>
    public sealed record UpdateBirthdayRequest(string Name, int Day, int Month, int? Year);

    [HttpGet]
    [Authorize(Policy = AuthPolicies.UiOnly)]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var result = await _getHandler.Handle(new GetBirthdaysQuery(), cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : Failure(result.Error);
    }

    [HttpPost]
    [Authorize(Policy = AuthPolicies.UiOnly)]
    public async Task<IActionResult> Create([FromBody] CreateBirthdayCommand command, CancellationToken cancellationToken)
    {
        var result = await _createHandler.Handle(command, cancellationToken);

        // Plain 201 without a Location header: there is no GET-by-id endpoint to point at.
        return result.IsSuccess
            ? StatusCode(StatusCodes.Status201Created, new { id = result.Value })
            : Failure(result.Error);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = AuthPolicies.UiOnly)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateBirthdayRequest request, CancellationToken cancellationToken)
    {
        var result = await _updateHandler.Handle(
            new UpdateBirthdayCommand(id, request.Name, request.Day, request.Month, request.Year), cancellationToken);
        return result.IsSuccess ? NoContent() : Failure(result.Error);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = AuthPolicies.UiOnly)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var result = await _deleteHandler.Handle(new DeleteBirthdayCommand(id), cancellationToken);
        return result.IsSuccess ? NoContent() : Failure(result.Error);
    }

    [HttpGet("notifications/pending")]
    [Authorize(Policy = AuthPolicies.MainBotNotifications)]
    public async Task<IActionResult> GetPendingNotifications(CancellationToken cancellationToken)
    {
        var result = await _pendingHandler.Handle(new GetPendingBirthdayNotificationsQuery(), cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : Failure(result.Error);
    }

    [HttpPost("notifications/sent")]
    [Authorize(Policy = AuthPolicies.MainBotNotifications)]
    public async Task<IActionResult> MarkNotificationSent(
        [FromBody] MarkBirthdayNotificationSentCommand command, CancellationToken cancellationToken)
    {
        var result = await _markSentHandler.Handle(command, cancellationToken);
        return result.IsSuccess ? NoContent() : Failure(result.Error);
    }

    // The ".NotFound" suffix convention lives in the error codes (e.g. BirthdayErrors.NotFound).
    private IActionResult Failure(Error error) =>
        error.Code.EndsWith(".NotFound", StringComparison.Ordinal)
            ? NotFound(new { error = error.Message })
            : BadRequest(new { error = error.Message });
}
