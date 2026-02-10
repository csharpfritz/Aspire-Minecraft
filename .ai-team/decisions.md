# Decisions

> Shared decision log. All agents read this before starting work. Scribe merges new decisions from the inbox.

### 2026-02-10: NuGet hardening completed — floating deps pinned, SourceLink and deterministic builds added

**By:** Shuri
**What:** Audited all three packages and found blockers (floating `Version="*"` deps, no SourceLink, no package validation, no CI/CD, 41 MB hosting package). Resolved in Sprint 1: pinned 6 deps to exact versions, added `GenerateDocumentationFile`, `EnablePackageValidation`, `Deterministic`, `ContinuousIntegrationBuild`, `EmbedUntrackedSources`, and `Microsoft.SourceLink.GitHub` to `Directory.Build.props`. Created per-package README.md files. Kept the 23 MB OpenTelemetry Java agent embedded — runtime download deferred to Sprint 2 due to container networking complexity.
**Why:** NuGet.org rejects floating versions. Pinned versions ensure reproducible builds. SourceLink enables debugger source mapping. Per-package READMEs improve nuget.org presentation. OTel jar stays embedded to avoid offline/restricted environment issues in v0.1.0.
**Status:** ✅ Resolved. Remaining: `PackageIcon` not yet added. OTel jar extraction is Sprint 2.

### 2026-02-10: Proposed feature ideas for Aspire-Minecraft

**By:** Rocket

**What:** A prioritized set of 18 new in-world interaction features organized by effort and impact across 3 tiers.

**Why:** The current worker is mostly passive (holograms, scoreboards, structures, chat). These features add drama, atmosphere, and delight — making health changes feel like real events in the game world.

**Must-Have (Size S, Sprint 1):** Boss Bar Health Meter, Title Screen Alerts, Sound Effects on Events, Weather = System Health, Particle Effects at Structures.

**Nice-to-Have (Size S–M, Sprint 2):** Action Bar Metrics Ticker, Fireworks on All-Green Recovery, Guardian Mobs per Resource, World Border Pulse, Beacon Towers per Resource Type, Deployment Fanfare.

**Stretch Goals (Size M–L, Sprint 3):** Resource Village with Themed Architecture, Redstone Heartbeat Circuit, Nether Portal Frames, Live Log Wall, Player /trigger Commands, Advancement Achievements, Resource Dependency Rail Network.

**Backlog (not in 3-sprint arc):** Nether Portal Frames, Live Log Wall, /trigger Commands.

### 2026-02-10: 3-Sprint Plan for Aspire-Minecraft

**By:** Rhodey
**What:** Three-sprint roadmap from "builds locally" to "conference-demo-ready NuGet packages with CI/CD and blog coverage."
**Why:** Working code but unpublishable packages. 18 features need sequencing so each sprint ends shippable and demo-able.

**Sprint 1 "Ship It" (~22 items):**
- Shuri: Pin floating deps, NuGet hardening, extract otel jar, verify pack output.
- Rocket: Boss bars, title alerts, sounds, weather, particles (all Size S).
- Nebula: Test project structure, RCON unit tests, health check tests, pack smoke test.
- Wong: build.yml CI workflow, release.yml stub, branch protection.
- Rhodey: PR review gate, public API contract, CONTRIBUTING.md.
- Mantis: Blog outline, demo screenshots.

**Sprint 2 "Polish & Atmosphere" (~20 items):**
- Shuri: Configuration builder pattern, XML docs, RCON batching audit.
- Rocket: Action bar ticker, fireworks, guardian mobs, beacons, deployment fanfare.
- Nebula: Integration test harness, Sprint 1 feature tests, config tests, coverage gate.
- Wong: NuGet publish on tag, test execution in CI, Dependabot, issue templates.
- Rhodey: API review, cut v0.1.0 release.
- Mantis: Publish v0.1.0 blog, social media, begin deep-dive draft.

**Sprint 3 "Showstopper" (~18 items):**
- Shuri: World border pulse, placement algorithm, RCON rate-limiting.
- Rocket: Themed village (L), redstone heartbeat, achievements, rail network.
- Nebula: Sprint 2 feature tests, world border tests, E2E demo test, perf test.
- Wong: Changelog gen, symbol packages, GitHub Pages docs, CodeQL.
- Rhodey: API freeze v0.2.0, automated release, demo script review.
- Mantis: Deep-dive blog, conference demo post, README overhaul.

