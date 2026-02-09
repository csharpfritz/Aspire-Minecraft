var builder = WebApplication.CreateBuilder(args);

var app = builder.Build();

app.MapGet("/", () => Results.Content("""
<!DOCTYPE html>
<html>
<head><title>Minecraft Aspire Demo</title></head>
<body>
    <h1>🎮 Minecraft + Aspire Demo</h1>
    <p>This web frontend is part of the Aspire-managed application.</p>
    <p>It will appear as a resource in the Minecraft world!</p>
    <ul>
        <li><a href="/health">Health Check</a></li>
    </ul>
</body>
</html>
""", "text/html"));

app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }));

app.Run();
