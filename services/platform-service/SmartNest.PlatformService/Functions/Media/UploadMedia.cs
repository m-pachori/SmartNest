using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using SmartNest.PlatformService.Handlers.Media;

namespace SmartNest.PlatformService.Functions.Media;

public sealed class UploadMedia
{
    private readonly UploadMediaHandler _handler;

    public UploadMedia(UploadMediaHandler handler)
    {
        _handler = handler ?? throw new ArgumentNullException(nameof(handler));
    }

    [Function("UploadMedia")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "media")] HttpRequest req,
        CancellationToken cancellationToken)
    {
        // Authenticated caller identity isn't needed for the write-only upload step (the
        // deviceId in the query string scopes the blob path) - kept function-key
        // protected like every other endpoint in this app.
        var deviceId = req.Query["deviceId"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(deviceId))
            return new ObjectResult(new { error = "deviceId query parameter is required." }) { StatusCode = StatusCodes.Status400BadRequest };

        var contentType = req.ContentType ?? string.Empty;

        try
        {
            var result = await _handler
                .HandleAsync(deviceId, contentType, req.ContentLength, req.Body, cancellationToken)
                .ConfigureAwait(false);

            return new ObjectResult(result) { StatusCode = StatusCodes.Status202Accepted };
        }
        catch (UploadMediaHandler.PayloadTooLargeException ex)
        {
            return new ObjectResult(new { error = ex.Message }) { StatusCode = StatusCodes.Status413PayloadTooLarge };
        }
        catch (UploadMediaHandler.LengthRequiredException ex)
        {
            return new ObjectResult(new { error = ex.Message }) { StatusCode = StatusCodes.Status411LengthRequired };
        }
        catch (ArgumentException ex)
        {
            return new ObjectResult(new { error = ex.Message }) { StatusCode = StatusCodes.Status400BadRequest };
        }
    }
}
