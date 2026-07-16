using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace SmartNest.PlatformService.Functions.Alert;

public sealed class GetAlerts
{
    private readonly Handlers.Alert.GetAlertsHandler _handler;

    public GetAlerts(Handlers.Alert.GetAlertsHandler handler)
    {
        _handler = handler ?? throw new ArgumentNullException(nameof(handler));
    }

    [Function("GetAlerts")]
    public Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "alerts")] HttpRequest req,
        CancellationToken cancellationToken) =>
        HttpFunctionHelpers.ExecuteAsync(async () =>
        {
            var user = HttpFunctionHelpers.GetCurrentUser(req);
            var homeId = req.Query["homeId"].FirstOrDefault()
                ?? throw new ArgumentException("homeId query parameter is required.");

            var result = await _handler.HandleAsync(user, homeId, cancellationToken).ConfigureAwait(false);

            return new OkObjectResult(result);
        });
}
