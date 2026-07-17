using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using SmartNest.DeviceService.Dtos;
using SmartNest.DeviceService.Handlers;
using SmartNest.Shared.Security;

namespace SmartNest.DeviceService.Functions;

public sealed class RegisterDevice
{
    private readonly RegisterDeviceHandler _handler;
    private readonly IJwtValidator _jwtValidator;

    public RegisterDevice(RegisterDeviceHandler handler, IJwtValidator jwtValidator)
    {
        _handler = handler ?? throw new ArgumentNullException(nameof(handler));
        _jwtValidator = jwtValidator ?? throw new ArgumentNullException(nameof(jwtValidator));
    }

    [Function("RegisterDevice")]
    public Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "homes/{homeId}/devices")] HttpRequest req,
        string homeId,
        CancellationToken cancellationToken) =>
        HttpFunctionHelpers.ExecuteAsync(async () =>
        {
            var user = await HttpFunctionHelpers.GetCurrentUserAsync(req, _jwtValidator, cancellationToken).ConfigureAwait(false);
            var request = await HttpFunctionHelpers.ReadRequiredJsonAsync<RegisterDeviceRequest>(req, cancellationToken)
                .ConfigureAwait(false);

            var result = await _handler.HandleAsync(user, homeId, request, cancellationToken).ConfigureAwait(false);

            return new ObjectResult(result) { StatusCode = StatusCodes.Status201Created };
        });
}
