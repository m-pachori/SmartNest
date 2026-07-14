using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using SmartNest.HomeService.Handlers;

namespace SmartNest.HomeService.Functions;

public sealed class DeleteHome
{
    private readonly DeleteHomeHandler _handler;

    public DeleteHome(DeleteHomeHandler handler)
    {
        _handler = handler ?? throw new ArgumentNullException(nameof(handler));
    }

    [Function("DeleteHome")]
    public Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Function, "delete", Route = "homes/{id}")] HttpRequest req,
        string id,
        CancellationToken cancellationToken) =>
        HttpFunctionHelpers.ExecuteAsync(async () =>
        {
            var user = HttpFunctionHelpers.GetCurrentUser(req);
            await _handler.HandleAsync(user, id, cancellationToken).ConfigureAwait(false);
            return new NoContentResult();
        });
}
