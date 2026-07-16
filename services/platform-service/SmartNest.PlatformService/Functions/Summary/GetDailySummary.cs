using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using SmartNest.PlatformService.Handlers.Summary;

namespace SmartNest.PlatformService.Functions.Summary;

public sealed class GetDailySummary
{
    private readonly GetDailySummaryHandler _handler;

    public GetDailySummary(GetDailySummaryHandler handler)
    {
        _handler = handler ?? throw new ArgumentNullException(nameof(handler));
    }

    [Function("GetDailySummary")]
    public Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "summaries/{homeId}")] HttpRequest req,
        string homeId,
        CancellationToken cancellationToken) =>
        HttpFunctionHelpers.ExecuteAsync(async () =>
        {
            var user = HttpFunctionHelpers.GetCurrentUser(req);
            var date = req.Query["date"].FirstOrDefault()
                ?? throw new ArgumentException("date query parameter is required (format yyyy-MM-dd).");

            var result = await _handler.HandleAsync(user, homeId, date, cancellationToken).ConfigureAwait(false);

            return new OkObjectResult(result);
        });
}
