using Azure.Messaging.ServiceBus;
using Azure.Monitor.OpenTelemetry.Exporter;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Azure.Functions.Worker.OpenTelemetry;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using SmartNest.DeviceService.Events;
using SmartNest.DeviceService.Handlers;
using SmartNest.DeviceService.Repositories;
using SmartNest.DeviceService.Telemetry;
using SmartNest.Shared.Events;

var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();

if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("APPLICATIONINSIGHTS_CONNECTION_STRING")))
{
    builder.Services.AddOpenTelemetry()
        .UseFunctionsWorkerDefaults()
        .WithMetrics(metrics => metrics.AddMeter(DeviceMetrics.MeterName))
        .UseAzureMonitorExporter();
}

var configuration = builder.Configuration;

// Cosmos DB — connection details wired via Key Vault references in Function App settings
// (see infra/modules/function-app.bicep). Container: "devices", partition key: "/homeId".
builder.Services.AddSingleton(_ =>
{
    var endpoint = configuration["Cosmos:Endpoint"]
        ?? throw new InvalidOperationException("Cosmos:Endpoint app setting is required.");
    var key = configuration["Cosmos:Key"]
        ?? throw new InvalidOperationException("Cosmos:Key app setting is required.");
    return new CosmosClient(endpoint, key);
});

builder.Services.AddSingleton(sp =>
{
    var client = sp.GetRequiredService<CosmosClient>();
    var databaseName = configuration["Cosmos:DatabaseName"] ?? "smartnest-db";
    var containerName = configuration["Cosmos:DevicesContainerName"] ?? "devices";
    return client.GetContainer(databaseName, containerName);
});

// Service Bus — publishes to the "device-events" topic using the least-privilege
// DeviceServiceSend (send-only) connection string (see infra/main.bicep).
builder.Services.AddSingleton(_ =>
{
    var connectionString = configuration["ServiceBus:ConnectionString"]
        ?? throw new InvalidOperationException("ServiceBus:ConnectionString app setting is required.");
    return new ServiceBusClient(connectionString);
});

builder.Services.AddSingleton<IEventPublisher, ServiceBusEventPublisher>();
builder.Services.AddSingleton<IDeviceRepository, CosmosDeviceRepository>();
builder.Services.AddSingleton<DeviceEventPublisher>();

builder.Services.AddScoped<RegisterDeviceHandler>();
builder.Services.AddScoped<GetDeviceHandler>();
builder.Services.AddScoped<UpdateDeviceStateHandler>();
builder.Services.AddScoped<RemoveDeviceHandler>();

builder.Build().Run();
