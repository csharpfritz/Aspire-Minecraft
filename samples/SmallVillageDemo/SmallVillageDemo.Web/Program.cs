var builder = WebApplication.CreateBuilder(args);

var app = builder.Build();

app.MapGet("/", () => Results.Content("""
<!DOCTYPE html>
<html>
<head><title>Small Village Demo</title></head>
<body>
    <h1>🏘️ Small Village Demo</h1>
    <p>A minimal web app — one of two monitored resources in the Minecraft world.</p>
</body>
</html>
""", "text/html"));

app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }));

app.Run();
