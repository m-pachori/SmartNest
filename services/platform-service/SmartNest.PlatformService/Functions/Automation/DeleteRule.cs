using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using SmartNest.PlatformService.Handlers.Automation;
using SmartNest.Shared.Security;

namespace SmartNest.PlatformService.Functions.Automation;

public sealed class DeleteRule
{
    private readonly DeleteRuleHandler _handler;
    private readonly IJwtValidator _jwtValidator;

    public DeleteRule(DeleteRuleHandler handler, IJwtValidator jwtValidator)
    {
        _handler = handler ?? throw new ArgumentNullException(nameof(handler));
        _jwtValidator = jwtValidator ?? throw new ArgumentNullException(nameof(jwtValidator));
    }

    [Function("DeleteRule")]
    public Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Function, "delete", Route = "homes/{homeId}/rules/{id}")] HttpRequest req,
        string homeId,
        string id,
        CancellationToken cancellationToken) =>
        HttpFunctionHelpers.ExecuteAsync(async () =>
        {
            var user = await HttpFunctionHelpers.GetCurrentUserAsync(req, _jwtValidator, cancellationToken).ConfigureAwait(false);

            await _handler.HandleAsync(user, homeId, id, cancellationToken).ConfigureAwait(false);

            return new NoContentResult();
        });
}
