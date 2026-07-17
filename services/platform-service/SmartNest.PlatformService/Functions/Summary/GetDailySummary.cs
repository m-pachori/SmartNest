using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using SmartNest.PlatformService.Handlers.Summary;
using SmartNest.Shared.Security;

namespace SmartNest.PlatformService.Functions.Summary;

public sealed class GetDailySummary
{
    private readonly GetDailySummaryHandler _handler;
    private readonly IJwtValidator _jwtValidator;

    public GetDailySummary(GetDailySummaryHandler handler, IJwtValidator jwtValidator)
    {
        _handler = handler ?? throw new ArgumentNullException(nameof(handler));
        _jwtValidator = jwtValidator ?? throw new ArgumentNullException(nameof(jwtValidator));
    }

    [Function("GetDailySummary")]
    public Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "summaries/{homeId}")] HttpRequest req,
        string homeId,
        CancellationToken cancellationToken) =>
        HttpFunctionHelpers.ExecuteAsync(async () =>
        {
            var user = await HttpFunctionHelpers.GetCurrentUserAsync(req, _jwtValidator, cancellationToken).ConfigureAwait(false);
            var date = req.Query["date"].FirstOrDefault()
                ?? throw new ArgumentException("date query parameter is required (format yyyy-MM-dd).");

            var result = await _handler.HandleAsync(user, homeId, date, cancellationToken).ConfigureAwait(false);

            return new OkObjectResult(result);
        });
}
