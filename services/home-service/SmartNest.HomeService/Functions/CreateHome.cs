using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using SmartNest.HomeService.Dtos;
using SmartNest.HomeService.Handlers;

namespace SmartNest.HomeService.Functions;

public sealed class CreateHome
{
    private readonly CreateHomeHandler _handler;

    public CreateHome(CreateHomeHandler handler)
    {
        _handler = handler ?? throw new ArgumentNullException(nameof(handler));
    }

    [Function("CreateHome")]
    public Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "homes")] HttpRequest req,
        CancellationToken cancellationToken) =>
        HttpFunctionHelpers.ExecuteAsync(async () =>
        {
            var user = HttpFunctionHelpers.GetCurrentUser(req);
            var request = await HttpFunctionHelpers.ReadRequiredJsonAsync<CreateHomeRequest>(req, cancellationToken)
                .ConfigureAwait(false);

            var result = await _handler.HandleAsync(user, request, cancellationToken).ConfigureAwait(false);

            return new ObjectResult(result) { StatusCode = StatusCodes.Status201Created };
        });
}
