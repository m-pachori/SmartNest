using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using SmartNest.IdentityService.Dtos;
using SmartNest.IdentityService.Handlers;

namespace SmartNest.IdentityService.Functions;

public sealed class UpdateUserRole
{
    private readonly UpdateUserRoleHandler _handler;

    public UpdateUserRole(UpdateUserRoleHandler handler)
    {
        _handler = handler ?? throw new ArgumentNullException(nameof(handler));
    }

    [Function("UpdateUserRole")]
    public Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Function, "put", Route = "users/{id}/role")] HttpRequest req,
        string id,
        CancellationToken cancellationToken) =>
        HttpFunctionHelpers.ExecuteAsync(async () =>
        {
            var user = HttpFunctionHelpers.GetCurrentUser(req);
            var request = await HttpFunctionHelpers.ReadRequiredJsonAsync<UpdateUserRoleRequest>(req, cancellationToken)
                .ConfigureAwait(false);

            var result = await _handler.HandleAsync(user, id, request, cancellationToken).ConfigureAwait(false);

            return new OkObjectResult(result);
        });
}
