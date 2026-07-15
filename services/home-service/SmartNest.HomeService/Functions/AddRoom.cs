using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using SmartNest.HomeService.Dtos;
using SmartNest.HomeService.Handlers;

namespace SmartNest.HomeService.Functions;

public sealed class AddRoom
{
    private readonly AddRoomHandler _handler;

    public AddRoom(AddRoomHandler handler)
    {
        _handler = handler ?? throw new ArgumentNullException(nameof(handler));
    }

    [Function("AddRoom")]
    public Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "homes/{id}/rooms")] HttpRequest req,
        string id,
        CancellationToken cancellationToken) =>
        HttpFunctionHelpers.ExecuteAsync(async () =>
        {
            var user = HttpFunctionHelpers.GetCurrentUser(req);
            var request = await HttpFunctionHelpers.ReadRequiredJsonAsync<AddRoomRequest>(req, cancellationToken)
                .ConfigureAwait(false);

            var result = await _handler.HandleAsync(user, id, request, cancellationToken).ConfigureAwait(false);

            return new ObjectResult(result) { StatusCode = StatusCodes.Status201Created };
        });
}
