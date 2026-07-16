using System.Text;
using System.Text.Json;

namespace SmartNest.PlatformService.Infrastructure;

/// <summary>
/// <see cref="IDeviceStateClient"/> implementation - PATCHes Device Service's
/// <c>/devices/{id}/state</c> endpoint using its function key (stored in Key Vault,
/// wired via the <c>DeviceService:FunctionKey</c> app setting - see infra/main.bicep).
/// Always sends a Numeric state value shape since rule actions target numeric/text
/// thresholds; extend if boolean device actions are needed later.
/// </summary>
public sealed class DeviceServiceClient : IDeviceStateClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;
    private readonly string _functionKey;

    public DeviceServiceClient(HttpClient httpClient, string baseUrl, string functionKey)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        if (string.IsNullOrWhiteSpace(baseUrl))
            throw new ArgumentException("Device Service base URL is required.", nameof(baseUrl));
        if (string.IsNullOrWhiteSpace(functionKey))
            throw new ArgumentException("Device Service function key is required.", nameof(functionKey));

        _baseUrl = baseUrl.TrimEnd('/');
        _functionKey = functionKey;
    }

    public async Task UpdateStateAsync(string deviceId, string property, string value, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(deviceId))
            throw new ArgumentException("DeviceId is required.", nameof(deviceId));
        if (string.IsNullOrWhiteSpace(property))
            throw new ArgumentException("Property is required.", nameof(property));

        var body = new
        {
            Property = property,
            Value = new
            {
                Type = "Text",
                StringValue = value,
            },
        };

        var requestUri = $"{_baseUrl}/devices/{Uri.EscapeDataString(deviceId)}/state?code={Uri.EscapeDataString(_functionKey)}";
        var content = new StringContent(JsonSerializer.Serialize(body, JsonOptions), Encoding.UTF8, "application/json");

        using var response = await _httpClient.PatchAsync(requestUri, content, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
    }
}
