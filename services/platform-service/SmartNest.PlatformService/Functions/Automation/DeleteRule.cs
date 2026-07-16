using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using SmartNest.PlatformService.Handlers.Automation;

namespace SmartNest.PlatformService.Functions.Automation;

public sealed class DeleteRule
{
    private readonly DeleteRuleHandler _handler;

    public DeleteRule(DeleteRuleHandler handler)
    {
        _handler = handler ?? throw new ArgumentNullException(nameof(handler));
    }

    [Function("DeleteRule")]
    public Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Function, "delete", Route = "homes/{homeId}/rules/{id}")] HttpRequest req,
        string homeId,
        string id,
        CancellationToken cancellationToken) =>
        HttpFunctionHelpers.ExecuteAsync(async () =>
        {
            var user = HttpFunctionHelpers.GetCurrentUser(req);

            await _handler.HandleAsync(user, homeId, id, cancellationToken).ConfigureAwait(false);

            return new NoContentResult();
        });
}
