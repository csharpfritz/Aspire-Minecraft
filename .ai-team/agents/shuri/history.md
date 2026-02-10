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

### Public API Surface Audit (Issue #12)

- **MinecraftHealthCheck** changed from `public` to `internal` — it's only instantiated inside `AddMinecraftServer()` and consumers never need to reference it directly.
- **All Worker project types** changed from `public` to `internal` — the Worker is a standalone service (`IsPackable=false`) with no public API surface. Types changed: `MinecraftMetrics`, `RconService`, `AspireResourceMonitor`, `ResourceInfo`, `ResourceStatusChange`, `ResourceStatus`, `HologramManager`, `PlayerMessageService`, `ScoreboardManager`, `StructureBuilder`, `BossBarService`, `TitleAlertService`, `ParticleEffectService`, `SoundEffectService`, `WeatherService`.
- **Added `InternalsVisibleTo` for Worker tests** — `Aspire.Hosting.Minecraft.Worker.Tests` now has access to internal Worker types.
- **Fixed pre-existing build errors:** Extension methods in `MinecraftServerBuilderExtensions.cs` were placed outside the class body (lines 310–385). Moved them inside the class. Also fixed `$$` raw string interpolation syntax in `BossBarService.cs` and `TitleAlertService.cs`.
- **Test adaptation:** `StateTransitionTrackingTests.ResourceStatusChange_AllValidTransitions_AreRecorded` used `ResourceStatus` enum (now internal) in `[InlineData]` — changed to `int` parameters with cast.
- **Intentionally public API surface (Hosting package):**
  - `MinecraftServerBuilderExtensions` — all `With*` and `Add*` extension methods
  - `MinecraftServerResource` — resource type, `RconPasswordParameter`, `ConnectionStringExpression`
- **Intentionally public API surface (Rcon, embedded in Hosting package):**
  - `RconClient` — `ConnectAsync`, `AuthenticateAsync`, `SendCommandAsync`, `IsConnected`, `DisposeAsync`
  - `RconConnection` — `SendCommandAsync`, `IsConnected`, `DisposeAsync`
  - `RconResponseParser` — `StripColorCodes`, `ParseTps`, `ParseMspt`, `ParsePlayerList`, `ParseWorldList`
  - `TpsResult`, `MsptResult`, `PlayerListResult`, `WorldListResult` — response data types
- **Created CONTRIBUTING.md** with prerequisites, build/test/pack commands, single-package architecture notes, code style, and PR process.
- **Updated PR template** — added `-c Release` to build command, `Closes #` placeholder, pack clarification for src projects only, public API changes checklist item.

### NuGet Readiness Audit (2026-02-10)

- **Build status:** `dotnet build Aspire-Minecraft.slnx` succeeds cleanly (0 errors, 0 warnings on src projects).
- **Pack status:** `dotnet pack -o nupkgs` succeeds. Produces three `.nupkg` files. Two sample projects correctly emit "not packable" warnings.
- **Package sizes:** Hosting=~41 MB (due to 23 MB otel jar), Rcon=~20 KB, Worker=~28 KB.
- **Content files:** `bluemap/core.conf` and `otel/opentelemetry-javaagent.jar` are packed as both `content/` and `contentFiles/` entries — correct for consumer copy-to-output behavior.
- **Floating versions:** All three csproj files use `Version="*"` on PackageReference. Pack resolves these to concrete versions in the nuspec (e.g., Aspire.Hosting → 13.1.0). This is fine for local dev but is fragile for reproducible builds and should be pinned before publishing.
- **Directory.Build.props** (`Directory.Build.props`): Sets shared metadata — Authors, License (MIT), PackageProjectUrl, RepositoryUrl, RepositoryType, PackageReadmeFile. Conditionally includes repo-root README.md in all packages.
- **Missing from all csproj/props:** `GenerateDocumentationFile`, `EnablePackageValidation`, `SourceLink`, `Deterministic`, `ContinuousIntegrationBuild`, `EmbedUntrackedSources`, `PackageIcon`, `PackageReleaseNotes`.
- **README:** All three packages share the repo-level `README.md`. No per-package README.
- **CI/CD:** No `.github/workflows/` files exist. Only `.github/agents/squad.agent.md`.
- **XML docs:** Present on public APIs in all three projects (confirmed by grep). But `GenerateDocumentationFile` is not enabled, so no XML doc file is shipped in the nupkg.
- **No icon:** `img/sample-1.png` exists but no `PackageIcon` property is set. NuGet.org will show a default icon.

📌 Team update (2026-02-10): 18 Minecraft interaction features proposed across 3 tiers — decided by Rocket
📌 Team update (2026-02-10): 3-sprint roadmap adopted — Sprint 1 assigns Shuri: pin deps, NuGet hardening, extract otel jar, verify pack — decided by Rhodey

### Sprint 1 NuGet Hardening (2026-02-10)

