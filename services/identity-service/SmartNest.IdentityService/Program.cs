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
using SmartNest.IdentityService.Events;
using SmartNest.IdentityService.Handlers;
using SmartNest.IdentityService.Repositories;
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

// Cosmos DB — connection details wired via Key Vault references in Function App settings
// (see infra/modules/function-app.bicep). Container: "users", partition key: "/homeId".
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
    var containerName = configuration["Cosmos:UsersContainerName"] ?? "users";
    return client.GetContainer(databaseName, containerName);
});

// Read-only lookup against the shared "homes" container - used to verify the caller
// owns the target home (AuthorizationGuard.RequireOwnership). Constructed directly from
// CosmosClient (not a second Container DI registration) to avoid colliding with the
// "users" Container registered above.
builder.Services.AddSingleton<IHomeOwnershipRepository>(sp =>
{
    var client = sp.GetRequiredService<CosmosClient>();
    var databaseName = configuration["Cosmos:DatabaseName"] ?? "smartnest-db";
    var homesContainerName = configuration["Cosmos:HomesContainerName"] ?? "homes";
    return new CosmosHomeOwnershipRepository(client.GetContainer(databaseName, homesContainerName));
});

// Service Bus — publishes to the "user-events" topic using the least-privilege
// IdentityServiceSend (send-only) connection string (see infra/main.bicep).
builder.Services.AddSingleton(_ =>
{
    var connectionString = configuration["ServiceBus:ConnectionString"]
        ?? throw new InvalidOperationException("ServiceBus:ConnectionString app setting is required.");
    return new ServiceBusClient(connectionString);
});

builder.Services.AddSingleton<IEventPublisher, ServiceBusEventPublisher>();
builder.Services.AddSingleton<IIdentityRepository, CosmosIdentityRepository>();
builder.Services.AddSingleton<IdentityEventPublisher>();

builder.Services.AddScoped<InviteUserHandler>();
builder.Services.AddScoped<UpdateUserRoleHandler>();
builder.Services.AddScoped<RemoveUserHandler>();

builder.Build().Run();
