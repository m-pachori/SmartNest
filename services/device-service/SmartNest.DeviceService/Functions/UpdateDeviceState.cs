using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using SmartNest.DeviceService.Dtos;
using SmartNest.DeviceService.Handlers;
using SmartNest.Shared.Security;

namespace SmartNest.DeviceService.Functions;

public sealed class UpdateDeviceState
{
    private readonly UpdateDeviceStateHandler _handler;
    private readonly IJwtValidator _jwtValidator;

    public UpdateDeviceState(UpdateDeviceStateHandler handler, IJwtValidator jwtValidator)
    {
        _handler = handler ?? throw new ArgumentNullException(nameof(handler));
        _jwtValidator = jwtValidator ?? throw new ArgumentNullException(nameof(jwtValidator));
    }

    [Function("UpdateDeviceState")]
    public Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Function, "patch", Route = "devices/{id}/state")] HttpRequest req,
        string id,
        CancellationToken cancellationToken) =>
        HttpFunctionHelpers.ExecuteAsync(async () =>
        {
            var user = await HttpFunctionHelpers.GetCurrentUserAsync(req, _jwtValidator, cancellationToken).ConfigureAwait(false);
            var request = await HttpFunctionHelpers.ReadRequiredJsonAsync<UpdateDeviceStateRequest>(req, cancellationToken)
                .ConfigureAwait(false);

            var result = await _handler.HandleAsync(user, id, request, cancellationToken).ConfigureAwait(false);

            return new OkObjectResult(result);
        });
}
