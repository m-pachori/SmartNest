using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using SmartNest.HomeService.Handlers;

namespace SmartNest.HomeService.Functions;

public sealed class GetHome
{
    private readonly GetHomeHandler _handler;

    public GetHome(GetHomeHandler handler)
    {
        _handler = handler ?? throw new ArgumentNullException(nameof(handler));
    }

    [Function("GetHome")]
    public Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "homes/{id}")] HttpRequest req,
        string id,
        CancellationToken cancellationToken) =>
        HttpFunctionHelpers.ExecuteAsync(async () =>
        {
            var user = HttpFunctionHelpers.GetCurrentUser(req);
            var result = await _handler.HandleAsync(user, id, cancellationToken).ConfigureAwait(false);
            return new OkObjectResult(result);
        });
}
