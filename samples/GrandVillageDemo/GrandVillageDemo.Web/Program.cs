var builder = WebApplication.CreateBuilder(args);

var app = builder.Build();

app.MapGet("/", () => Results.Content("""
<!DOCTYPE html>
<html>
<head><title>Grand Village Demo</title></head>
<body>
    <h1>🏰 Grand Village Demo</h1>
    <p>This web frontend produces a grand Watchtower (15×15) in the Minecraft world.</p>
    <ul>
        <li><a href="/health">Health Check</a></li>
    </ul>
</body>
</html>
""", "text/html"));

app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }));

app.Run();
