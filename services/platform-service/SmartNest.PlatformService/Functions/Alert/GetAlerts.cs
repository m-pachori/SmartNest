using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using SmartNest.Shared.Security;

namespace SmartNest.PlatformService.Functions.Alert;

public sealed class GetAlerts
{
    private readonly Handlers.Alert.GetAlertsHandler _handler;
    private readonly IJwtValidator _jwtValidator;

    public GetAlerts(Handlers.Alert.GetAlertsHandler handler, IJwtValidator jwtValidator)
    {
        _handler = handler ?? throw new ArgumentNullException(nameof(handler));
        _jwtValidator = jwtValidator ?? throw new ArgumentNullException(nameof(jwtValidator));
    }

    [Function("GetAlerts")]
    public Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "alerts")] HttpRequest req,
        CancellationToken cancellationToken) =>
        HttpFunctionHelpers.ExecuteAsync(async () =>
        {
            var user = await HttpFunctionHelpers.GetCurrentUserAsync(req, _jwtValidator, cancellationToken).ConfigureAwait(false);
            var homeId = req.Query["homeId"].FirstOrDefault()
                ?? throw new ArgumentException("homeId query parameter is required.");

            var result = await _handler.HandleAsync(user, homeId, cancellationToken).ConfigureAwait(false);

            return new OkObjectResult(result);
        });
}
