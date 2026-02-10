# Project Context

- **Owner:** Jeffrey T. Fritz (csharpfritz@users.noreply.github.com)
- **Project:** Aspire.Hosting.Minecraft — .NET Aspire integration for Minecraft servers
- **Stack:** C#, .NET 10, Docker, Aspire, OpenTelemetry, Minecraft Paper Server, RCON
- **Created:** 2026-02-10

## Key Facts

- Three NuGet packages: Aspire.Hosting.Minecraft (hosting lib), Aspire.Hosting.Minecraft.Rcon (RCON client), Aspire.Hosting.Minecraft.Worker (in-world display)
- All packages at version 0.1.0, packable via `dotnet pack -o nupkgs`
- Hosting lib depends on Aspire.Hosting and the Rcon project
- Worker depends on Microsoft.Extensions.Hosting, Http, and OpenTelemetry packages
- Rcon depends only on Microsoft.Extensions.Logging.Abstractions
- Content files: bluemap/core.conf and otel/opentelemetry-javaagent.jar bundled with hosting package
- Uses PackageReference with Version="*" for Aspire packages (floating versions)

## Learnings

<!-- Append new learnings below. Each entry is something lasting about the project. -->

### Summary: Sprint 1 NuGet Work (2026-02-10)

- **NuGet readiness audit:** Identified blockers — floating `Version="*"` deps, no SourceLink/deterministic builds, no CI/CD, 41 MB hosting package (23 MB OTel jar), no per-package READMEs, no `GenerateDocumentationFile`.
- **Hardening:** Pinned 6 deps to exact versions, added `GenerateDocumentationFile`, `EnablePackageValidation`, `Deterministic`, `ContinuousIntegrationBuild`, `EmbedUntrackedSources`, `Microsoft.SourceLink.GitHub` to `Directory.Build.props`. Created per-package READMEs. OTel jar kept embedded (Sprint 2 TODO).
- **Single package consolidation:** Only `Aspire.Hosting.Minecraft` is packable (`Fritz.Aspire.Hosting.Minecraft` on NuGet). Rcon embedded via `PrivateAssets="All"` + `BuildOutputInPackage`. Worker is `IsPackable=false` (standalone service). Rcon's transitive dep surfaced as direct PackageReference in Hosting.
- **Public API audit (Issue #12):** `MinecraftHealthCheck` → internal. All Worker types → internal. Public: `MinecraftServerBuilderExtensions`, `MinecraftServerResource`, 5 RCON types. Created CONTRIBUTING.md, updated PR template.
- **PackageId rename:** `Aspire.Hosting.Minecraft` → `Fritz.Aspire.Hosting.Minecraft` (Aspire.Hosting prefix reserved by Microsoft). Namespaces/folders unchanged.

📌 Team update (2026-02-10): 18 Minecraft interaction features proposed across 3 tiers — decided by Rocket
📌 Team update (2026-02-10): 3-sprint roadmap adopted — Sprint 1 assigns Shuri: pin deps, NuGet hardening, extract otel jar, verify pack — decided by Rhodey
📌 Team update (2026-02-10): CI/CD pipeline created — build.yml + release.yml now build/test/publish your packages — decided by Wong
📌 Team update (2026-02-10): Test infrastructure created — InternalsVisibleTo added to both source projects, 62 tests passing — decided by Nebula
📌 Team update (2026-02-10): FluentAssertions fully removed — replaced with xUnit Assert, zero licensing risk — decided by Jeffrey T. Fritz, Nebula

### Sprint 2: XML Documentation & RCON Throttle (Issue #16)

- **Added XML doc comments to all public types and methods** across both `Aspire.Hosting.Minecraft` and `Aspire.Hosting.Minecraft.Rcon` projects. Every public method now has `<summary>`, `<param>`, `<returns>`, and `<exception>` tags where applicable.
- **`GenerateDocumentationFile` already enabled** in `Directory.Build.props` from Sprint 1. No csproj changes needed.
- **Covered types:** `MinecraftServerResource` (constructor, properties), `MinecraftServerBuilderExtensions` (all 15 public methods including Rocket's Sprint 2 additions: `WithActionBarTicker`, `WithBeaconTowers`, `WithBossBar` with `appName`), `RconClient` (all public members), `RconConnection` (constructor, `IsConnected`, `SendCommandAsync`, `DisposeAsync`), `RconResponseParser` (all 5 methods), `TpsResult`, `MsptResult`, `PlayerListResult`, `WorldListResult` (all with param docs).
- **RCON throttle mechanism added to Worker `RconService`:** New optional `minCommandInterval` parameter (defaults to `TimeSpan.Zero` — disabled). When configured, identical RCON commands sent within the interval are deduplicated. Production Worker configures 250ms throttle. Tests use default (no throttle) to avoid timing sensitivity.
- **Design rationale for throttle:** Per-command-string deduplication catches the main flooding scenario (rapid health oscillations sending identical `weather`, `bossbar`, or `particle` commands). Default-off ensures backward compatibility and test stability. The Worker's `Program.cs` opts in with 250ms.
- **Verified:** `dotnet build -c Release` ✅ (0 errors, 1 pre-existing CS8604 nullable warning), `dotnet test --no-build -c Release` ✅ (248 tests pass: 186 Worker + 45 RCON + 17 Hosting).

### Sprint 2: Configuration Builder Pattern Review (Issue #21)

- **Determined existing pattern is sufficient.** The current `With*()` fluent extension method pattern already provides the configuration builder experience: `AddMinecraftServer().WithBossBar().WithWeatherEffects().WithParticleEffects()`.
- **No formal builder options class needed.** A `AddMinecraftServer(opts => opts.EnableBossBars().EnableWeather())` pattern would duplicate functionality without adding value. The per-method approach is idiomatic Aspire, independently opt-in, and backward-compatible by design.
- **Recommendation:** Close Issue #21 as already-addressed by Sprint 1's `With*()` extension method architecture.

### Server Properties Configuration API

- **Added `WithServerProperty(string, string)` and `WithServerProperties(Dictionary<string, string>)`** — generic methods for setting any Minecraft `server.properties` value via the itzg/minecraft-server env var convention (property name → UPPER_SNAKE_CASE).
- **Added 6 convenience methods:** `WithGameMode`, `WithDifficulty`, `WithMaxPlayers`, `WithMotd`, `WithWorldSeed`, `WithPvp` — type-safe wrappers for the most commonly configured properties.
- **itzg env var convention:** The `itzg/minecraft-server` Docker image reads env vars as `server.properties` overrides. Property names are converted by uppercasing and replacing hyphens with underscores (e.g., `max-players` → `MAX_PLAYERS`). The `ConvertPropertyNameToEnvVar` helper centralizes this.
- **Design decision:** These methods set env vars on the container resource directly (not on the worker builder). This is correct because `server.properties` is a Minecraft server concern, not a worker concern. Later env var calls override earlier ones, so user calls to `WithWorldSeed("custom")` correctly override the default `SEED=aspire2026` set in `AddMinecraftServer()`.
- **Updated demo AppHost** to show `.WithMaxPlayers(10).WithMotd("Aspire Fleet Monitor")` chained right after `AddMinecraftServer()`.
- **Verified:** `dotnet build -c Release` ✅ (0 errors), `dotnet test --no-build -c Release` ✅ (248 tests pass: 186 Worker + 45 RCON + 17 Hosting).

### ServerProperty Enum & File-Based Properties Loading

- **Created `ServerProperty` enum** (`ServerProperty.cs`) with 24 PascalCase members covering all commonly configured Minecraft `server.properties` keys: `MaxPlayers`, `Motd`, `Difficulty`, `GameMode`, `Pvp`, `Hardcore`, `ViewDistance`, `SimulationDistance`, `MaxWorldSize`, `SpawnProtection`, `SpawnAnimals`, `SpawnMonsters`, `SpawnNpcs`, `AllowFlight`, `AllowNether`, `ForceGamemode`, `LevelType`, `LevelName`, `Seed`, `WhiteList`, `OnlineMode`, `EnableCommandBlock`, `ServerPort`, `MaxBuildHeight`, `GenerateStructures`. Each member has XML doc comments describing valid values.
- **Created `MinecraftGameMode` enum** (`MinecraftGameMode.cs`) with `Survival`, `Creative`, `Adventure`, `Spectator` members.
- **Created `MinecraftDifficulty` enum** (`MinecraftDifficulty.cs`) with `Peaceful`, `Easy`, `Normal`, `Hard` members.
- **Added `WithServerProperty(ServerProperty, string)` overload** — accepts the enum and converts PascalCase to UPPER_SNAKE_CASE internally via `ConvertEnumToEnvVar()` helper.
- **Added `WithGameMode(MinecraftGameMode)` overload** — converts enum to lowercase string (e.g., `MinecraftGameMode.Creative` → `"creative"`).
- **Added `WithDifficulty(MinecraftDifficulty)` overload** — converts enum to lowercase string (e.g., `MinecraftDifficulty.Hard` → `"hard"`).
- **Added `WithServerPropertiesFile(string)` method** — reads a standard Minecraft `server.properties` file at build/configuration time, parses key=value lines (skipping comments/blanks, splitting on first `=` only), and calls `WithServerProperty()` for each entry. Relative paths resolve from AppHost project directory. Throws `FileNotFoundException` if the file doesn't exist.
- **PascalCase→UPPER_SNAKE_CASE conversion** (`ConvertEnumToEnvVar`): Inserts `_` before each uppercase letter (except the first), then uppercases everything. E.g., `MaxPlayers` → `MAX_PLAYERS`, `SpawnNpcs` → `SPAWN_NPCS`.
- **Verified:** `dotnet build -c Release` ✅ (0 errors, 1 pre-existing CS8604 warning), `dotnet test --no-build -c Release` ✅ (248 tests pass: 186 Worker + 45 RCON + 17 Hosting).

### NuGet Package Versioning Fix

- **Changed `<Version>0.1.0</Version>` to `<Version>0.1.0-dev</Version>`** in `src/Aspire.Hosting.Minecraft/Aspire.Hosting.Minecraft.csproj`. The `-dev` suffix marks local/dev builds as pre-release.
- **CI override mechanism:** MSBuild command-line properties (`-p:Version=X.Y.Z`) always override csproj `<Version>`. The release workflow (maintained by Wong) passes the git tag version via `-p:Version`, so published packages get the correct release version (e.g., `0.2.1`). No `<VersionPrefix>`/`<VersionSuffix>` split needed — the single `<Version>` property with CLI override is the simplest approach.
- **Verified both scenarios:**
  - `dotnet pack -c Release --no-build -o nupkgs` → `Fritz.Aspire.Hosting.Minecraft.0.1.0-dev.nupkg` ✅
  - `dotnet pack -c Release --no-build -o nupkgs -p:Version=0.2.1` → `Fritz.Aspire.Hosting.Minecraft.0.2.1.nupkg` ✅
- **All tests pass:** `dotnet test --no-build -c Release` ✅ (248 tests: 186 Worker + 45 RCON + 17 Hosting).

📌 Team update (2026-02-10): Release workflow extracts version from git tag and passes to dotnet build/pack — decided by Wong
📌 Team update (2026-02-10): Sprint 2 API review complete — 5 additive recommendations for Sprint 3 (WithAllFeatures, ParseConnectionString extraction, IRconCommandSender, env var tightening, auto-discovery) — decided by Rhodey
📌 Team update (2026-02-10): Beacon tower colors now match Aspire dashboard resource type palette — decided by Rocket
📌 Team update (2026-02-10): Hologram line-add bug fixed (RCON throttle dropping duplicate commands) — decided by Rocket

### Ephemeral World by Default + WithPersistentWorld() Opt-in

- **Removed named volume mount** (`{name}-data` → `/data`) from `AddMinecraftServer()`. World data is now ephemeral — each `dotnet run` starts fresh, no leftover structures or state.
- **Added `WithPersistentWorld()` extension method** that mounts a named Docker volume (`{name}-data` → `/data`) for consumers who want persistent world data across restarts.
- **Updated `AddMinecraftServer()` XML docs** with a `<see cref="WithPersistentWorld"/>` reference explaining the default ephemeral behavior.
- **Updated sample AppHost** with a comment showing `WithPersistentWorld()` usage (commented out — demo should get fresh worlds).
- **Updated README** with a "World Persistence" subsection in the Configuration section.
- **No test changes needed** — no existing tests referenced the volume mount. 248 tests pass.
- **Key insight:** The itzg/minecraft-server container stores everything in `/data`. Without a named volume, Docker uses an anonymous volume that's cleaned up with the container. Aspire removes containers on shutdown, so the world is truly ephemeral.
