using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using SmartNest.DeviceService.Handlers;

namespace SmartNest.DeviceService.Functions;

public sealed class GetDevice
{
    private readonly GetDeviceHandler _handler;

    public GetDevice(GetDeviceHandler handler)
    {
        _handler = handler ?? throw new ArgumentNullException(nameof(handler));
    }

    [Function("GetDevice")]
    public Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "devices/{id}")] HttpRequest req,
        string id,
        CancellationToken cancellationToken) =>
        HttpFunctionHelpers.ExecuteAsync(async () =>
        {
            var user = HttpFunctionHelpers.GetCurrentUser(req);
            var result = await _handler.HandleAsync(user, id, cancellationToken).ConfigureAwait(false);
            return new OkObjectResult(result);
        });
}
