using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using SmartNest.PlatformService.Handlers.Automation;

namespace SmartNest.PlatformService.Functions.Automation;

public sealed class GetRule
{
    private readonly GetRuleHandler _handler;

    public GetRule(GetRuleHandler handler)
    {
        _handler = handler ?? throw new ArgumentNullException(nameof(handler));
    }

    [Function("GetRule")]
    public Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "homes/{homeId}/rules/{id}")] HttpRequest req,
        string homeId,
        string id,
        CancellationToken cancellationToken) =>
        HttpFunctionHelpers.ExecuteAsync(async () =>
        {
            var user = HttpFunctionHelpers.GetCurrentUser(req);

            var result = await _handler.HandleAsync(user, homeId, id, cancellationToken).ConfigureAwait(false);

            return new OkObjectResult(result);
        });
}
