
using Smart_X_API.Controllers;
using Smart_X_API.Services;
using Smart_X_API.Services.Command;
using Smart_X_API.Services.Location;
using Smart_X_API.Services.Telemetry;
using Smart_X_API.Services.Sensors;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();

builder.Services.AddSingleton<SensorUnitDetectionService>();
builder.Services.AddSingleton<SensorCategoryStore>();
builder.Services.AddSingleton<TelemetryService>();
builder.Services.AddSingleton<LocationService>();
builder.Services.AddSingleton<LocationValidationService>();

builder.Services.AddScoped<CommandStreamService>();

builder.Services.AddSingleton<DeviceLookupService>();
builder.Services.AddSingleton<TelemetryQueueService>();
builder.Services.AddSingleton<AlertTrackingService>();
builder.Services.AddSingleton<CommandHistoryService>();
builder.Services.AddSingleton<PredictiveActionService>();

builder.Services.AddSingleton<TelemetryProcessingQueue>();
builder.Services.AddSingleton<AlertDeduplicationService>();

/*builder.Services.AddSingleton<NetworkSimulationService>();

builder.Services.AddHostedService(provider =>
    provider.GetRequiredService<NetworkSimulationService>());

builder.Services.AddSingleton<INetworkSimulationService>(provider =>
    provider.GetRequiredService<NetworkSimulationService>());
*/
builder.Services.AddScoped<
    INetworkTraversalService,
    NetworkTraversalService>();

builder.Services.AddScoped<
    INetworkReachabilityService,
    NetworkReachabilityService>();

builder.Services.AddScoped<
    INetworkDisconnectedService,
    NetworkDisconnectedService>();

builder.Services.AddSingleton<
    INetworkConnectionService,
    NetworkConnectionService>();

builder.Services.AddScoped<
    INetworkTopologyService,
    NetworkTopologyService>();

builder.Services.AddScoped<
    INetworkGraphService,
    NetworkGraphService>();

builder.Services.AddScoped<
    INetworkRoutingService,
    NetworkRoutingService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseAuthorization();

app.MapControllers();

app.MapGet("/api/health", () => Results.Ok(new
{
    status = "Healthy",
    service = "Smart X API"
}));

app.Run();
