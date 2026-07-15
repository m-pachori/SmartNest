using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using SmartNest.IdentityService.Dtos;
using SmartNest.IdentityService.Handlers;

namespace SmartNest.IdentityService.Functions;

public sealed class InviteUser
{
    private readonly InviteUserHandler _handler;

    public InviteUser(InviteUserHandler handler)
    {
        _handler = handler ?? throw new ArgumentNullException(nameof(handler));
    }

    [Function("InviteUser")]
    public Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "homes/{homeId}/users/invite")] HttpRequest req,
        string homeId,
        CancellationToken cancellationToken) =>
        HttpFunctionHelpers.ExecuteAsync(async () =>
        {
            var user = HttpFunctionHelpers.GetCurrentUser(req);
            var request = await HttpFunctionHelpers.ReadRequiredJsonAsync<InviteUserRequest>(req, cancellationToken)
                .ConfigureAwait(false);

            var result = await _handler.HandleAsync(user, homeId, request, cancellationToken).ConfigureAwait(false);

            return new ObjectResult(result) { StatusCode = StatusCodes.Status201Created };
        });
}
