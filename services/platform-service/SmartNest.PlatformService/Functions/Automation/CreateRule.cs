using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using SmartNest.PlatformService.Dtos.Automation;
using SmartNest.PlatformService.Handlers.Automation;

namespace SmartNest.PlatformService.Functions.Automation;

public sealed class CreateRule
{
    private readonly CreateRuleHandler _handler;

    public CreateRule(CreateRuleHandler handler)
    {
        _handler = handler ?? throw new ArgumentNullException(nameof(handler));
    }

    [Function("CreateRule")]
    public Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "homes/{homeId}/rules")] HttpRequest req,
        string homeId,
        CancellationToken cancellationToken) =>
        HttpFunctionHelpers.ExecuteAsync(async () =>
        {
            var user = HttpFunctionHelpers.GetCurrentUser(req);
            var request = await HttpFunctionHelpers.ReadRequiredJsonAsync<CreateRuleRequest>(req, cancellationToken)
                .ConfigureAwait(false);

            var result = await _handler.HandleAsync(user, homeId, request, cancellationToken).ConfigureAwait(false);

            return new ObjectResult(result) { StatusCode = StatusCodes.Status201Created };
        });
}