**Cut line:** Rail Network drops first; Resource Village + Achievements are conference must-haves.

### 2026-02-10: CI/CD pipeline — build.yml + release.yml created

**By:** Wong
**What:** Created two GitHub Actions workflows: `build.yml` (CI on push/PR to main, ubuntu+windows matrix, restore→build→test→pack→upload) and `release.yml` (NuGet publish on `v*` tag, GitHub Release creation). Also added `.github/PULL_REQUEST_TEMPLATE.md`. No separate PR-validation workflow — `build.yml` covers PR triggers.
**Why:** Sprint 1 blocker — no CI/CD existed. Packages can't ship to nuget.org without an automated publish pipeline. The matrix build ensures cross-platform correctness. Tag-triggered release keeps publishing intentional. `NUGET_API_KEY` secret must be configured in repo settings before first release.

### 2026-02-10: Test project structure and InternalsVisibleTo pattern established

**By:** Nebula
**What:** Created tests/Aspire.Hosting.Minecraft.Rcon.Tests and tests/Aspire.Hosting.Minecraft.Tests with xUnit and Microsoft.NET.Test.Sdk. Added InternalsVisibleTo to both source projects. Changed MinecraftHealthCheck.ParseConnectionString from private to internal for testability. 62 tests (45 RCON + 17 hosting), 0 failures.
**Why:** CI/CD pipeline requires test projects to exist and pass. The InternalsVisibleTo pattern enables testing of internal types (RconPacket, endpoint constants, ParseConnectionString) without exposing them publicly.

### 2026-02-10: FluentAssertions removal and assertion library decision (consolidated)

**By:** Nebula, Jeffrey T. Fritz
**What:** FluentAssertions 8.8.0 (Xceed) had commercial licensing incompatible with this MIT-licensed project. Jeff directed the team to drop it entirely. Nebula replaced all 95 assertion calls across 5 test files with xUnit's built-in `Assert` class. 62 tests, 0 failures after migration. Zero new dependencies added.
**Why:** Nebula flagged the licensing concern; Jeff confirmed no FluentAssertions. xUnit `Assert` was chosen over Shouldly/TUnit because all existing patterns (equality, boolean, null, empty, contains, throws) mapped 1:1 to `Assert.*` — no new package needed.
**Status:** ✅ Resolved. FluentAssertions fully removed from both .csproj files and all test code.

### 2026-02-10: Track all work as GitHub issues with team member labels

**By:** Jeffrey T. Fritz (via Copilot)
**What:** All sprint plan items opened as GitHub issues. Labels created for each team member (rhodey, shuri, rocket, nebula, wong, mantis) and sprint (sprint-1, sprint-2, sprint-3). 34 issues created across 3 sprints. Labels should have distinct, visually meaningful colors for easy identification.
**Why:** User directive — ensures visibility and accountability for all planned work.

### 2026-02-10: Single NuGet package consolidation (consolidated)

