using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using SmartNest.DeviceService.Dtos;
using SmartNest.DeviceService.Handlers;

namespace SmartNest.DeviceService.Functions;

public sealed class UpdateDeviceState
{
    private readonly UpdateDeviceStateHandler _handler;

    public UpdateDeviceState(UpdateDeviceStateHandler handler)
    {
        _handler = handler ?? throw new ArgumentNullException(nameof(handler));
    }

    [Function("UpdateDeviceState")]
    public Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Function, "patch", Route = "devices/{id}/state")] HttpRequest req,
        string id,
        CancellationToken cancellationToken) =>
        HttpFunctionHelpers.ExecuteAsync(async () =>
        {
            var user = HttpFunctionHelpers.GetCurrentUser(req);
            var request = await HttpFunctionHelpers.ReadRequiredJsonAsync<UpdateDeviceStateRequest>(req, cancellationToken)
                .ConfigureAwait(false);

            var result = await _handler.HandleAsync(user, id, request, cancellationToken).ConfigureAwait(false);

            return new OkObjectResult(result);
        });
}
