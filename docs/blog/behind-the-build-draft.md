# Behind the Build: How Fritz.Aspire.Hosting.Minecraft Works

> **Status:** Draft outline + opening paragraphs  
> **Author:** Jeffrey T. Fritz  
> **Target:** Post-v0.1.0 deep-dive for the .NET dev blog / personal blog  
> **Audience:** .NET developers curious about the architecture, Aspire resource model, and RCON protocol

---

## Outline

### 1. Introduction — Why This Exists
- The origin story: "What if Aspire resources were Minecraft structures?"
- The gap between dashboards and *feeling* system state
- Setting the scope: this post is the technical how, not the marketing what

### 2. The RCON Protocol — Talking to Minecraft from .NET
- What RCON is: Valve's Source RCON protocol, adopted by Minecraft
- Packet structure: 4-byte length, 4-byte request ID, 4-byte type, payload, null terminators
- Authentication handshake: type 3 (login) → type 2 (auth response)
- Command execution: type 2 (command) → type 0 (response)
- Challenges: fragmented responses, max packet size (4096 bytes), no async notification
- Our implementation: `RconClient` and `RconConnection` — TCP socket, manual framing, request/response correlation
- Parsing commands: `/tps`, `/mspt`, `/list` — regex-based response parsers (`TpsResult`, `MsptResult`, `PlayerListResult`)
- Why not a plugin? RCON requires zero server-side modifications — the integration stays non-invasive

### 3. The .NET Aspire Resource Model — Making Minecraft a First-Class Citizen
- Aspire's `IResourceBuilder<T>` pattern and how `MinecraftServerResource` implements it
- Container resource configuration: `itzg/minecraft-server` image, environment variables, port mappings, volume mounts
- The extension method chain: `AddMinecraftServer()` → `WithBlueMap()` → `WithOpenTelemetry()` → `WithAspireWorldDisplay<T>()`
- How `WithMonitoredResource()` injects environment variables for resource discovery
- Health check integration: `MinecraftHealthCheck` using RCON connection as the health probe
- Child resource pattern: the worker is added as a child of the Minecraft resource, not a sibling

### 4. The Worker Service — The Brain of the Operation
- Architecture: a standard .NET `BackgroundService` with `IHostedService` lifecycle
- Resource discovery: reading `ASPIRE_RESOURCE_*` environment variables at startup
- Health polling loop: HTTP for web services, TCP for databases/caches
- RCON command dispatch: translating health ratios into Minecraft commands
- Feature opt-in pattern: `ASPIRE_FEATURE_{NAME}` environment variables → conditional service registration
- Dependency injection: feature services (weather, boss bar, particles, titles, sounds) as nullable constructor parameters
- State tracking: `_lastWeather`, `_lastValue`, `_lastColor` — only send commands on state transitions

### 5. Feature Deep-Dives
- **Weather Effects:** mapping health ratio → `/weather clear|rain|thunder` with hysteresis
- **Boss Bar:** `/bossbar add`, `/bossbar set value`, `/bossbar set color` — the lifecycle of a persistent UI element
- **Title Alerts:** `/title @a title` with JSON text components for color and formatting
- **Sound Effects:** `/playsound` with Minecraft sound event names and positional audio
- **Particle Effects:** `/particle` with world coordinates calculated from resource structure positions
- **In-World Structures:** `/setblock` and `/fill` commands for building emerald/redstone towers; DecentHolograms API for floating text

### 6. OpenTelemetry Integration — Two Worlds of Metrics
- The OTEL Java agent: bind-mounted into the Minecraft container, configured via environment variables
- JVM metrics: automatic instrumentation of heap, GC, threads, CPU
- Custom game metrics: the worker polls RCON and publishes `minecraft.*` metrics via `System.Diagnostics.Metrics`
- Structured logging: every player message logged as an OTEL event with rich context
- How it all shows up in the Aspire dashboard

### 7. Lessons Learned
- RCON is synchronous and single-threaded — batching matters
- Docker volume mounts for plugins require careful ordering (server must generate folders first)
- The OTEL Java agent is 23 MB — embedding vs. runtime download tradeoffs
- Minecraft commands are not transactional — idempotency is on you
- State tracking is essential to avoid command spam on every poll cycle

### 8. What's Next — Sprint 2 Architecture Preview
- `IRconCommandSender` interface for testability
- RCON command batching and rate limiting
- Guardian mob spawning via `/summon` with entity lifecycle management
- Beacon towers and the placement algorithm challenge

---

## Opening Paragraphs (Draft)

When I first wired up a Minecraft server as an Aspire resource, the demo was simple: run `dotnet run`, watch a Minecraft server appear in the dashboard, connect with the game client, and see some blocks. It was neat. But it wasn't *interesting*.

The moment it became interesting was when we added health monitoring. A Minecraft world that reacts to your distributed system — where the weather darkens because your Redis cache went down, where a wither's growl plays because your API threw a 503 — that's not a dashboard. That's an experience. And it turns out, building that experience required solving some genuinely tricky engineering problems.

This post walks through the architecture of `Fritz.Aspire.Hosting.Minecraft` — from the binary RCON packets we send to the Minecraft server, to the Aspire resource model that makes it all feel like a native integration, to the worker service that ties health checks to weather patterns. If you've ever wondered how a .NET `BackgroundService` talks to a Java game server running in Docker, this is your post.

Let's start where all Minecraft server administration starts: with RCON.
