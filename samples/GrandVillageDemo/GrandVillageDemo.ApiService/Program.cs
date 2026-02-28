var builder = WebApplication.CreateBuilder(args);
builder.Services.AddHttpClient();

var app = builder.Build();

var isHealthy = true;
var errorWebhookUrl = app.Configuration["ASPIRE_MINECRAFT_ERROR_WEBHOOK"];

app.MapGet("/", () => isHealthy
    ? Results.Ok("Grand Village Demo API")
    : Results.Json(new { status = "unhealthy", message = "API service is not healthy" }, statusCode: 503));

app.MapGet("/health", () => isHealthy
    ? Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow })
    : Results.Json(new { status = "unhealthy", timestamp = DateTime.UtcNow }, statusCode: 503));

// Called by the Aspire dashboard "Trigger Error" command to simulate a failure.
// Emits an OpenTelemetry error-level log entry which the Minecraft worker detects
// and responds to by spawning a creeper boat in the canal system.
app.MapPost("/trigger-error", async (ILogger<Program> logger, IHttpClientFactory httpClientFactory) =>
{
    // Emit an OTel error-level log entry
    logger.LogError("User-triggered error on API service — simulating application failure");

    // Notify the Minecraft worker via webhook to spawn an error boat
    if (!string.IsNullOrEmpty(errorWebhookUrl))
    {
        try
        {
            var client = httpClientFactory.CreateClient();
            await client.PostAsJsonAsync(errorWebhookUrl,
                new { resourceName = "api", message = "User-triggered error", severityText = "Error" });
        }
        catch
        {
            // Best-effort notification — don't fail the trigger if worker is unreachable
        }
    }

    return Results.Ok(new { message = "Error triggered — OTel error log emitted and error boat spawned" });
});

app.Run();
