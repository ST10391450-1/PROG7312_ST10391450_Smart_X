using Smart_X_API.Controllers;
using Smart_X_API.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();


builder.Services.AddSingleton<SensorUnitDetectionService>();
builder.Services.AddSingleton<SensorCategoryStore>();
builder.Services.AddSingleton<TelemetryService>();
builder.Services.AddSingleton<LocationService>();
builder.Services.AddSingleton<LocationValidationService>();
builder.Services.AddScoped<CommandStreamService>();


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