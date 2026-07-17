using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using SmartNest.DeviceService.Handlers;
using SmartNest.Shared.Security;

namespace SmartNest.DeviceService.Functions;

public sealed class RemoveDevice
{
    private readonly RemoveDeviceHandler _handler;
    private readonly IJwtValidator _jwtValidator;

    public RemoveDevice(RemoveDeviceHandler handler, IJwtValidator jwtValidator)
    {
        _handler = handler ?? throw new ArgumentNullException(nameof(handler));
        _jwtValidator = jwtValidator ?? throw new ArgumentNullException(nameof(jwtValidator));
    }

    [Function("RemoveDevice")]
    public Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Function, "delete", Route = "devices/{id}")] HttpRequest req,
        string id,
        CancellationToken cancellationToken) =>
        HttpFunctionHelpers.ExecuteAsync(async () =>
        {
            var user = await HttpFunctionHelpers.GetCurrentUserAsync(req, _jwtValidator, cancellationToken).ConfigureAwait(false);
            await _handler.HandleAsync(user, id, cancellationToken).ConfigureAwait(false);
            return new NoContentResult();
        });
}
