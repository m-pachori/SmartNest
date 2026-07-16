using System.Net;
using System.Text.Json;
using FluentAssertions;
using SmartNest.PlatformService.Infrastructure;

namespace SmartNest.PlatformService.Tests.Infrastructure;

public class DeviceServiceClientTests
{
    private sealed class CapturingHandler : HttpMessageHandler
    {
        public string? CapturedBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CapturedBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK);
        }
    }

    private static (DeviceServiceClient client, CapturingHandler handler) MakeClient()
    {
        var handler = new CapturingHandler();
        var httpClient = new HttpClient(handler);
        var client = new DeviceServiceClient(httpClient, "https://device-svc.example.com/api", "test-key");
        return (client, handler);
    }

    [Fact]
    public async Task UpdateStateAsync_SendsBooleanShape_ForBooleanLikeValue()
    {
        var (client, handler) = MakeClient();

        await client.UpdateStateAsync("device-1", "power", "true");

        using var body = JsonDocument.Parse(handler.CapturedBody!);
        var value = body.RootElement.GetProperty("value");
        value.GetProperty("type").GetInt32().Should().Be(0);
        value.GetProperty("boolValue").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task UpdateStateAsync_SendsNumericShape_ForNumericValue()
    {
        var (client, handler) = MakeClient();

        await client.UpdateStateAsync("device-1", "temperature", "22.5");

        using var body = JsonDocument.Parse(handler.CapturedBody!);
        var value = body.RootElement.GetProperty("value");
        value.GetProperty("type").GetInt32().Should().Be(1);
        value.GetProperty("numericValue").GetDouble().Should().Be(22.5);
    }

    [Fact]
    public async Task UpdateStateAsync_SendsTextShape_ForNonBooleanNonNumericValue()
    {
        var (client, handler) = MakeClient();

        await client.UpdateStateAsync("device-1", "mode", "eco");

        using var body = JsonDocument.Parse(handler.CapturedBody!);
        var value = body.RootElement.GetProperty("value");
        value.GetProperty("type").GetInt32().Should().Be(2);
        value.GetProperty("stringValue").GetString().Should().Be("eco");
    }
}
