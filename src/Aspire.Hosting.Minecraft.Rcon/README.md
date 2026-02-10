# Aspire.Hosting.Minecraft.Rcon

A lightweight RCON (Remote Console) protocol client for Minecraft servers, built on .NET with auto-reconnect and response parsing.

## Features

- **Full RCON protocol** — authenticate, send commands, receive responses
- **Auto-reconnect** — resilient connection handling for long-running services
- **Response parsing** — built-in parsers for TPS, MSPT, and player data
- **Tracing** — integrates with `Microsoft.Extensions.Logging` for diagnostics

## Quick Start

```csharp
using Aspire.Hosting.Minecraft.Rcon;

var client = new RconClient("localhost", 25575, "your-rcon-password");
await client.ConnectAsync();

var response = await client.SendCommandAsync("list");
Console.WriteLine(response); // "There are 2 of a max of 20 players online: Steve, Alex"
```

## Connection String Format

When used with Aspire, the RCON connection string follows this format:

```
Host=localhost;Port=25575;Password=secret
```

## Dependencies

- `Microsoft.Extensions.Logging.Abstractions` — for structured logging support

## License

MIT — see [LICENSE](https://github.com/csharpfritz/Aspire-Minecraft) for details.
