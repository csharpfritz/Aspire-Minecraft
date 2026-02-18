# Aspire Minecraft Server — Pre-baked Docker Image

A turnkey Docker image based on `itzg/minecraft-server:latest`. All plugins, server
properties, and configuration are baked in so that `docker run` immediately spawns a
fully configured Minecraft server — no extra setup required.

## What's Included

| Category | Detail |
|----------|--------|
| **Base image** | `itzg/minecraft-server:latest` (Paper) |
| **Server type** | Paper (creative mode, flat world) |
| **RCON** | Enabled on port 25575 (password must be supplied at runtime) |
| **BlueMap plugin** | Pre-installed via Modrinth, web UI on port 8100 |
| **BlueMap config** | `accept-download: true` baked into `/plugins/BlueMap/core.conf` |
| **Image size** | ~868 MB |

### Baked-in Environment Variables

These mirror `MinecraftServerBuilderExtensions.cs` and can all be overridden at
runtime with `docker run -e`:

| Variable | Default | Notes |
|----------|---------|-------|
| `ASPIRE_MINECRAFT_PREBAKED` | `true` | Hosting extension marker |
| `EULA` | `TRUE` | Mojang EULA acceptance |
| `TYPE` | `PAPER` | Server distribution |
| `ONLINE_MODE` | `FALSE` | Offline/LAN mode |
| `MODE` | `creative` | Game mode |
| `LEVEL_TYPE` | `flat` | Flat world generation |
| `ENABLE_RCON` | `true` | Remote console |
| `RCON_PORT` | `25575` | RCON listen port |
| `SPAWN_PROTECTION` | `0` | No spawn protection |
| `VIEW_DISTANCE` | `12` | Chunk view distance |
| `SIMULATION_DISTANCE` | `8` | Chunk sim distance |
| `GENERATE_STRUCTURES` | `false` | No villages/temples |
| `SPAWN_ANIMALS` | `FALSE` | No animal spawning |
| `SPAWN_MONSTERS` | `FALSE` | No hostile mobs |
| `SPAWN_NPCS` | `FALSE` | No villager spawning |
| `MAX_WORLD_SIZE` | `29999984` | World border |
| `MODRINTH_PROJECTS` | `bluemap` | Auto-installed plugins |

### NOT Baked In (Runtime Only)

| Variable | Reason |
|----------|--------|
| `RCON_PASSWORD` | Security — must be supplied per-instance |
| `SEED` | Project-specific world seed |

## Usage

### With Aspire (recommended)

```csharp
builder.AddMinecraftServer("minecraft")
    .WithImage("ghcr.io/csharpfritz/aspire-minecraft-server", "latest");
```

The hosting extension detects `ASPIRE_MINECRAFT_PREBAKED=true` and skips
redundant plugin downloads.

### Standalone Docker

```bash
docker run --rm -it \
  -p 25565:25565 \
  -p 25575:25575 \
  -p 8100:8100 \
  -e RCON_PASSWORD=changeme \
  ghcr.io/csharpfritz/aspire-minecraft-server:latest
```

Ports exposed:
- **25565** — Minecraft game
- **25575** — RCON management
- **8100** — BlueMap web UI

## Building Locally

```bash
docker build -t aspire-minecraft-server:dev docker/
```

Then smoke-test:

```bash
docker run --rm -d --name mc-test \
  -p 25565:25565 -p 25575:25575 -p 8100:8100 \
  -e RCON_PASSWORD=test123 \
  aspire-minecraft-server:dev

# Wait ~30-60 seconds, then:
docker logs mc-test          # look for "RCON running" and "Done"
docker stop mc-test
```

## Relationship to itzg/minecraft-server

This image extends `itzg/minecraft-server:latest` with environment variables
and plugin configuration baked in at build time. The itzg image's env-var
convention means every baked-in value can be overridden at runtime — the
Dockerfile sets sensible defaults, not hard constraints.

## Future Enhancements

- **DecentHolograms** — Hologram rendering plugin
- **OpenTelemetry Agent** — Java agent for distributed tracing

## Image Tags

- `latest` — Latest build from main branch
- `v*` — Release versions (e.g., `v0.2.0`) matching GitHub releases
- `<commit-sha>` — Git commit SHA for pinning specific builds
