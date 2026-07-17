using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using SmartNest.HomeService.Handlers;
using SmartNest.Shared.Security;

namespace SmartNest.HomeService.Functions;

public sealed class GetHome
{
    private readonly GetHomeHandler _handler;
    private readonly IJwtValidator _jwtValidator;

    public GetHome(GetHomeHandler handler, IJwtValidator jwtValidator)
    {
        _handler = handler ?? throw new ArgumentNullException(nameof(handler));
        _jwtValidator = jwtValidator ?? throw new ArgumentNullException(nameof(jwtValidator));
    }

    [Function("GetHome")]
    public Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "homes/{id}")] HttpRequest req,
        string id,
        CancellationToken cancellationToken) =>
        HttpFunctionHelpers.ExecuteAsync(async () =>
        {
            var user = await HttpFunctionHelpers.GetCurrentUserAsync(req, _jwtValidator, cancellationToken).ConfigureAwait(false);
            var result = await _handler.HandleAsync(user, id, cancellationToken).ConfigureAwait(false);
            return new OkObjectResult(result);
        });
}
