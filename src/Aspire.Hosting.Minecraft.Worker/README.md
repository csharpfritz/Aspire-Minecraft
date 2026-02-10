# Aspire.Hosting.Minecraft.Worker

A background worker service that renders .NET Aspire resource health as in-world Minecraft structures, holograms, and scoreboards via RCON.

## Features

- **Health visualization** — each monitored Aspire resource gets a colored cube in the Minecraft world (green = healthy, red = unhealthy)
- **Holograms** — floating text labels above each structure showing resource name and status (requires DecentHolograms plugin)
- **Scoreboards** — real-time sidebar displaying all resource health at a glance
- **Auto-discovery** — resources registered with `WithMonitoredResource()` are automatically tracked

## Usage

This package is typically consumed indirectly through `Aspire.Hosting.Minecraft`:

```csharp
var minecraft = builder.AddMinecraftServer("minecraft")
    .WithAspireWorldDisplay<Projects.MinecraftAspireDemo_Worker>()
    .WithMonitoredResource(apiService)
    .WithMonitoredResource(webFrontend);
```

The worker connects to the Minecraft server via RCON and periodically updates in-world structures based on each resource's health status.

## Dependencies

- `Microsoft.Extensions.Hosting` — background service host
- `Microsoft.Extensions.Http` — HTTP client for health polling
- `OpenTelemetry.Extensions.Hosting` / `OpenTelemetry.Exporter.OpenTelemetryProtocol` — telemetry export

## License

MIT — see [LICENSE](https://github.com/csharpfritz/Aspire-Minecraft) for details.
