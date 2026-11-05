using DzhusShelter.Api.Domain.Common;
using Microsoft.AspNetCore.Mvc;

namespace DzhusShelter.Api.Controllers;

internal static class ErrorResults
{
    /// <summary>
    /// A failed <see cref="Result"/> as HTTP: codes ending in ".NotFound" (e.g. BirthdayErrors.NotFound)
    /// → 404, anything else → 400; both with Bad Habits' <c>{ "error": "..." }</c> body.
    /// </summary>
    public static IActionResult ToActionResult(this Error error) =>
        error.Code.EndsWith(".NotFound", StringComparison.Ordinal)
            ? new NotFoundObjectResult(new { error = error.Message })
            : new BadRequestObjectResult(new { error = error.Message });
}
