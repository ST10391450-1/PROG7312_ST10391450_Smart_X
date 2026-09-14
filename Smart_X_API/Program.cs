using Smart_X_API.Controllers;
using Smart_X_API.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();

builder.Services.AddSingleton<TelemetryService>();
builder.Services.AddSingleton<LocationService>();
builder.Services.AddSingleton<LocationValidationService>();

var app = builder.Build();

TestDataService.Seed(
    SensorsController.Sensors,
    app.Services.GetRequiredService<LocationService>());

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