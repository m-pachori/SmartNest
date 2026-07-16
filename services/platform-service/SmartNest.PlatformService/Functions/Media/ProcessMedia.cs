using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using SmartNest.PlatformService.Handlers.Media;

namespace SmartNest.PlatformService.Functions.Media;

/// <summary>
/// Blob trigger (Task 9) - the sole processing path; fires automatically after
/// <see cref="UploadMedia"/> writes to <c>media-uploads/{name}</c>.
/// </summary>
public sealed class ProcessMedia
{
    private readonly ProcessMediaHandler _handler;
    private readonly ILogger<ProcessMedia> _logger;

    public ProcessMedia(ProcessMediaHandler handler, ILogger<ProcessMedia> logger)
    {
        _handler = handler ?? throw new ArgumentNullException(nameof(handler));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [Function("ProcessMedia")]
    public async Task Run(
        [BlobTrigger("media-uploads/{name}", Connection = "AzureWebJobsStorage")] Stream content,
        string name,
        CancellationToken cancellationToken)
    {
        try
        {
            await _handler.HandleAsync(name, content, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process uploaded blob '{BlobName}'.", name);
            throw;
        }
    }
}
