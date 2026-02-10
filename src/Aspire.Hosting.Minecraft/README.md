# Aspire.Hosting.Minecraft

A [.NET Aspire](https://learn.microsoft.com/dotnet/aspire/) hosting integration that adds Minecraft Paper servers to your Aspire application with first-class dashboard support.

## Features

- **Paper server container** — runs [itzg/minecraft-server](https://hub.docker.com/r/itzg/minecraft-server) with RCON enabled
- **RCON health checks** — dependents can `WaitFor()` the Minecraft server
- **RCON protocol client** — full Source RCON implementation with auto-reconnect, response parsing for TPS/MSPT/player data, and structured logging
- **BlueMap web map** — optional plugin with HTTP endpoint visible in the Aspire dashboard
- **OpenTelemetry** — optional JVM agent that exports metrics, traces, and logs to the Aspire dashboard
- **In-world display** — optional worker that renders Aspire resource health as Minecraft structures

## Quick Start

```csharp
var builder = DistributedApplication.CreateBuilder(args);

var minecraft = builder.AddMinecraftServer("minecraft")
    .WithBlueMap()
    .WithOpenTelemetry();

builder.Build().Run();
```

## Configuration

| Method | Description |
|---|---|
| `AddMinecraftServer(name, gamePort?, rconPort?)` | Adds a Paper server container |
| `WithBlueMap(port?)` | Adds the BlueMap web map plugin |
| `WithOpenTelemetry()` | Configures JVM OpenTelemetry agent |
| `WithAspireWorldDisplay<TWorker>()` | Enables in-world health visualization |
| `WithMonitoredResource(resource)` | Registers a resource for in-world display |

## RCON Client

This package includes a built-in RCON protocol client (`Aspire.Hosting.Minecraft.Rcon` namespace):

```csharp
using Aspire.Hosting.Minecraft.Rcon;

var client = new RconClient();
await client.ConnectAsync("localhost", 25575);
await client.AuthenticateAsync("your-rcon-password");

var response = await client.SendCommandAsync("list");
```

The `RconConnection` class provides managed connections with auto-reconnect and exponential backoff. The `RconResponseParser` offers built-in parsers for TPS, MSPT, player lists, and world data.

## Package Note

This package includes the OpenTelemetry Java agent JAR (~23 MB) for bind-mounting into the Minecraft container. This is required for the `WithOpenTelemetry()` feature and contributes to the package size.

## License

MIT — see [LICENSE](https://github.com/csharpfritz/Aspire-Minecraft) for details.
