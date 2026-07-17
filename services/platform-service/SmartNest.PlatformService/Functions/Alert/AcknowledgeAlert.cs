using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using SmartNest.Shared.Security;

namespace SmartNest.PlatformService.Functions.Alert;

public sealed class AcknowledgeAlert
{
    private readonly Handlers.Alert.AcknowledgeAlertHandler _handler;
    private readonly IJwtValidator _jwtValidator;

    public AcknowledgeAlert(Handlers.Alert.AcknowledgeAlertHandler handler, IJwtValidator jwtValidator)
    {
        _handler = handler ?? throw new ArgumentNullException(nameof(handler));
        _jwtValidator = jwtValidator ?? throw new ArgumentNullException(nameof(jwtValidator));
    }

    [Function("AcknowledgeAlert")]
    public Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "homes/{homeId}/alerts/{id}/acknowledge")] HttpRequest req,
        string homeId,
        string id,
        CancellationToken cancellationToken) =>
        HttpFunctionHelpers.ExecuteAsync(async () =>
        {
            var user = await HttpFunctionHelpers.GetCurrentUserAsync(req, _jwtValidator, cancellationToken).ConfigureAwait(false);

            var result = await _handler.HandleAsync(user, homeId, id, cancellationToken).ConfigureAwait(false);

            return new OkObjectResult(result);
        });
}
