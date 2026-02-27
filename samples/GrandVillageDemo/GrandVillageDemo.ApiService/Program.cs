var builder = WebApplication.CreateBuilder(args);

var app = builder.Build();

var isHealthy = true;

app.MapGet("/", () => "Grand Village Demo API");

app.MapGet("/health", () => isHealthy
    ? Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow })
    : Results.Json(new { status = "unhealthy", timestamp = DateTime.UtcNow }, statusCode: 503));

// Called by the Aspire dashboard "Trigger Error" command to simulate a failure.
// Sets the health check to unhealthy so the Minecraft worker detects it and spawns an error boat.
app.MapPost("/trigger-error", () =>
{
    isHealthy = false;
    return Results.Ok(new { message = "Error triggered — API service is now unhealthy" });
});

app.Run();
