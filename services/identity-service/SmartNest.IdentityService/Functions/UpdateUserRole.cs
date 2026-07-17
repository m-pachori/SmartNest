using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using SmartNest.IdentityService.Dtos;
using SmartNest.IdentityService.Handlers;
using SmartNest.Shared.Security;

namespace SmartNest.IdentityService.Functions;

public sealed class UpdateUserRole
{
    private readonly UpdateUserRoleHandler _handler;
    private readonly IJwtValidator _jwtValidator;

    public UpdateUserRole(UpdateUserRoleHandler handler, IJwtValidator jwtValidator)
    {
        _handler = handler ?? throw new ArgumentNullException(nameof(handler));
        _jwtValidator = jwtValidator ?? throw new ArgumentNullException(nameof(jwtValidator));
    }

    [Function("UpdateUserRole")]
    public Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Function, "put", Route = "users/{id}/role")] HttpRequest req,
        string id,
        CancellationToken cancellationToken) =>
        HttpFunctionHelpers.ExecuteAsync(async () =>
        {
            var user = await HttpFunctionHelpers.GetCurrentUserAsync(req, _jwtValidator, cancellationToken).ConfigureAwait(false);
            var request = await HttpFunctionHelpers.ReadRequiredJsonAsync<UpdateUserRoleRequest>(req, cancellationToken)
                .ConfigureAwait(false);

            var result = await _handler.HandleAsync(user, id, request, cancellationToken).ConfigureAwait(false);

            return new OkObjectResult(result);
        });
}
