using Azure.Messaging.ServiceBus;
using Azure.Monitor.OpenTelemetry.Exporter;
using Azure.Storage.Blobs;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Azure.Functions.Worker.OpenTelemetry;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry;
using SmartNest.PlatformService.Events.Alert;
using SmartNest.PlatformService.Events.Automation;
using SmartNest.PlatformService.Events.Media;
using SmartNest.PlatformService.Events.Summary;
using SmartNest.PlatformService.Handlers.Alert;
using SmartNest.PlatformService.Handlers.Audit;
using SmartNest.PlatformService.Handlers.Automation;
using SmartNest.PlatformService.Handlers.Media;
using SmartNest.PlatformService.Handlers.Summary;
using SmartNest.PlatformService.Infrastructure;
using SmartNest.PlatformService.Repositories.Alert;
using SmartNest.PlatformService.Repositories.Audit;
using SmartNest.PlatformService.Repositories.Automation;
using SmartNest.PlatformService.Repositories.Media;
using SmartNest.PlatformService.Repositories.Shared;
using SmartNest.PlatformService.Repositories.Summary;
using SmartNest.Shared.Events;

var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();

if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("APPLICATIONINSIGHTS_CONNECTION_STRING")))
{
    builder.Services.AddOpenTelemetry()
        .UseFunctionsWorkerDefaults()
        .UseAzureMonitorExporter();
}

var configuration = builder.Configuration;

// ------------------------------------------------------------------
// Cosmos DB — one CosmosClient shared by every bounded context in this
// merged Function App (Automation, Alert, Audit, Summary, Media - Tasks
// 5-9). Connection details wired via Key Vault references in Function
// App settings (see infra/modules/function-app.bicep).
// ------------------------------------------------------------------
builder.Services.AddSingleton(_ =>
{
    var endpoint = configuration["Cosmos:Endpoint"]
        ?? throw new InvalidOperationException("Cosmos:Endpoint app setting is required.");
    var key = configuration["Cosmos:Key"]
        ?? throw new InvalidOperationException("Cosmos:Key app setting is required.");
    return new CosmosClient(endpoint, key);
});

var databaseName = configuration["Cosmos:DatabaseName"] ?? "smartnest-db";

// Read-only lookups against Home/Device Service's containers (ownership + media home lookup).
builder.Services.AddSingleton<IHomeOwnershipRepository>(sp =>
{
    var client = sp.GetRequiredService<CosmosClient>();
    var homesContainerName = configuration["Cosmos:HomesContainerName"] ?? "homes";
    return new CosmosHomeOwnershipRepository(client.GetContainer(databaseName, homesContainerName));
});

builder.Services.AddSingleton<IDeviceHomeLookupRepository>(sp =>
{
    var client = sp.GetRequiredService<CosmosClient>();
    var devicesContainerName = configuration["Cosmos:DevicesContainerName"] ?? "devices";
    return new CosmosDeviceHomeLookupRepository(client.GetContainer(databaseName, devicesContainerName));
});

// Automation (Task 5) — "rules" container.
builder.Services.AddSingleton<IRuleRepository>(sp =>
{
    var client = sp.GetRequiredService<CosmosClient>();
    var containerName = configuration["Cosmos:RulesContainerName"] ?? "rules";
    return new CosmosRuleRepository(client.GetContainer(databaseName, containerName));
});

// Alert (Task 6) — "alerts" container.
builder.Services.AddSingleton<IAlertRepository>(sp =>
{
    var client = sp.GetRequiredService<CosmosClient>();
    var containerName = configuration["Cosmos:AlertsContainerName"] ?? "alerts";
    return new CosmosAlertRepository(client.GetContainer(databaseName, containerName));
});

// Audit (Task 7) — "audit-log" container.
builder.Services.AddSingleton<IAuditRepository>(sp =>
{
    var client = sp.GetRequiredService<CosmosClient>();
    var containerName = configuration["Cosmos:AuditLogContainerName"] ?? "audit-log";
    return new CosmosAuditRepository(client.GetContainer(databaseName, containerName));
});

// Summary (Task 8) — "summaries" container.
builder.Services.AddSingleton<ISummaryRepository>(sp =>
{
    var client = sp.GetRequiredService<CosmosClient>();
    var containerName = configuration["Cosmos:SummariesContainerName"] ?? "summaries";
    return new CosmosSummaryRepository(client.GetContainer(databaseName, containerName));
});

// Media (Task 9) — "media-metadata" container.
builder.Services.AddSingleton<IMediaMetadataRepository>(sp =>
{
    var client = sp.GetRequiredService<CosmosClient>();
    var containerName = configuration["Cosmos:MediaMetadataContainerName"] ?? "media-metadata";
    return new CosmosMediaMetadataRepository(client.GetContainer(databaseName, containerName));
});

