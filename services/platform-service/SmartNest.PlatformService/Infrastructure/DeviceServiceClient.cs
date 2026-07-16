using System.Globalization;
using System.Text;
using System.Text.Json;

namespace SmartNest.PlatformService.Infrastructure;

/// <summary>
/// <see cref="IDeviceStateClient"/> implementation - PATCHes Device Service's
/// <c>/devices/{id}/state</c> endpoint using its function key (stored in Key Vault,
/// wired via the <c>DeviceService:FunctionKey</c> app setting - see infra/main.bicep).
/// Infers the Boolean/Numeric/Text wire shape from the rule action's plain string
/// value (the domain model doesn't carry a value-type hint) - see <see cref="BuildValue"/>.
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
            Value = BuildValue(value),
        };

        var requestUri = $"{_baseUrl}/devices/{Uri.EscapeDataString(deviceId)}/state?code={Uri.EscapeDataString(_functionKey)}";
        var content = new StringContent(JsonSerializer.Serialize(body, JsonOptions), Encoding.UTF8, "application/json");

        using var response = await _httpClient.PatchAsync(requestUri, content, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
    }

    /// <summary>
    /// Infers the state value's wire shape by attempting Boolean, then Numeric, falling
    /// back to Text. The <c>Type</c> field is sent as its numeric ordinal (0/1/2) rather
    /// than a string name, since Device Service deserializes it with a plain
    /// <c>System.Text.Json</c> enum converter (no <c>JsonStringEnumConverter</c>
    /// registered) - see SmartNest.DeviceService.Domain.ValueObjects.StateValueType
    /// (<c>Boolean = 0, Numeric = 1, Text = 2</c>). The ordinal is mirrored locally here
    /// rather than referencing that project directly, since Device Service and Platform
    /// Service are independently deployed Function Apps.
    /// </summary>
    private static object BuildValue(string value)
    {
        const int boolType = 0;
        const int numericType = 1;
        const int textType = 2;

        if (bool.TryParse(value, out var boolValue))
            return new { Type = boolType, BoolValue = boolValue, NumericValue = (double?)null, StringValue = (string?)null, Unit = (string?)null };

        if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var numericValue))
            return new { Type = numericType, BoolValue = (bool?)null, NumericValue = numericValue, StringValue = (string?)null, Unit = (string?)null };

        return new { Type = textType, BoolValue = (bool?)null, NumericValue = (double?)null, StringValue = value, Unit = (string?)null };
    }
}
