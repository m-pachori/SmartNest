using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using SmartNest.DeviceService.Handlers;

namespace SmartNest.DeviceService.Functions;

public sealed class RemoveDevice
{
    private readonly RemoveDeviceHandler _handler;

    public RemoveDevice(RemoveDeviceHandler handler)
    {
        _handler = handler ?? throw new ArgumentNullException(nameof(handler));
    }

    [Function("RemoveDevice")]
    public Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Function, "delete", Route = "devices/{id}")] HttpRequest req,
        string id,
        CancellationToken cancellationToken) =>
        HttpFunctionHelpers.ExecuteAsync(async () =>
        {
            var user = HttpFunctionHelpers.GetCurrentUser(req);
            await _handler.HandleAsync(user, id, cancellationToken).ConfigureAwait(false);
            return new NoContentResult();
        });
}