**By:** Jeffrey T. Fritz, Shuri
**What:** Jeff directed that the RCON client, worker service, and Aspire hosting integration should ship as a single NuGet package. Shuri implemented the consolidation: only `Aspire.Hosting.Minecraft` is now packable. Rcon project set to `IsPackable=false` with its assembly embedded via `PrivateAssets="All"` + `BuildOutputInPackage`. Worker set to `IsPackable=false` (stays separate — it's a standalone process using `Microsoft.NET.Sdk.Worker`). Rcon's transitive dependency (`Microsoft.Extensions.Logging.Abstractions`) surfaced as a direct PackageReference in the Hosting project.
**Why:** Simplifies the consumer experience — one package to install. The Rcon library is a pure implementation detail. The Worker is referenced via the `WithAspireWorldDisplay<TWorkerProject>()` generic type parameter, not as a library dependency.
**Verified:** `dotnet restore` ✅, `dotnet build -c Release` ✅, `dotnet pack -c Release -o nupkgs` ✅ (single package: 39.6 MB), `dotnet test` ✅ (62 tests pass).
**Status:** ✅ Resolved.

### 2026-02-10: User feature idea — Redstone Dependency Graph + Service Switches

**By:** Jeffrey T. Fritz (via Copilot)

**What:** Design a feature that:
1. **Redstone Wires = Resource Dependencies** — Visualize the connections between Aspire resources (databases, APIs, workers, etc.) using redstone wire circuits in the Minecraft world. Each resource has a structure/building, and redstone lines connect them to show the dependency graph.
2. **Lever Switches = Service Control** — Place Minecraft levers/switches on each service's structure so the player can physically toggle services on/off from within Minecraft. Flipping a lever would start or stop the corresponding Aspire resource.

**Why:** This turns the Minecraft world into an interactive operations dashboard. Instead of just visualizing health, the player can actually *control* the distributed system from inside the game. It's the ultimate "infrastructure as a game" experience.

**Technical considerations:**
- Redstone wires have a max range of 15 blocks — may need repeaters for distant services
- Lever state changes can be detected via RCON world interaction or plugin events
- Need to model the DAG (directed acyclic graph) of Aspire resource dependencies
- Starting/stopping services maps to Aspire's resource lifecycle (IResourceWithConnectionString, etc.)
- Should respect dependency ordering — stopping a database should warn about dependent services
- Could use redstone signal strength to indicate health/load

**Sprint target:** Sprint 3 (Showstopper) — this is a flagship feature

### 2026-02-10: NuGet PackageId renamed to Fritz.Aspire.Hosting.Minecraft

**By:** Shuri (requested by Jeffrey T. Fritz)
**What:** Renamed the NuGet PackageId from `Aspire.Hosting.Minecraft` to `Fritz.Aspire.Hosting.Minecraft` in the csproj. Updated all documentation (blog post, demo script, CONTRIBUTING.md) to reference the new package name. C# namespaces, project folders, assembly names, and solution structure are unchanged — only the NuGet package identity changed. User explicitly chose `Fritz` as the prefix (rejected `CommunityToolkit` alternative).
**Why:** The `Aspire.Hosting` prefix is reserved by Microsoft on NuGet.org. Publishing under that prefix would be rejected. The `Fritz` prefix avoids the reserved namespace while keeping the package discoverable. Consumers still `using Aspire.Hosting.Minecraft;` — the install command is now `dotnet add package Fritz.Aspire.Hosting.Minecraft`.
**Verified:** restore ✅, build ✅ (0 errors), pack ✅ (`Fritz.Aspire.Hosting.Minecraft.0.1.0.nupkg`), test ✅ (207 tests pass).
**Status:** ✅ Resolved.

### 2026-02-10: Blog outline structure and media plan for v0.1.0

**By:** Mantis
**What:** Created three deliverables in `docs/blog/`: `v0.1.0-release-outline.md` (full blog post outline with 7 sections, placeholder code snippets, social media copy), `v0.1.0-media-plan.md` (18 visual assets with capture instructions), and `v0.1.0-demo-script.md` (10-minute 4-act demo script).
**Why:** First public release — the blog post is the primary announcement channel. .NET devs using Aspire are the audience. Demo climax is the "break" moment (stopping a service and watching 6 feedback channels react). 18 media assets cover blog, social media, and conference slides. Media captures require Sprint 1 features from Rocket.
**Dependencies:** Rocket's Sprint 1 features (boss bar, weather, title alerts, sounds, particles) must be complete before media capture. Blog references actual sample `Program.cs`.

### 2026-02-10: Sprint 1 proactive test coverage for Rocket's features

**By:** Nebula
**What:** Created `tests/Aspire.Hosting.Minecraft.Worker.Tests` with 145 tests covering all 5 Sprint 1 features (particles, title alerts, weather, boss bar, sounds) plus state transitions, health→RCON mapping, and feature opt-in behavior. Solution total: 207 tests across 3 projects, all passing.
**Why:** Proactive testing — writing tests before implementation ensures expected RCON command syntax is documented, state transition edge cases are covered, and Rocket has concrete test expectations to code against.
**Key decisions:** No MockRconService (sealed class, no interface) — tests validate command format via static helper. Commented-out stubs for opt-in tests await Rocket's extension methods. Health ratio thresholds are opinionated (Weather: 100%=clear, 20-99%=rain, <20%=thunder; BossBar: ≥75%=green, 25-74%=yellow, <25%=red).
**Testability concern:** `RconService` is sealed with no interface — consider adding `IRconCommandSender` in Sprint 2.
**Status:** ✅ Complete.

### 2026-02-10: Sprint 1 feature decisions — opt-in architecture, state tracking, health thresholds

**By:** Rocket
**Issues:** #3, #5, #7, #8, #10
**What:** Each Sprint 1 feature (particles, title alerts, weather, boss bar, sounds) is enabled by a dedicated environment variable (`ASPIRE_FEATURE_{NAME}=true`) set via builder extension methods, with conditional service registration in the Worker. Services injected as nullable primary constructor parameters. Particles/titles/sounds fire per-resource; weather/boss bar reflect aggregate fleet health. State tracking (`_lastWeather`, `_lastValue`, `_lastColor`) avoids redundant RCON commands.
**Health thresholds:** Weather: 100%=clear, ≥50%=rain, <50%=thunder. Boss bar: 100%=green, ≥50%=yellow, <50%=red.
**Why:** Follows existing env var pattern. Opt-in ensures backward compatibility and zero additional RCON traffic for unused features. State tracking conserves server tick budget.
**Status:** ✅ Implemented.

### 2026-02-10: Public API surface contract established

**By:** Shuri
**Issue:** #12
**What:** Audited all public types and established intentional API surface. Made `MinecraftHealthCheck` internal (hosting). Made all Worker types internal (15 classes). Kept public: `MinecraftServerBuilderExtensions` (consumer entry point with 11 methods), `MinecraftServerResource`, and 5 RCON types (`RconClient`, `RconConnection`, `RconResponseParser`, `TpsResult`, `MsptResult`, `PlayerListResult`, `WorldListResult`).
**Why:** Worker is a standalone service (`IsPackable=false`) — all its types are implementation details. RCON types kept public for consumers who want custom RCON commands. `EnablePackageValidation` catches accidental API surface changes.
**Status:** ✅ Resolved.

### 2026-02-10: Sprint 2 API review — consistent, no breaking changes

**By:** Rhodey
**Scope:** Public API surface review of `src/Aspire.Hosting.Minecraft/` after Sprint 2 completion

**What:** All 10 feature extension methods (5 Sprint 1 + 5 Sprint 2) follow identical patterns: same signature shape, guard clause, env var naming (`ASPIRE_FEATURE_*`), fluent return, XML docs. No breaking changes needed.

**Key findings:**
- Environment variable naming consistent: `ASPIRE_FEATURE_{NAME}` for features, `ASPIRE_RESOURCE_{NAME}_TYPE/URL/HOST/PORT` for metadata, `ASPIRE_APP_NAME` for app-level config.
- XML documentation complete on all public types and methods.
- Access modifiers correct: public API intentional, internal types properly hidden.

**Recommendations for Sprint 3:**
1. Add `WithAllFeatures()` convenience method
2. Extract duplicated `ParseConnectionString` to shared utility
3. Add `IRconCommandSender` interface for testability
4. Tighten feature env var checks to `== "true"` instead of `!string.IsNullOrEmpty`
5. Consider `WithAllMonitoredResources()` auto-discovery
6. API freeze before v0.2.0

**Status:** ✅ Approved for v0.1.0 release cut.

### 2026-02-10: Beacon tower glass colors match Aspire dashboard resource type palette

**By:** Rocket (requested by Jeffrey T. Fritz)
**What:** Changed `BeaconTowerService` from simple green/red glass to resource-type-specific colors matching the Aspire dashboard icon palette. Project=blue, Container=purple, Executable=cyan, unknown type=light blue, unhealthy=red, starting=yellow. `GetGlassBlock()` method is `internal static` for testability. Implements user directive that beacon beams should match Aspire dashboard resource type colors.
**Why:** Green/red scheme gave no visual distinction between resource types. Dashboard uses blue for projects, purple for containers, teal for executables — beacon beams reinforce the same color language in-world. Health state overrides type color for at-a-glance alerting.
**Status:** ✅ Resolved. Build passes, 248 tests pass.

### 2026-02-10: Hologram line-add commands must use unique text to avoid RCON throttle

**By:** Rocket
**What:** Fixed `HologramManager` using identical placeholder text (`&7...`) for all `dh line add` commands. The `RconService` 250ms throttle silently dropped duplicate commands in rapid succession, causing fewer hologram lines than expected. Changed to `&7line{n}` for unique commands.
**Why:** The RCON throttle is intentional for preventing server flood. The fix works with the throttle rather than disabling it. Any future service issuing identical RCON commands in a tight loop must use unique command strings.
**Status:** ✅ Resolved. Build passes, 248 tests pass.

### 2026-02-10: Sprint 2 feature decisions — action bar ticker, beacon towers, boss bar app name

**By:** Rocket
**Issues:** #38, #20, #22
**What:** Three new Sprint 2 features following the established opt-in env var pattern. Boss bar now supports configurable app name via `ASPIRE_APP_NAME` (implements user directive: boss bar text should show system name from Aspire, not generic text). Action bar ticker cycles TPS/MSPT/healthy count/RCON latency. Beacon towers build iron+beacon+glass structures per resource.

**Key decisions:**
- `WithBossBar()` added optional `string? appName = null` for backward compatibility.
- Action bar ticker reads fresh metrics each tick (not cached from main loop).
- Beacon towers at Z=8 offset to avoid collision with existing structures at Z=0.
- Single-layer iron base (minimum for beacon activation).
- Plain strings for action bar, consistent with Sprint 1.

**Status:** ✅ Implemented. 248 tests pass.

### 2026-02-10: NuGet package version defaults to `0.1.0-dev`, overridden by CI

**By:** Shuri
**What:** Changed `<Version>` in csproj from `0.1.0` to `0.1.0-dev`. Local builds produce pre-release packages. The release workflow passes `-p:Version=X.Y.Z` (from git tag) which overrides the csproj default.
**Why:** Previously every NuGet publish produced version `0.1.0` regardless of the git tag. The `-dev` suffix distinguishes local from release builds. MSBuild CLI properties always win over csproj values.
**Status:** ✅ Resolved. Wong's release workflow update is the companion change.

### 2026-02-10: WithServerProperty API for server.properties configuration

**By:** Shuri
**What:** Added `WithServerProperty(string, string)` and `WithServerProperties(Dictionary<string, string>)` extension methods, plus 6 convenience methods (`WithGameMode`, `WithDifficulty`, `WithMaxPlayers`, `WithMotd`, `WithWorldSeed`, `WithPvp`). All set env vars on the container resource following the itzg/minecraft-server convention (property name → UPPER_SNAKE_CASE).
**Why:** The itzg Docker image supports all `server.properties` values via env vars, but the hosting library had no public API to set them.

### 2026-02-10: ServerProperty Enum & File-Based server.properties Loading

**By:** Shuri
**What:** Added `ServerProperty` enum (24 members), `MinecraftGameMode` enum (4 members), `MinecraftDifficulty` enum (4 members), corresponding `WithServerProperty`/`WithGameMode`/`WithDifficulty` overloads, and `WithServerPropertiesFile()` for bulk property loading from disk.
**Why:** Users previously had to look up `server.properties` key names and pass raw strings. The enum gives IntelliSense discovery. Typed enums prevent typos. File-based loading lets users maintain a standard `server.properties` file.
**Design choices:** PascalCase→UPPER_SNAKE_CASE conversion. `WithServerPropertiesFile` reads at build/configuration time. Last-write-wins semantics.
**Status:** ✅ Resolved.

### 2026-02-10: Sprint 2 — XML documentation, RCON throttle, config builder pattern review

**By:** Shuri
**Issues:** #16, #21
**What:**
1. Added comprehensive XML documentation to all public types and methods in both projects.
2. Added configurable RCON command throttle to Worker's `RconService` (default: disabled, production: 250ms). Per-command-string deduplication prevents server flood during rapid health oscillations.
3. Reviewed configuration builder pattern — existing `With*()` fluent extension methods already serve as the config builder. No formal options-class builder needed. Recommend closing Issue #21.

**Status:** ✅ Complete.

### 2026-02-10: Release workflow now extracts version from git tag

**By:** Wong
**What:** Updated `.github/workflows/release.yml` to extract the semantic version from the git tag (`v0.2.1` → `0.2.1`) and pass it to `dotnet build` and `dotnet pack` via `-p:Version=`. GitHub Release name now includes the version. CI workflow (`build.yml`) intentionally unchanged.
**Why:** Previously every tag-triggered release produced `0.1.0` packages regardless of the actual tag. The tag is now the single source of truth for release versions.
**Status:** ✅ Resolved.

### 2026-02-10: User directive — sprint branches with PRs
**By:** Jeffrey T. Fritz (via Copilot)
**What:** Each sprint's work should be done in a dedicated branch named after that sprint, then pushed and merged via PR to main on GitHub.
**Why:** User request — captured for team memory

### 2026-02-10: E2E cascade failure scenario + 25-resource performance tests

**By:** Nebula
**Issue:** #31

**What:** Added comprehensive test coverage for Sprint 2 features and beyond:

1. **Sprint 2 feature integration tests** — GuardianMobService (8 tests), BeaconTowerService (16 tests including `GetGlassBlock` unit tests), FireworksService (7 tests), DeploymentFanfareService (7 tests), ActionBarTickerService (5 tests). All follow the established MockRconServer integration test pattern.

2. **E2E cascade failure scenario** (`Scenarios/CascadeFailureScenarioTests.cs`) — 4 tests exercising multi-service interaction: 5 resources healthy → 1 fails → 2 more cascade → boss bar drops to red → guardians switch to zombies → all recover → fireworks launch → golems return.

3. **25-resource performance tests** (`Performance/LargeResourceSetTests.cs`) — 10 tests proving StructureBuilder, BeaconTowerService, HologramManager, BossBarService, GuardianMobService, and ParticleEffectService all handle 25 resources without exceptions.

**Verified:** 303 tests across 3 projects, 0 failures.
**Status:** ✅ Complete.

### 2026-02-10: API Surface Freeze for v0.2.0

**By:** Rhodey
**Issue:** #24

**What:** Froze the public API surface for the v0.2.0 release. Created `docs/api-surface.md` documenting all 31 public extension methods on `MinecraftServerBuilderExtensions`, 4 public types in `Aspire.Hosting.Minecraft`, and 6 public RCON types.

**Key findings:**
- All 13 feature methods (Sprint 1–3) follow identical patterns: guard clause → env var → fluent return. No deviations.
- No internal types leak through the public API.
- XML documentation is complete on every public type and method.
- `WithWorldBorderPulse` was incorrectly grouped under Sprint 2 in the demo — moved to Sprint 3.

**Status:** ✅ Resolved. Any API additions beyond this point require explicit review before release.

### 2026-02-10: Azure Resource Group Integration — Epic Design

**By:** Rhodey
**Date:** 2026-02-10
**Scope:** New epic — Azure Resource Group → Minecraft integration
**Document:** `docs/epics/azure-resource-group-integration.md`

**Decisions Made:**
1. Separate NuGet package: `Fritz.Aspire.Hosting.Minecraft.Azure` — isolates Azure SDK dependencies.
2. Azure monitor is a new resource discovery source, not a new rendering pipeline.
3. Polling for v1, Event Grid deferred. 30-second default interval.
4. Aspire-only for v1. Standalone mode is Phase 5.
5. `MaxResources = 50` default with auto-exclude of infrastructure noise.
6. `DefaultAzureCredential` as the default auth.

**Open Questions for Jeff:**
- Package naming: `Fritz.Aspire.Hosting.Minecraft.Azure` vs `Fritz.Azure.Minecraft`?
- Should mixed mode (Aspire + Azure resources in same world) be supported in v1?
- Default exclude list for infrastructure resource types?

**Team Impact:**
- Shuri: Owns Phases 1 and 3 (ARM client, auth, options, NuGet package scaffold)
- Rocket: Owns Phase 2 (Azure type → Minecraft structure mapping)
- Nebula: Owns Phase 4 (mocked ARM client tests, options validation)

### 2026-02-10: Advancement Achievements use RCON titles instead of datapacks

**By:** Rocket
**Issue:** #32
**What:** `AdvancementService` grants four infrastructure achievements using RCON `title @a title/subtitle` with JSON text components and `playsound`. No Minecraft datapack advancements are used.
**Why:** Mounting custom advancement JSON datapacks into the Minecraft container is complex and fragile. Title + subtitle + sound gives equivalent player feedback without container filesystem changes. Achievements tracked per-session via `HashSet<string>`.
**Status:** ✅ Implemented. Follows opt-in pattern (`ASPIRE_FEATURE_ACHIEVEMENTS`, `WithAchievements()`).

### 2026-02-10: Azure Resource Visualization Design

**By:** Rocket
**Date:** 2026-02-10
**Document:** `docs/epics/azure-minecraft-visuals.md`

**What:** Designed the complete visual language for rendering Azure resources in Minecraft. Covers 15 Azure resource types mapped to unique Minecraft structures.

**Key Decisions:**
1. Azure district visually distinct from Aspire village (prismarine/quartz/end stone palette).
2. 3-column tiered layout grouping resources by functional tier.
3. District starts at X=60 with prismarine boulevard connecting to Aspire village.
4. Azure beacon colors: Compute=cyan, Data=blue, Networking=purple, Security=black, Messaging=orange, Observability=magenta.
5. Azure health states: Stopped=cobwebs, Deallocated=soul sand ring, Failed=netherrack fire on roof.

**Status:** 📐 Design complete — no implementation yet.

### 2026-02-10: Heartbeat service timing

**By:** Rocket
**Issue:** #27

**What:** `HeartbeatService` uses a 1–4 second pulse interval depending on health. Originally implemented as `BackgroundService` (via `AddHostedService`), later converted to `AddSingleton<>()` called from `MinecraftWorldWorker` to fix a startup race condition (see: "Sprint 3 service lifecycle" decision).

**Why:** Main worker loop runs on 10-second intervals — too slow for a heartbeat. RCON throttle deduplication handled by micro-varying volume (0.001 increments per tick).

**Status:** ✅ Implemented (lifecycle updated).

### 2026-02-10: Redstone Dependency Graph Implementation

**By:** Rocket
**Issue:** #36

**What:** `RedstoneDependencyService` visualizes Aspire resource dependencies as redstone wire circuits. L-shaped routing (X then Z), repeaters every 15 blocks, redstone lamps at entrances, health-reactive circuit breaking/restoring. Originally a `BackgroundService`, later converted to `AddSingleton<>()` called from `MinecraftWorldWorker` (see: "Sprint 3 service lifecycle" decision).

**Key decisions:**
1. L-shaped routing avoids complex A* pathfinding.
2. Circuit breaking — remove redstone block + break wire every 5th position on unhealthy.
3. CommandPriority.Low for building to avoid starving higher-priority commands.
4. Wire positions at BaseY, Z-1 — paths run in front of structures.

**Status:** ✅ Implemented (lifecycle updated).

### 2026-02-10: Resource Village Layout & Themed Structures

**By:** Rocket
**Issue:** #25

**What:** Themed mini-buildings per Aspire resource type in a 2×N grid with 10-block spacing. Project=Watchtower, Container=Warehouse, Executable=Workshop, Unknown=Cottage. `VillageLayout` static class centralizes position calculations. Health indicator via redstone lamp in front wall.

### 2026-02-10: Service Switches — visual-only levers representing resource status

**By:** Rocket
**Issue:** #35

**What:** `ServiceSwitchService` with `WithServiceSwitches()` and `ASPIRE_FEATURE_SWITCHES` env var. Levers and lamps on each resource structure. Healthy=lever ON + glowstone, Unhealthy=lever OFF + unlit redstone lamp. Originally a `BackgroundService`, later converted to `AddSingleton<>()` called from `MinecraftWorldWorker` (see: "Sprint 3 service lifecycle" decision).

**Key decision:** Visual only — levers reflect state, they do not control Aspire resources.

**Status:** ✅ Implemented (lifecycle updated).

### 2026-02-10: Village fence perimeter and pathway coordinate conventions

**By:** Rocket
**What:** Added `GetVillageBounds()` and `GetFencePerimeter()` to `VillageLayout`. Fence at ground level (`BaseY`), 4-block gap from buildings. Boulevard at `BaseX + StructureSize` (X=17). Future services placing things around the village edge should use these methods.
**Status:** ✅ Implemented (updated: fence moved to ground level, gap increased to 4 blocks).

### 2026-02-10: Azure SDK Research — Separate Package Recommendation

**By:** Shuri
**Date:** 2026-02-10

**What:** Azure monitoring should ship as a separate NuGet package (`Fritz.Aspire.Hosting.Minecraft.Azure`), not bundled with the core package. Azure SDK adds ~5 MB of dependencies most users don't need.

**For v1:** Polling approach, layered health (provisioning state + Resource Health API), `DefaultAzureCredential` for auth.

**Reference:** `docs/epics/azure-sdk-research.md`

### 2026-02-10: Resource Dependency Placement + RCON Rate-Limiting

**By:** Shuri
**Issue:** #29

**What:**
1. **RCON rate-limiting:** `CommandPriority` enum, token bucket rate limiter (default 10 commands/sec). High-priority commands bypass limits; low-priority commands queue in bounded channel (100, DropOldest).
2. **Dependency placement:** `ResourceInfo` carries `Dependencies` list from `ASPIRE_RESOURCE_{NAME}_DEPENDS_ON` env vars. `VillageLayout.ReorderByDependency()` uses BFS topological sort.
3. **Hosting integration:** `WithMonitoredResource()` accepts `params string[] dependsOn` and auto-detects `IResourceWithParent`.

**Status:** ✅ Resolved. Build passes, 303 tests pass.

### 2026-02-10: Ephemeral Minecraft world by default, WithPersistentWorld() opt-in

**By:** Shuri (requested by Jeffrey T. Fritz)
**What:** Removed the default named Docker volume from `AddMinecraftServer()`. World data is now ephemeral. Added `WithPersistentWorld()` for opt-in persistence.
**Why:** Persistent worlds cause confusion during development — old structures remain from previous sessions.
**Status:** ✅ Resolved.

### 2026-02-10: World Border Pulse on Critical Failure

**By:** Shuri
**Issue:** #28
**What:** `WorldBorderService` and `WithWorldBorderPulse()`. World border shrinks from 200→100 blocks over 10s when >50% of resources are unhealthy, restores to 200 over 5s on recovery. Red warning tint at 5 blocks from border edge.
**Why:** Dramatic visual/physical feedback for critical failures. Follows opt-in pattern (`ASPIRE_FEATURE_WORLDBORDER`).
**Status:** ✅ Implemented.

### 2026-02-10: Changelog, Symbol Packages, CodeQL Scanning

**By:** Wong
**Issue:** #26

**What:**
1. Changelog generation uses GitHub's built-in `generate_release_notes: true`.
2. NuGet symbol packages enabled via `IncludeSymbols`/`SymbolPackageFormat`. Release workflow pushes `.snupkg` explicitly.
3. CodeQL security scanning added as `.github/workflows/codeql.yml` — C# only, default query suite, weekly + push/PR triggers.
4. GitHub Pages/docfx deferred to a future sprint.

### Sprint 3 service lifecycle: no independent BackgroundServices for RCON-dependent features

**By:** Rocket
**What:** Converted HeartbeatService, RedstoneDependencyService, and ServiceSwitchService from `AddHostedService<>()` (independent BackgroundServices) to `AddSingleton<>()` called by MinecraftWorldWorker. Also fixed beacon tower positions to derive from VillageLayout instead of hardcoded offsets.
**Why:** Independent BackgroundServices start before RCON is connected and before resources are discovered, causing all Sprint 3 features to silently fail. The established pattern (used by WorldBorderService, AdvancementService, BeaconTowerService, etc.) is singleton + nullable constructor injection + calls from the main worker loop. Beacon positions used a hardcoded BaseZ=14 that overlapped with row-1 structures (z=10–16), blocking beacon sky access for 2 of 4 resources.
**Rule:** Any feature service that uses RCON or depends on discovered resources MUST be registered as `AddSingleton<>()` and called from MinecraftWorldWorker — never as an independent `AddHostedService<>()`.
**Status:** ✅ Resolved. All 303 tests pass. Build clean (0 errors, 0 warnings).
