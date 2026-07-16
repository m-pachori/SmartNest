using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using SmartNest.PlatformService.Handlers.Audit;

namespace SmartNest.PlatformService.Functions.Audit;

public sealed class ReplayEvents
{
    private readonly ReplayEventsHandler _handler;

    public ReplayEvents(ReplayEventsHandler handler)
    {
        _handler = handler ?? throw new ArgumentNullException(nameof(handler));
    }

    [Function("ReplayEvents")]
    public Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "audit/replay/{aggregateId}")] HttpRequest req,
        string aggregateId,
        CancellationToken cancellationToken) =>
        HttpFunctionHelpers.ExecuteAsync(async () =>
        {
            var user = HttpFunctionHelpers.GetCurrentUser(req);

            var result = await _handler.HandleAsync(user, aggregateId, cancellationToken).ConfigureAwait(false);

            return new OkObjectResult(result);
        });
}
