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
using SmartNest.HomeService.Events;
using SmartNest.HomeService.Handlers;
using SmartNest.HomeService.Repositories;
using SmartNest.Shared.Events;
using SmartNest.Shared.Security;

var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();

if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("APPLICATIONINSIGHTS_CONNECTION_STRING")))
{
    builder.Services.AddOpenTelemetry()
        .UseFunctionsWorkerDefaults()
        .UseAzureMonitorExporter();
}

var configuration = builder.Configuration;

// JWT validation - validates Entra ID token signature/issuer/audience/expiry against the
// tenant's live JWKS (see SmartNest.Shared.Security.EntraJwtValidator). Function-level
// defence-in-depth on top of APIM's validate-jwt policy (infra/modules/apim.bicep) -
// required while deployApim is disabled (infra/main.bicep).
builder.Services.AddSingleton(_ => new JwtValidationOptions
{
    TenantId = configuration["Auth:TenantId"]
        ?? throw new InvalidOperationException("Auth:TenantId app setting is required."),
    Audience = configuration["Auth:Audience"]
        ?? throw new InvalidOperationException("Auth:Audience app setting is required."),
});
builder.Services.AddSingleton<IJwtValidator, EntraJwtValidator>();

// Cosmos DB — connection details wired via Key Vault references in Function App settings
// (see infra/modules/function-app.bicep). Container: "homes", partition key: "/homeId".
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
    var containerName = configuration["Cosmos:HomesContainerName"] ?? "homes";
    return client.GetContainer(databaseName, containerName);
});

// Service Bus — publishes to the "home-events" topic.
builder.Services.AddSingleton(_ =>
{
    var connectionString = configuration["ServiceBus:ConnectionString"]
        ?? throw new InvalidOperationException("ServiceBus:ConnectionString app setting is required.");
    return new ServiceBusClient(connectionString);
});

builder.Services.AddSingleton<IEventPublisher, ServiceBusEventPublisher>();
builder.Services.AddSingleton<IHomeRepository, CosmosHomeRepository>();
builder.Services.AddSingleton<HomeEventPublisher>();

builder.Services.AddScoped<CreateHomeHandler>();
builder.Services.AddScoped<GetHomeHandler>();
builder.Services.AddScoped<UpdateHomeHandler>();
builder.Services.AddScoped<DeleteHomeHandler>();
builder.Services.AddScoped<AddRoomHandler>();
builder.Services.AddScoped<RemoveRoomHandler>();

builder.Build().Run();
