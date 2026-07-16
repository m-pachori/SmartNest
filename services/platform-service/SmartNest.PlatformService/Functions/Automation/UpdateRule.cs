using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using SmartNest.PlatformService.Dtos.Automation;
using SmartNest.PlatformService.Handlers.Automation;

namespace SmartNest.PlatformService.Functions.Automation;

public sealed class UpdateRule
{
    private readonly UpdateRuleHandler _handler;

    public UpdateRule(UpdateRuleHandler handler)
    {
        _handler = handler ?? throw new ArgumentNullException(nameof(handler));
    }

    [Function("UpdateRule")]
    public Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Function, "put", Route = "homes/{homeId}/rules/{id}")] HttpRequest req,
        string homeId,
        string id,
        CancellationToken cancellationToken) =>
        HttpFunctionHelpers.ExecuteAsync(async () =>
        {
            var user = HttpFunctionHelpers.GetCurrentUser(req);
            var request = await HttpFunctionHelpers.ReadRequiredJsonAsync<UpdateRuleRequest>(req, cancellationToken)
                .ConfigureAwait(false);

            var result = await _handler.HandleAsync(user, homeId, id, request, cancellationToken).ConfigureAwait(false);

            return new OkObjectResult(result);
        });
}
