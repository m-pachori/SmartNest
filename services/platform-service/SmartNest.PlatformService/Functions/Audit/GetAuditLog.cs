using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using SmartNest.PlatformService.Handlers.Audit;
using SmartNest.Shared.Security;

namespace SmartNest.PlatformService.Functions.Audit;

public sealed class GetAuditLog
{
    private readonly GetAuditLogHandler _handler;
    private readonly IJwtValidator _jwtValidator;

    public GetAuditLog(GetAuditLogHandler handler, IJwtValidator jwtValidator)
    {
        _handler = handler ?? throw new ArgumentNullException(nameof(handler));
        _jwtValidator = jwtValidator ?? throw new ArgumentNullException(nameof(jwtValidator));
    }

    [Function("GetAuditLog")]
    public Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "audit/{aggregateId}")] HttpRequest req,
        string aggregateId,
        CancellationToken cancellationToken) =>
        HttpFunctionHelpers.ExecuteAsync(async () =>
        {
            var user = await HttpFunctionHelpers.GetCurrentUserAsync(req, _jwtValidator, cancellationToken).ConfigureAwait(false);
            var fromSequence = int.TryParse(req.Query["from"].FirstOrDefault(), out var parsed) ? parsed : 0;

            var result = await _handler.HandleAsync(user, aggregateId, fromSequence, cancellationToken).ConfigureAwait(false);

            return new OkObjectResult(result);
        });
}
