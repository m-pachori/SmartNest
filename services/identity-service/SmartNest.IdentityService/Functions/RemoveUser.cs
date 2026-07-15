using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using SmartNest.IdentityService.Handlers;

namespace SmartNest.IdentityService.Functions;

public sealed class RemoveUser
{
    private readonly RemoveUserHandler _handler;

    public RemoveUser(RemoveUserHandler handler)
    {
        _handler = handler ?? throw new ArgumentNullException(nameof(handler));
    }

    [Function("RemoveUser")]
    public Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Function, "delete", Route = "homes/{homeId}/users/{userId}")] HttpRequest req,
        string homeId,
        string userId,
        CancellationToken cancellationToken) =>
        HttpFunctionHelpers.ExecuteAsync(async () =>
        {
            var user = HttpFunctionHelpers.GetCurrentUser(req);
            await _handler.HandleAsync(user, homeId, userId, cancellationToken).ConfigureAwait(false);
            return new NoContentResult();
        });
}
