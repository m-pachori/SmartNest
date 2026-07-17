using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using SmartNest.PlatformService.Handlers.Audit;
using SmartNest.Shared.Security;

namespace SmartNest.PlatformService.Functions.Audit;

public sealed class ReplayEvents
{
    private readonly ReplayEventsHandler _handler;
    private readonly IJwtValidator _jwtValidator;

    public ReplayEvents(ReplayEventsHandler handler, IJwtValidator jwtValidator)
    {
        _handler = handler ?? throw new ArgumentNullException(nameof(handler));
        _jwtValidator = jwtValidator ?? throw new ArgumentNullException(nameof(jwtValidator));
    }

    [Function("ReplayEvents")]
    public Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "audit/replay/{aggregateId}")] HttpRequest req,
        string aggregateId,
        CancellationToken cancellationToken) =>
        HttpFunctionHelpers.ExecuteAsync(async () =>
        {
            var user = await HttpFunctionHelpers.GetCurrentUserAsync(req, _jwtValidator, cancellationToken).ConfigureAwait(false);

            var result = await _handler.HandleAsync(user, aggregateId, cancellationToken).ConfigureAwait(false);

            return new OkObjectResult(result);
        });
}
