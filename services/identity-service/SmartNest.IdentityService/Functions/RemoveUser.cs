using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using SmartNest.IdentityService.Handlers;
using SmartNest.Shared.Security;

namespace SmartNest.IdentityService.Functions;

public sealed class RemoveUser
{
    private readonly RemoveUserHandler _handler;
    private readonly IJwtValidator _jwtValidator;

    public RemoveUser(RemoveUserHandler handler, IJwtValidator jwtValidator)
    {
        _handler = handler ?? throw new ArgumentNullException(nameof(handler));
        _jwtValidator = jwtValidator ?? throw new ArgumentNullException(nameof(jwtValidator));
    }

    [Function("RemoveUser")]
    public Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Function, "delete", Route = "homes/{homeId}/users/{userId}")] HttpRequest req,
        string homeId,
        string userId,
        CancellationToken cancellationToken) =>
        HttpFunctionHelpers.ExecuteAsync(async () =>
        {
            var user = await HttpFunctionHelpers.GetCurrentUserAsync(req, _jwtValidator, cancellationToken).ConfigureAwait(false);
            await _handler.HandleAsync(user, homeId, userId, cancellationToken).ConfigureAwait(false);
            return new NoContentResult();
        });
}
