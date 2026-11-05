using DzhusShelter.Api.Application.Abstractions;
using DzhusShelter.Api.Application.Birthdays.Commands;
using DzhusShelter.Api.Application.Birthdays.Dtos;
using DzhusShelter.Api.Application.Birthdays.Queries;
using DzhusShelter.Api.Domain.Common;
using DzhusShelter.Api.Infrastructure.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DzhusShelter.Api.Controllers;

// Main bot only, as a class-level policy so a later action can't fall open to the
// FallbackPolicy (see BirthdaysController for why the two groups are separate classes).
[ApiController]
[Route("api/birthdays/notifications")]
[Authorize(Policy = AuthPolicies.MainBotNotifications)]
public sealed class BirthdayNotificationsController : ControllerBase
{
    private readonly IQueryHandler<GetPendingBirthdayNotificationsQuery, Result<IReadOnlyList<PendingBirthdayNotificationDto>>> _pendingHandler;
    private readonly ICommandHandler<MarkBirthdayNotificationSentCommand, Result> _markSentHandler;

    public BirthdayNotificationsController(
        IQueryHandler<GetPendingBirthdayNotificationsQuery, Result<IReadOnlyList<PendingBirthdayNotificationDto>>> pendingHandler,
        ICommandHandler<MarkBirthdayNotificationSentCommand, Result> markSentHandler)
    {
        _pendingHandler = pendingHandler;
        _markSentHandler = markSentHandler;
    }

    [HttpGet("pending")]
    public async Task<IActionResult> GetPending(CancellationToken cancellationToken)
    {
        var result = await _pendingHandler.Handle(new GetPendingBirthdayNotificationsQuery(), cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : result.Error.ToActionResult();
    }

    [HttpPost("sent")]
    public async Task<IActionResult> MarkSent([FromBody] MarkBirthdayNotificationSentCommand command, CancellationToken cancellationToken)
    {
        var result = await _markSentHandler.Handle(command, cancellationToken);
        return result.IsSuccess ? NoContent() : result.Error.ToActionResult();
    }
}
