using System.Diagnostics.Metrics;

namespace SmartNest.DeviceService.Telemetry;

/// <summary>
/// Custom OpenTelemetry metrics for the Device Service, exported via the Azure Monitor
/// OpenTelemetry Exporter already wired in Program.cs - not the classic Application
/// Insights <c>TelemetryClient</c>, to stay consistent with the platform's OpenTelemetry
/// based Functions Worker instrumentation.
/// </summary>
public static class DeviceMetrics
{
    public const string MeterName = "SmartNest.DeviceService";

    private static readonly Meter Meter = new(MeterName);

    /// <summary>The `device.state.changes` custom metric (smartnest-plan.md Observability Strategy).</summary>
    public static readonly Counter<long> StateChanges = Meter.CreateCounter<long>("device.state.changes");
}
