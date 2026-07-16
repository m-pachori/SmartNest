using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace SmartNest.PlatformService.Functions.Alert;

public sealed class AcknowledgeAlert
{
    private readonly Handlers.Alert.AcknowledgeAlertHandler _handler;

    public AcknowledgeAlert(Handlers.Alert.AcknowledgeAlertHandler handler)
    {
        _handler = handler ?? throw new ArgumentNullException(nameof(handler));
    }

    [Function("AcknowledgeAlert")]
    public Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "homes/{homeId}/alerts/{id}/acknowledge")] HttpRequest req,
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
