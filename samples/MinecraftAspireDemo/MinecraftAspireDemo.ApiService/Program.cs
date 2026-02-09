var builder = WebApplication.CreateBuilder(args);

var app = builder.Build();

app.MapGet("/", () => "Minecraft Aspire Demo API");

app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }));

app.MapGet("/api/status", () => new
{
    Service = "MinecraftAspireDemo.ApiService",
    Status = "Running",
    Timestamp = DateTime.UtcNow,
    Environment = app.Environment.EnvironmentName
});

app.Run();