- **Pinned all floating `Version="*"` dependencies** to exact resolved versions:
  - `Aspire.Hosting` → `13.1.0` (in Aspire.Hosting.Minecraft.csproj)
  - `Microsoft.Extensions.Logging.Abstractions` → `10.0.2` (in Aspire.Hosting.Minecraft.Rcon.csproj)
  - `Microsoft.Extensions.Hosting` → `10.0.2` (in Aspire.Hosting.Minecraft.Worker.csproj)
  - `Microsoft.Extensions.Http` → `10.0.2` (in Aspire.Hosting.Minecraft.Worker.csproj)
  - `OpenTelemetry.Extensions.Hosting` → `1.15.0` (in Aspire.Hosting.Minecraft.Worker.csproj)
  - `OpenTelemetry.Exporter.OpenTelemetryProtocol` → `1.15.0` (in Aspire.Hosting.Minecraft.Worker.csproj)
- **Added NuGet hardening properties** to `Directory.Build.props`: `GenerateDocumentationFile`, `EnablePackageValidation`, `Deterministic`, `ContinuousIntegrationBuild` (CI-only), `EmbedUntrackedSources`.
- **Added SourceLink** via `Microsoft.SourceLink.GitHub` Version `8.*` with `PrivateAssets="All"`.
- **OpenTelemetry Java agent (23 MB):** Chose Option B — kept embedded, added a TODO comment in the csproj for Sprint 2 runtime download. Rationale: runtime download introduces container networking assumptions and itzg image init-system complexity. Ship the simple thing first.
- **Created per-package README.md files** for all three packages; removed the shared repo-root README from `Directory.Build.props` conditional include. Each csproj now packs its own local README.
- **Pack output verified clean:** 0 errors, 0 warnings on src projects. Only warnings are from sample projects ("not packable") — expected.
  - `Aspire.Hosting.Minecraft.0.1.0.nupkg` — 39.6 MB (otel jar dominates)
  - `Aspire.Hosting.Minecraft.Rcon.0.1.0.nupkg` — 20.8 KB
  - `Aspire.Hosting.Minecraft.Worker.0.1.0.nupkg` — 27.5 KB
- **No floating versions remain** in any nuspec — confirmed by inspecting the packed nuspec inside the nupkg.

📌 Team update (2026-02-10): CI/CD pipeline created — build.yml + release.yml now build/test/publish your packages — decided by Wong
📌 Team update (2026-02-10): Test infrastructure created — InternalsVisibleTo added to both source projects, 62 tests passing — decided by Nebula

### Single Package Consolidation (2026-02-10)

- **Consolidated three NuGet packages into one:** Only `Aspire.Hosting.Minecraft` is now packable. Rcon and Worker projects set to `IsPackable=false`.
- **Rcon embedding approach:** Used `PrivateAssets="All"` on the ProjectReference to prevent Rcon from appearing as a nuspec dependency, combined with `TargetsForTfmSpecificBuildOutput` and `BuildOutputInPackage` MSBuild items to physically include `Aspire.Hosting.Minecraft.Rcon.dll` and its XML docs in the package's `lib/` folder.
- **Rcon transitive dependency:** Added `Microsoft.Extensions.Logging.Abstractions` as a direct `PackageReference` in the Hosting project since Rcon's dependency is no longer surfaced via a separate package.
- **Worker kept separate:** Worker uses `Microsoft.NET.Sdk.Worker` and runs as a standalone process — cannot be a DLL inside the hosting package. Consumers reference it as a `ProjectReference` via the `WithAspireWorldDisplay<TWorkerProject>()` generic type parameter.
- **Test projects unchanged:** Both test projects still reference their source projects directly. All 62 tests (45 Rcon + 17 Hosting) pass.
- **Pack output:** Single `Aspire.Hosting.Minecraft.0.1.0.nupkg` (39.6 MB). Contains both DLLs, XML docs, content files (bluemap, otel jar), and README.

📌 Team update (2026-02-10): FluentAssertions fully removed — replaced with xUnit Assert, zero licensing risk — decided by Jeffrey T. Fritz, Nebula

### NuGet PackageId Rename (2026-02-10)

- **Renamed PackageId** from `Aspire.Hosting.Minecraft` to `Fritz.Aspire.Hosting.Minecraft` in `src/Aspire.Hosting.Minecraft/Aspire.Hosting.Minecraft.csproj`. The `Aspire.Hosting` prefix is reserved by Microsoft on NuGet.org and would cause publish rejection.
- **No namespace/assembly/folder changes** — C# namespaces remain `Aspire.Hosting.Minecraft`, project folders and assembly names unchanged. Only the NuGet package identity changed.
- **Updated documentation:** Blog post install commands, demo script, CONTRIBUTING.md nupkg filename reference, social media copy — all now reference `Fritz.Aspire.Hosting.Minecraft`.
- **CI/CD unaffected:** Both `build.yml` and `release.yml` use `nupkgs/*.nupkg` globs — no hardcoded package name.
- **Verified:** `dotnet restore` ✅, `dotnet build -c Release` ✅ (0 errors), `dotnet pack -c Release -o nupkgs` ✅ (produces `Fritz.Aspire.Hosting.Minecraft.0.1.0.nupkg`), `dotnet test` ✅ (207 tests pass).

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
