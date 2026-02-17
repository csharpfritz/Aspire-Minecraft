# Aspire Minecraft Server Docker Image

A custom Docker image based on `itzg/minecraft-server:latest` with pre-baked Minecraft plugins for faster, deterministic startup in Aspire applications.

## What's Included

- **Base Image:** `itzg/minecraft-server:latest` (Paper server)
- **Pre-Installed Plugins:**
  - **BlueMap** — Web-based 3D world map UI (accessible on port 8100)

## Usage

### With Aspire

In your Aspire app host, configure the server to use the prebaked image:

```csharp
builder.AddMinecraftServer("minecraft")
    .WithImage("ghcr.io/csharpfritz/aspire-minecraft-server", "latest");
```

The hosting extension will detect `ASPIRE_MINECRAFT_PREBAKED=true` and skip redundant plugin downloads, speeding up startup time.

### With Docker

Run locally:

```bash
docker run --rm -it \
  -p 25565:25565 \
  -p 8100:8100 \
  -e EULA=TRUE \
  ghcr.io/csharpfritz/aspire-minecraft-server:latest
```

## Building Locally

```bash
cd docker
docker build -t aspire-minecraft-server:dev .
```

Then test:

```bash
docker run --rm -it -p 25565:25565 -e EULA=TRUE aspire-minecraft-server:dev
```

## Relationship to itzg/minecraft-server

This image extends `itzg/minecraft-server:latest` with environment variables that pre-download and configure plugins at build time rather than at container startup. This reduces startup latency and ensures consistent plugin versions across deployments.

## Future Enhancements

The Dockerfile is designed for incremental plugin additions:

- **DecentHolograms** — Plugin for hologram rendering
- **OpenTelemetry Agent** — Java agent for distributed tracing

## Image Tags

- `latest` — Latest build from main branch
- `v*` — Release versions (e.g., `v0.2.0`) matching GitHub releases
- `<commit-sha>` — Git commit SHA for debugging specific builds
