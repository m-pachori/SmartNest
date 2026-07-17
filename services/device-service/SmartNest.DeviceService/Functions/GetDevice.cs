using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using SmartNest.DeviceService.Handlers;
using SmartNest.Shared.Security;

namespace SmartNest.DeviceService.Functions;

public sealed class GetDevice
{
    private readonly GetDeviceHandler _handler;
    private readonly IJwtValidator _jwtValidator;

    public GetDevice(GetDeviceHandler handler, IJwtValidator jwtValidator)
    {
        _handler = handler ?? throw new ArgumentNullException(nameof(handler));
        _jwtValidator = jwtValidator ?? throw new ArgumentNullException(nameof(jwtValidator));
    }

    [Function("GetDevice")]
    public Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "devices/{id}")] HttpRequest req,
        string id,
        CancellationToken cancellationToken) =>
        HttpFunctionHelpers.ExecuteAsync(async () =>
        {
            var user = await HttpFunctionHelpers.GetCurrentUserAsync(req, _jwtValidator, cancellationToken).ConfigureAwait(false);
            var result = await _handler.HandleAsync(user, id, cancellationToken).ConfigureAwait(false);
            return new OkObjectResult(result);
        });
}
