using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using SmartNest.HomeService.Handlers;

namespace SmartNest.HomeService.Functions;

public sealed class RemoveRoom
{
    private readonly RemoveRoomHandler _handler;

    public RemoveRoom(RemoveRoomHandler handler)
    {
        _handler = handler ?? throw new ArgumentNullException(nameof(handler));
    }

    [Function("RemoveRoom")]
    public Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Function, "delete", Route = "homes/{id}/rooms/{roomId}")] HttpRequest req,
        string id,
        string roomId,
        CancellationToken cancellationToken) =>
        HttpFunctionHelpers.ExecuteAsync(async () =>
        {
            var user = HttpFunctionHelpers.GetCurrentUser(req);
            await _handler.HandleAsync(user, id, roomId, cancellationToken).ConfigureAwait(false);
            return new NoContentResult();
        });
}
