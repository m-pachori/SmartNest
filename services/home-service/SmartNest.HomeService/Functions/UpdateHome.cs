using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using SmartNest.HomeService.Dtos;
using SmartNest.HomeService.Handlers;

namespace SmartNest.HomeService.Functions;

public sealed class UpdateHome
{
    private readonly UpdateHomeHandler _handler;

    public UpdateHome(UpdateHomeHandler handler)
    {
        _handler = handler ?? throw new ArgumentNullException(nameof(handler));
    }

    [Function("UpdateHome")]
    public Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Function, "put", Route = "homes/{id}")] HttpRequest req,
        string id,
        CancellationToken cancellationToken) =>
        HttpFunctionHelpers.ExecuteAsync(async () =>
        {
            var user = HttpFunctionHelpers.GetCurrentUser(req);
            var request = await HttpFunctionHelpers.ReadRequiredJsonAsync<UpdateHomeRequest>(req, cancellationToken)
                .ConfigureAwait(false);

            var result = await _handler.HandleAsync(user, id, request, cancellationToken).ConfigureAwait(false);

            return new OkObjectResult(result);
        });
}
