using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using SmartNest.PlatformService.Handlers.Audit;

namespace SmartNest.PlatformService.Functions.Audit;

public sealed class GetAuditLog
{
    private readonly GetAuditLogHandler _handler;

    public GetAuditLog(GetAuditLogHandler handler)
    {
        _handler = handler ?? throw new ArgumentNullException(nameof(handler));
    }

    [Function("GetAuditLog")]
    public Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "audit/{aggregateId}")] HttpRequest req,
        string aggregateId,
        CancellationToken cancellationToken) =>
        HttpFunctionHelpers.ExecuteAsync(async () =>
        {
            var user = HttpFunctionHelpers.GetCurrentUser(req);
            var fromSequence = int.TryParse(req.Query["from"].FirstOrDefault(), out var parsed) ? parsed : 0;

            var result = await _handler.HandleAsync(user, aggregateId, fromSequence, cancellationToken).ConfigureAwait(false);

            return new OkObjectResult(result);
        });
}