// ------------------------------------------------------------------
// Service Bus — one connection with Listen+Send rights (namespace-wide
// PlatformServiceSendListen rule, see infra/modules/service-bus.bicep),
// since this merged app both consumes (Automation/Alert/Audit
// subscriptions) and publishes (AutomationExecuted/AlertRaised/
// SummaryGenerated/DocumentProcessed) across bounded contexts.
// ------------------------------------------------------------------
builder.Services.AddSingleton(_ =>
{
    var connectionString = configuration["ServiceBus:ConnectionString"]
        ?? throw new InvalidOperationException("ServiceBus:ConnectionString app setting is required.");
    return new ServiceBusClient(connectionString);
});

builder.Services.AddSingleton<IEventPublisher, ServiceBusEventPublisher>();
builder.Services.AddSingleton<AutomationEventPublisher>();
builder.Services.AddSingleton<AlertEventPublisher>();
builder.Services.AddSingleton<SummaryEventPublisher>();
builder.Services.AddSingleton<MediaEventPublisher>();

// ------------------------------------------------------------------
// Blob Storage (Task 9) — reuses the shared platform storage account via
// AzureWebJobsStorage (already a plain value the Functions host requires
// at startup - see infra/modules/function-app.bicep's storageConnectionString comment).
// ------------------------------------------------------------------
builder.Services.AddSingleton(_ =>
{
    var storageConnectionString = configuration["AzureWebJobsStorage"]
        ?? throw new InvalidOperationException("AzureWebJobsStorage app setting is required.");
    return new BlobServiceClient(storageConnectionString);
});
builder.Services.AddSingleton<IBlobStorageClientFactory, BlobStorageClientFactory>();

// ------------------------------------------------------------------
// Device Service HTTP client (Automation's "ChangeDeviceState" rule action -
// see infra/main.bicep's DeviceService:BaseUrl/FunctionKey app settings).
// ------------------------------------------------------------------
builder.Services.AddHttpClient();
builder.Services.AddSingleton<IDeviceStateClient>(sp =>
{
    var httpClient = sp.GetRequiredService<IHttpClientFactory>().CreateClient();
    var baseUrl = configuration["DeviceService:BaseUrl"]
        ?? throw new InvalidOperationException("DeviceService:BaseUrl app setting is required.");
    var functionKey = configuration["DeviceService:FunctionKey"]
        ?? throw new InvalidOperationException("DeviceService:FunctionKey app setting is required.");
    return new DeviceServiceClient(httpClient, baseUrl, functionKey);
});

builder.Services.AddSingleton<INotificationSender, LoggingNotificationSender>();

// ------------------------------------------------------------------
// Handlers (Scoped — new instance per invocation, mirrors Home/Device/Identity Service).
// ------------------------------------------------------------------
// Automation (Task 5)
builder.Services.AddScoped<CreateRuleHandler>();
builder.Services.AddScoped<GetRuleHandler>();
builder.Services.AddScoped<UpdateRuleHandler>();
builder.Services.AddScoped<DeleteRuleHandler>();
builder.Services.AddScoped<EvaluateRulesHandler>();

// Alert (Task 6)
builder.Services.AddScoped<CreateAlertHandler>();
builder.Services.AddScoped<DispatchAlertsHandler>();
builder.Services.AddScoped<GetAlertsHandler>();
builder.Services.AddScoped<AcknowledgeAlertHandler>();

// Audit (Task 7)
builder.Services.AddScoped<AppendAuditLogHandler>();
builder.Services.AddScoped<GetAuditLogHandler>();
builder.Services.AddScoped<ReplayEventsHandler>();

// Summary (Task 8)
builder.Services.AddScoped<GenerateDailySummaryHandler>();
builder.Services.AddScoped<GetDailySummaryHandler>();

// Media (Task 9)
builder.Services.AddScoped(sp => new UploadMediaHandler(
    sp.GetRequiredService<IBlobStorageClientFactory>(),
    configuration["Storage:MediaUploadsContainerName"] ?? "media-uploads"));
builder.Services.AddScoped(sp => new ProcessMediaHandler(
    sp.GetRequiredService<IBlobStorageClientFactory>(),
    sp.GetRequiredService<IMediaMetadataRepository>(),
    sp.GetRequiredService<IDeviceHomeLookupRepository>(),
    sp.GetRequiredService<MediaEventPublisher>(),
    configuration["Storage:MediaUploadsContainerName"] ?? "media-uploads",
    configuration["Storage:ProcessedMediaContainerName"] ?? "processed-media"));

builder.Build().Run();
