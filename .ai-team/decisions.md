### 2026-02-10: NuGet hardening completed — floating deps pinned, SourceLink and deterministic builds added





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







### 2026-02-10: Redstone Dependency Graph — Design & Implementation (consolidated)

**By:** Jeffrey T. Fritz (idea), Rocket (implementation)
**Issue:** #36

**What:** Visualize Aspire resource dependencies as redstone wire circuits in the Minecraft world. Each resource has a structure, and redstone lines connect them to show the dependency graph. `RedstoneDependencyService` implements L-shaped routing (X then Z), repeaters every 15 blocks, redstone lamps at entrances, health-reactive circuit breaking/restoring. Originally a `BackgroundService`, later converted to `AddSingleton<>()` called from `MinecraftWorldWorker` (see: "Sprint 3 service lifecycle" decision).

**Key decisions:**
1. L-shaped routing avoids complex A* pathfinding.
2. Circuit breaking — remove redstone block + break wire every 5th position on unhealthy.
3. CommandPriority.Low for building to avoid starving higher-priority commands.
4. Wire positions at BaseY, Z-1 — paths run in front of structures.
5. Lever switches are visual-only (see: "Service Switches" decision) — they do not control Aspire resources.

**Technical considerations:**
- Redstone wires have a max range of 15 blocks — repeaters used for distant services
- Should respect dependency ordering — stopping a database should warn about dependent services
- Could use redstone signal strength to indicate health/load

**Status:** ✅ Implemented (lifecycle updated).






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







### 2026-02-10: Server Properties API — WithServerProperty + Enums + File Loading (consolidated)

**By:** Shuri

**What:** Added comprehensive server.properties configuration API:
1. `WithServerProperty(string, string)` and `WithServerProperties(Dictionary<string, string>)` extension methods, plus 6 convenience methods (`WithGameMode`, `WithDifficulty`, `WithMaxPlayers`, `WithMotd`, `WithWorldSeed`, `WithPvp`). All set env vars following the itzg/minecraft-server convention (property name → UPPER_SNAKE_CASE).
2. `ServerProperty` enum (24 members), `MinecraftGameMode` enum (4 members), `MinecraftDifficulty` enum (4 members), corresponding typed overloads.
3. `WithServerPropertiesFile()` for bulk property loading from disk.

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







### 2026-02-10: Azure Resource Group Integration — Epic Design & SDK Research (consolidated)

**By:** Rhodey (epic design), Shuri (SDK research)
**Date:** 2026-02-10
**Scope:** New epic — Azure Resource Group → Minecraft integration
**Document:** `docs/epics/azure-resource-group-integration.md`, `docs/epics/azure-sdk-research.md`

**Decisions Made:**
1. Separate NuGet package: `Fritz.Aspire.Hosting.Minecraft.Azure` — isolates Azure SDK dependencies (~5 MB most users don't need).
2. Azure monitor is a new resource discovery source, not a new rendering pipeline.
3. Polling for v1, Event Grid deferred. 30-second default interval.
4. Aspire-only for v1. Standalone mode is Phase 5.
5. `MaxResources = 50` default with auto-exclude of infrastructure noise.
6. `DefaultAzureCredential` as the default auth.
7. For v1: layered health (provisioning state + Resource Health API).

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







### 2026-02-10: Resource Village Layout & Themed Structures

**By:** Rocket
**Issue:** #25

**What:** Themed mini-buildings per Aspire resource type in a 2×N grid with 10-block spacing. Project=Watchtower, Container=Warehouse, Executable=Workshop, Unknown=Cottage. `VillageLayout` static class centralizes position calculations. Health indicator via redstone lamp in front wall.







### 2026-02-10: Service Switches — visual-only levers representing resource status (consolidated)

**By:** Rocket
**Issue:** #35

**What:** `ServiceSwitchService` with `WithServiceSwitches()` and `ASPIRE_FEATURE_SWITCHES` env var. Levers and lamps on each resource structure. Healthy=lever ON + glowstone, Unhealthy=lever OFF + unlit redstone lamp. Originally a `BackgroundService`, later converted to `AddSingleton<>()` called from `MinecraftWorldWorker` (see: "Sprint 3 service lifecycle" decision).

**Key decision:** Visual only — levers reflect state, they do not control Aspire resources. Manually flipping a lever in-game will be overwritten on next update cycle. This is by design for safety (prevents accidental resource control from game interface) and consistency with other display-only features (health lamps, holograms, boss bar, redstone dependency graph).

**Status:** ✅ Implemented (lifecycle updated).






### 2026-02-10: Village fence perimeter and pathway coordinate conventions

**By:** Rocket
**What:** Added `GetVillageBounds()` and `GetFencePerimeter()` to `VillageLayout`. Fence at ground level (`BaseY`), 4-block gap from buildings. Boulevard at `BaseX + StructureSize` (X=17). Future services placing things around the village edge should use these methods.
**Status:** ✅ Implemented (updated: fence moved to ground level, gap increased to 4 blocks).







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






### 2026-02-11: Minecraft building rules and constraints

**By:** Jeffrey T. Fritz (via Copilot)

**What:** When building structures and infrastructure in the Minecraft world, follow these mandatory constraints:

1. **Fences and barriers must sit ON the ground surface** — place at `BaseY` (y=-60 for superflat worlds), NOT `BaseY + 1`. Fences at y=-59 float in the air.

2. **Fences must be at least 4 blocks away from any building perimeter** — this provides adequate clearance and visual separation. The previous 1-2 block gap was too tight.

3. **Building footprint accounting** — when calculating fence perimeters around villages/groups, the village bounds already include the full structure footprint (7×7 for current structures). Offsets should be applied FROM those bounds, not from structure origins.

4. **Beacon placement must avoid structure overlap** — beacons require clear sky access. Position them dynamically based on structure size and layout (e.g., `z + StructureSize + 1`) rather than using hardcoded offsets that may conflict with multi-row grids.

5. **Ground level assumption** — for superflat worlds, `BaseY = -60` is the grass surface. Structures place floors at BaseY, walls at BaseY+1 and up, fences/paths at BaseY.

**Why:** These constraints ensure in-world structures render correctly in Minecraft:
- Floating fences look broken
- Structures too close to fences feel cramped
- Beacons without sky access don't show beams
- Y-level consistency prevents visual glitches

These rules were established after fixing Sprint 3 bugs where fences floated and beacons were blocked by structure overlap.







### 2026-02-11: Use GitHub issues and milestones for planning
**By:** Jeffrey T. Fritz (via Copilot)
**What:** Going forward, record all plans as issues and milestones in GitHub. Each sprint is a milestone.
**Why:** User directive — centralizes planning in GitHub for visibility and tracking. Replaces ad-hoc SQL/plan.md tracking.







### 2026-02-11: Sprint completion definition includes documentation
**By:** Jeffrey T. Fritz (via Copilot)
**What:** Going forward, all sprints must include updates to README and user documentation to be considered complete.
**Why:** User directive — documentation is a first-class deliverable, not an afterthought. Ensures features are always properly documented when shipped.







### 2026-02-11: Boss Bar Title Configuration

**Date:** 2026-02-11  
**Decider:** Rocket  
**Status:** Implemented


**Context:**

The boss bar previously displayed "Aspire Fleet Health: 100 percent" which looked unpolished. Additionally, users wanted the ability to customize the boss bar title text.


**Decision:**

1. **Changed percentage formatting** from "100 percent" to "100%" for cleaner display
2. **Added optional `title` parameter** to `WithBossBar()` extension method
3. **Used dedicated environment variable** `ASPIRE_BOSSBAR_TITLE` instead of repurposing `ASPIRE_APP_NAME`
4. **Default title** is "Aspire Fleet Health" when not specified


**Implementation:**

- `WithBossBar(string? title = null)` sets `ASPIRE_BOSSBAR_TITLE` env var if title provided
- `BossBarService` reads env var at construction with fallback to default
- Boss bar displays as: `"{title}: {percentage}%"`


**Rationale:**

- Dedicated env var is clearer than overloading `ASPIRE_APP_NAME`
- Optional parameter follows existing Fluent API pattern
- Default value maintains backward compatibility
- Percentage symbol is more concise and professional than "percent" word


**Impact:**

- Breaking change: `ASPIRE_APP_NAME` no longer affects boss bar (only title parameter does)
- API surface updated to show optional title parameter
- No change required for users who don't pass a title (default behavior preserved)







### 2026-02-10: Peaceful Mode Implementation

**Date:** 2026-02-10  
**Decider:** Rocket (Integration Dev)  
**Status:** Implemented


**Context:**

User requested a feature to eliminate hostile mobs (zombies, skeletons, creepers) from the Minecraft world to create a safer environment for monitoring infrastructure.


**Decision:**

Implemented `WithPeacefulMode()` extension method using `/difficulty peaceful` Minecraft command instead of gamerules.


**Rationale:**

1. **`/difficulty peaceful` vs gamerule approach:**
   - `difficulty peaceful` is the standard Minecraft way to eliminate hostiles
   - Immediately removes all existing hostile mobs
   - Prevents hostile mob spawning
   - Preserves passive mob spawning (cows, pigs, sheep)
   - More idiomatic than using `doMobSpawning` gamerule (which stops ALL mobs)

2. **One-time execution pattern:**
   - Command executes once at server startup after RCON connection
   - No service class needed — single RCON command is sufficient
   - Follows initialization pattern similar to `WorldBorderService.InitializeAsync()`

3. **Env var: `ASPIRE_FEATURE_PEACEFUL`**
   - Consistent with other opt-in features
   - Checked directly in `MinecraftWorldWorker.ExecuteAsync()` after resource discovery
   - No conditional DI registration needed (no service class)


**Implementation:**

- Extension method: `MinecraftServerBuilderExtensions.WithPeacefulMode()`
- Worker logic: Direct check in `MinecraftWorldWorker.ExecuteAsync()`
- Demo updated: Added `.WithPeacefulMode()` to Sprint 3 features
- API surface doc updated


**Alternatives Considered:**

- **Gamerule `doMobSpawning false`:** Stops ALL mob spawning including passives
- **Separate service class:** Overkill for single one-time command
- **Server property `DIFFICULTY=peaceful`:** Container-level, but less flexible for opt-in pattern


**Impact:**

- Opt-in feature, no effect on existing deployments
- All existing tests pass
- Consistent with team's feature opt-in architecture







### 2026-02-11: Village Structure Idempotent Building Pattern

**Date:** 2026-02-11  
**Decider:** Rocket (Integration Dev)  
**Status:** Implemented


**Context:**

Village structures were being rebuilt every 10-second display update cycle, causing visible glitching in-game. The `StructureBuilder.UpdateStructuresAsync()` method was calling `BuildResourceStructureAsync()` for every resource on every cycle without checking if the structure already existed.

Additionally, cobblestone paths were placed at `BaseY - 1` (Y=-61) which is underground in superflat worlds, making them invisible to players.


**Decision:**

Implemented idempotent building pattern for village structures:

1. **Structure Tracking**: Added `HashSet<string> _builtStructures` to track which resources have had their structures built
2. **Build Once Pattern**: Modified update loop to:
   - Check if structure already exists via `_builtStructures.Contains(name)`
   - If not built: call `BuildResourceStructureAsync()` and add to set
   - If already built: only update health indicator via `PlaceHealthIndicatorAsync()`
3. **Path Y-Level Fix**: Reverted all cobblestone path placements from `BaseY - 1` to `BaseY` so they sit on grass surface


**Rationale:**

- **Performance**: Eliminates redundant RCON commands for structure building every 10 seconds
- **Visual Stability**: Prevents the "glitching" effect where structures briefly change shape during rebuilds
- **Element Preservation**: Prevents structure rebuilds from overwriting decorative elements (switches, signs, lamps)
- **Path Visibility**: Paths at `BaseY` replace grass blocks and are visible/walkable; paths at `BaseY - 1` are buried in dirt

This follows the same pattern already established for fence (`_fenceBuilt` flag) and paths (`_pathsBuilt` flag).


**Consequences:**

- *Positive:*
- Buildings remain stable and don't glitch every 10 seconds
- Paths are visible and walkable on the ground surface
- Switches, signs, and other decorative elements persist correctly
- Reduced RCON command volume (better performance)
- All 303 existing tests pass

- *Negative:*
- Structures cannot be "refreshed" if manually destroyed in-game without restarting the worker
- Resource name is used as tracking key (if a resource is renamed and added again, it would build a new structure)

- *Neutral:*
- Pattern is consistent with existing fence/path building flags
- Health indicators still update dynamically every cycle as intended


**Implementation Details:**

**File**: `src/Aspire.Hosting.Minecraft.Worker/Services/StructureBuilder.cs`

- Added field: `private readonly HashSet<string> _builtStructures = new(StringComparer.OrdinalIgnoreCase);`
- Modified `UpdateStructuresAsync()` to check `_builtStructures` before building
- Reverted path Y-coordinates from `VillageLayout.BaseY - 1` to `VillageLayout.BaseY` in three locations (main boulevard, cross paths, entry path)


**Alternatives Considered:**

1. **Time-based rebuild**: Only rebuild structures every N minutes instead of every cycle
   - Rejected: Still causes glitching, just less frequently
2. **Change detection**: Compare current vs desired structure state and only update differences
   - Rejected: Too complex; requires querying and parsing Minecraft world state via RCON
3. **Manual refresh command**: Add an RCON command to force structure rebuild
   - Deferred: Could be added later if needed, but not required for normal operation


**Related Decisions:**

- Fence perimeter uses `_fenceBuilt` flag (similar pattern)
- Paths use `_pathsBuilt` flag (similar pattern)
- Service switches already placed once then only update state on transitions







### 2026-02-10: Structure Build Validation with Graceful Degradation

**By:** Shuri  
**What:** Added post-build validation to StructureBuilder that verifies door and window blocks were placed successfully after each structure builds.  
**Why:** RCON commands can fail silently or be rate-limited, leaving incomplete structures. Validation helps detect these failures and logs warnings for observability. Uses graceful degradation (log warnings, don't throw exceptions) to avoid blocking the entire village build process if individual blocks fail validation.

**Implementation:**
- `VerifyBlockAsync()` helper uses `testforblock` RCON command to check block type at coordinates
- Each structure type has a corresponding `Validate*Async()` method called after building
- Validates door air blocks and window blocks (glass_pane, stained_glass variants) at expected coordinates
- Returns false on any exception to handle RCON failures gracefully







### 2026-02-11: User Documentation Structure in user-docs/

**By:** Vision

**What:** Created comprehensive user documentation in `user-docs/` folder with guides for getting started, configuration, features, troubleshooting, and examples. Documentation follows consistent structure across all feature guides and emphasizes user perspective over technical implementation.

**Why:** 

1. **Separation of concerns:** User documentation (`user-docs/`) is separate from technical documentation (`docs/`). Users need "how to use" guides, not architecture deep-dives.

2. **Consistent structure:** Each feature document follows the same pattern (What It Does → How to Enable → What You'll See → Use Cases → Code Example) making docs scannable and predictable.

3. **User-centric language:** Documentation uses concrete, observable descriptions ("glowing yellow lamp", "fast high-pitched heartbeat") instead of technical implementation details ("glowstone block at Y+5", "1000ms interval, pitch 24").

4. **Comprehensive examples:** Every feature includes working code examples that users can copy-paste. Examples section shows real-world patterns for common scenarios (full stack apps, demos, ambient monitoring, etc.).

5. **Troubleshooting first:** Dedicated troubleshooting guide covers common issues with specific solutions, not generic advice. Organized by category (installation, startup, world generation, features, connection, performance).

6. **Progressive disclosure:** Documentation starts simple (README → Getting Started → Configuration) then provides deep-dives for specific features. Users can go as deep as they need.

**Impact:** Users can now understand and use all features without reading source code or technical architecture documents. Documentation is ready for external users (NuGet package consumers).







### 2026-02-10: Documentation path filters added to GitHub Actions workflows

**By:** Wong

**What:** Added `paths-ignore` filters to build.yml, release.yml, and codeql.yml to skip CI/CD pipelines when only documentation files change. Ignored paths: `docs/**`, `user-docs/**`, `*.md` (root-level), `.ai-team/**`.

**Why:** Documentation updates (README, user docs, team state) don't affect code correctness, test outcomes, or package output. Running full build/test/pack cycles wastes CI runner minutes and creates noise in the workflow history. This change ensures CI resources are spent only on actual code changes.

**Impact:** PRs and commits that only touch markdown files or documentation directories will not trigger builds, tests, or CodeQL scans. The scheduled CodeQL run (Mondays) is unaffected and always runs regardless of path filters.







### 2026-02-11: Dynamic terrain detection replaces hardcoded superflat Y=-60

**By:** Rocket
**What:** Added `TerrainProbeService` that uses RCON binary search (`setblock ... keep`) to detect surface height at startup. All village services now use `VillageLayout.SurfaceY` instead of hardcoded `BaseY`. Path building made terrain-agnostic (clears all blocks, not just grass_block). Falls back to BaseY=-60 if detection fails, preserving backward compatibility.
**Why:** The village was hardcoded to Y=-60 (superflat grass layer). This broke on any other world type (normal, amplified, custom). Dynamic detection makes the village work on ANY world type while keeping superflat as the safe fallback. Binary search keeps RCON usage minimal (~8 commands) and the probe is non-destructive (cleans up placed blocks immediately).







### 2026-02-12: User directive
**By:** Jeffrey T. Fritz (via Copilot)
**What:** SourceLink is not needed for this project. Remove it from Directory.Build.props.
**Why:** User request  captured for team memory







### 2026-02-12: User directive — Label issues by squad member
**By:** Jeffrey T. Fritz (via Copilot)
**What:** When creating GitHub issues and assigning them to a squad member, apply a label for that member so we can see who is working on what.
**Why:** User request — captured for team memory. Improves visibility into workload distribution on the GitHub issue board.


# Sprint 4 Technical Design Decisions

> **By:** Rhodey (Lead)
> **Date:** 2026-02-12
> **Status:** 📐 Design — pending team review

---






### Decision: Redstone Dashboard Wall placement west of village at X=-5

**What:** The Redstone Dashboard Wall is placed at `DashboardX = -5`, facing east toward the village. This is 11 blocks west of the fence perimeter — visible from the village gate but not overshadowing any buildings.

**Why:** Jeff specifically requested it "near the village but far enough away it isn't overshadowing the buildings." West placement uses negative-X space that is otherwise empty (village grows in positive-Z). The east-facing orientation means players see it when they exit the village gate and turn left.

**Trade-offs:** Could have placed it north (behind village) but that competes with village growth direction. Could have placed it further away but then it's outside view distance for players at the village.

---






### Decision: /clone shift-register for dashboard scrolling, not per-lamp updates

**What:** Each update cycle uses one `/clone` command to shift the entire lamp grid left by one column, then writes only the new rightmost column (N commands for N resources). Total: N+1 commands per cycle.

**Why:** Naive approach would update every lamp every cycle: N×columns commands. For 8 resources × 10 columns = 80 commands vs 9 commands with /clone. This is a 9× RCON savings, critical for staying within the 10 cmd/sec budget alongside other services.

**Risk:** `/clone` copies block states including redstone power. Must clone the power layer (x-1) not just the lamp layer (x). Tested in Paper 1.21 — `/clone` handles this correctly.

---






### Decision: Database resources get cylindrical buildings using circular geometry in 7×7 grid

**What:** Resources detected as databases via `IsDatabaseResource()` are built as cylindrical structures using polished deepslate, fitting within the existing 7×7 structure footprint. The circular footprint uses a 3-block radius approximation.

**Why:** Jeff requested "round/cylindrical buildings — like database cylinder icons in architecture diagrams." The 7×7 grid cell perfectly accommodates a radius-3 circle. Deepslate palette is dark and distinct from all other structure types.

**Trade-off:** Cylinder construction requires ~88 RCON commands vs ~15 for a watchtower. Acceptable because it's a one-time build, and database resources are typically <30% of total resources.

---






### Decision: Azure detection via resource type string matching, not SDK dependency

**What:** `IsAzureResource()` uses string matching on the resource type (starts with "azure.", contains "azure", or matches known Azure-only types like "cosmosdb", "servicebus"). No Azure SDK package reference needed.

**Why:** Avoids introducing `Azure.ResourceManager.*` dependencies into the main package, which was already decided against (separate package for Azure SDK integration). String matching works for the visual theming use case — we're just choosing a building color, not making API calls.

**Risk:** False positives are harmless (worst case: a non-Azure resource gets a blue banner). False negatives are unlikely since Aspire resource types are well-defined strings.

---






### Decision: Azure banner on ALL Azure resources regardless of structure type

**What:** The light_blue_banner is placed on the rooftop of any structure when `IsAzureResource()` returns true. This applies even to database cylinders (Azure SQL gets a cylinder + azure banner). The banner is additive — it doesn't change the building shape, just adds the flag.

**Why:** Jeff asked for "Azure-related resources should have a bright azure blue flag/banner on top." Making it additive means a resource's building shape communicates its function (database, project, container) while the banner communicates its origin (Azure). Players can spot Azure resources at a glance across the village.

---






### Decision: Sprint 4 scope is 14 issues — dashboard, buildings, Dragon Egg, DX polish, docs

**What:** Sprint 4 includes: Redstone Dashboard (4 issues), Enhanced Buildings (3 issues), Dragon Egg monument (1 issue), DX polish (3 issues: WithAllFeatures, env var tightening, welcome teleport), and documentation (3 issues: README, user-docs, tests).

**Why:** This balances Jeff's visual enhancement requests (dashboard, buildings, Dragon Egg) with the tech debt items recommended since Sprint 2 (WithAllFeatures, env var checks). Documentation is mandatory per Jeff's directive. Sculk Error Network and OTLP features defer to Sprint 5.

**Cut line:** If sprint runs long, drop welcome teleport first (M, nice-to-have), then Dragon Egg (L, can slip to Sprint 5 without blocking anything).

---






### Decision: HealthHistoryTracker as a separate class, not embedded in AspireResourceMonitor

**What:** Health history tracking (ring buffer per resource) lives in a new `HealthHistoryTracker` class, not added to `AspireResourceMonitor`.

**Why:** `AspireResourceMonitor` has a clear responsibility: discover resources and poll health. Adding time-series storage blurs that. `HealthHistoryTracker` is consumed only by the dashboard service — it's optional and shouldn't burden the core monitoring path. It's also independently testable.


# Sprint 4 Brainstorm: Aspire Observability Visualization Ideas

> **By:** Rhodey (Lead)  
> **Date:** 2026-02-12  
> **Requested by:** Jeffrey T. Fritz  
> **Status:** 💡 Brainstorm — ideas for team discussion and prioritization

---

## Context

Jeff asked: *"What would be fun to be able to browse through and wander around in Minecraft? Some way to visualize traces?"*

The project already visualizes resource health, dependencies, and status events. These ideas push into **observability data** — traces, metrics, logs, and request flows that Aspire collects via OpenTelemetry.

---

## Idea 1: Trace River

**Name:** Trace River  
**Extension method:** `WithTraceRiver()`  
**What it visualizes:** OpenTelemetry distributed traces — request flows across services  
**How it looks in Minecraft:**

Water channels flow between resource buildings, representing HTTP request paths. Each trace becomes a **boat** (or armor stand riding a boat) that spawns at the originating service's building and floats downstream through connecting channels to each service the request touched. The boat's color/name tag shows the trace ID. **Slow traces** (high latency) cause the water to turn to **honey blocks** (slow movement). **Error traces** turn the channel to **lava** briefly, with smoke particles. Each channel has **soul lanterns** along its banks showing the span count.

The channels are dug 2 blocks below surface level between buildings, with glass floors so you can watch boats from above. At each service building, there's a small "dock" where boats arrive and depart.

**Fun factor:** You literally *watch your requests flow* between services. Seeing a boat hit lava when a 500 error occurs is visceral. Walking along the river and following a single request through your system is the kind of thing you'd show at a conference and people would lose their minds.

**Technical feasibility:** **Medium.** Requires consuming OTLP trace data in the worker (new data source — currently only health polling). Water channel construction is straightforward RCON `/fill`. Boat spawning via `/summon`. The hard part is subscribing to trace data from the Aspire dashboard's OTLP collector — may need to run a secondary OTLP receiver in the worker or poll the dashboard API. Rate limiting boat spawns is critical (busy systems could spawn hundreds per second).

---

## Idea 2: The Enchanting Tower (Metrics Observatory)

**Name:** Enchanting Tower  
**Extension method:** `WithMetricsTower()`  
**What it visualizes:** Key metrics per resource — CPU%, memory, request rate, error rate  
**How it looks in Minecraft:**

A tall central tower (15-20 blocks high) at the village center, built from **enchanting tables, bookshelves, and amethyst**. Each monitored resource gets a floor/level in the tower with 4 indicator columns:

- **CPU:** A column of **magma blocks** (0-100% height). High CPU = tall glowing magma column.  
- **Memory:** A column of **blue ice** that grows upward as memory usage increases. Melts (becomes water) if memory drops.  
- **Request Rate:** **Note blocks** on a redstone clock — the tempo increases with request rate. You literally *hear* how busy a service is.  
- **Error Rate:** **Crying obsidian** column height — errors make the tower weep.

A spiral staircase lets players walk up and visually compare metrics across services. Each floor has a hologram sign with the service name and current values.

**Fun factor:** Standing at the top of the tower and looking down at which floors are glowing hot (CPU) vs weeping (errors) is an incredible overview. The note block tempo creates an ambient soundscape where you can *hear* your system's load.

**Technical feasibility:** **Medium-Hard.** Requires consuming OTLP metrics (gauges, counters). Column height updates via `/fill` are easy. Note block tempo requires careful redstone timing or repeated `/playsound` calls. The main risk is RCON throughput — 4 columns × N resources × update frequency could exceed the 10 cmd/sec budget. Needs batching or selective updates (only update changed values).

---

## Idea 3: Log Campfires

**Name:** Log Campfires  
**Extension method:** `WithLogCampfires()`  
**What it visualizes:** Application logs — especially errors and warnings  
**How it looks in Minecraft:**

Each resource building gets a **campfire** outside its front door. The campfire represents the service's log stream:

- **Normal logs (Info):** Regular campfire with gentle smoke particles — the service is humming along.  
- **Warnings:** Campfire becomes a **soul campfire** (blue flames) — something's off.  
- **Errors:** The campfire is replaced with a **fire block** (spreading flames) and **TNT** particles appear. If errors exceed a threshold, an actual TNT block spawns (doesn't detonate — just the visual threat).  
- **Log volume:** Smoke particle intensity matches log volume. A chatty service has a roaring campfire; a quiet service has gentle wisps.

Behind each building, a **wall of signs** (2-wide, 4-tall) shows the last 8 log lines, scrolling like a terminal. Signs update every poll cycle with the most recent entries, with error lines in red text.

**Fun factor:** Walking through the village and seeing which buildings are on fire vs. gently smoking is an instant error heatmap. The sign wall is the closest thing to `tail -f` in Minecraft. The TNT visual for error storms is *chef's kiss*.

**Technical feasibility:** **Medium.** Campfire block swaps are simple `/setblock` commands. Sign text via `/data merge` is well-supported in Paper. The challenge is log ingestion — need OTLP log receiver or dashboard API access. Rate-limiting sign updates is important (don't hammer RCON with sign changes on every log line). Batch to every 5-10 seconds.

---

## Idea 4: Nether Portal Request Gateway

**Name:** Nether Portal Gateway  
**Extension method:** `WithRequestGateway()`  
**What it visualizes:** HTTP request/response flows — counts, latencies, status codes  
**How it looks in Minecraft:**

Each service building gets a **Nether Portal frame** as its front entrance. The portal represents the service's HTTP endpoint:

- **Active portal (purple swirl):** Service is receiving requests normally.  
- **Portal deactivated (empty obsidian frame):** No traffic / service down.  
- **Frame material changes with status codes:**  
  - 2xx: Standard obsidian frame → purple portal active  
  - 4xx: Frame turns to **blackstone** with occasional enderman particles (client errors)  
  - 5xx: Frame turns to **crying obsidian** with dripping particles (server errors)  

Above each portal, a **hologram** shows: `GET /api/users → 200 (45ms)` for the last request.

Between connected services, **End Gateway blocks** (the starry portal block) create visual "wormholes" showing where requests travel. The space between portals pulses with **end rod** particles tracing the request path.

**Fun factor:** Walking through a Nether Portal to "enter" a service is the most Minecraft thing possible. Seeing your gateway frame crack and weep when 500s hit is dramatic. The wormhole effect between services is sci-fi gorgeous.

**Technical feasibility:** **Medium.** Portal construction is RCON `/fill` with obsidian + `/setblock` fire to activate. Material swaps for error states are simple block replacements. Hologram updates use existing DecentHolograms infrastructure. The hard part is getting HTTP metric data (request counts, status codes, latencies) — needs OTLP metric consumption. End Gateway blocks are creative-mode-only items, perfect for our flat world.

---

## Idea 5: Sculk Sensor Error Detection Network

**Name:** Sculk Sensor Network  
**Extension method:** `WithSculkErrorNetwork()`  
**What it visualizes:** Error propagation and cascading failures across services  
**How it looks in Minecraft:**

**Sculk veins and sensors** spread between resource buildings underground (at Y=-61, one block below surface). This creates a Warden-themed detection network:

- When a service throws errors, **sculk catalyst blocks** appear around its building, and **sculk veins** spread along the ground toward dependent services.  
- **Sculk sensors** placed along dependency paths activate (vibration particles) when errors propagate — you can see the error cascade ripple through the network.  
- If errors cascade to 3+ services, **sculk shriekers** activate with their distinctive warning sound and **Darkness** effect applied to players briefly. The *Warden is coming* = your system is about to have a very bad time.  
- Recovery clears the sculk, replaced with **moss blocks** (nature healing).

**Fun factor:** The Deep Dark is Minecraft's scariest biome. Using it to represent cascading failures is thematically perfect — errors spreading like sculk infection through your infrastructure is genuinely unsettling in the best way. The Darkness effect when things go really wrong is *immersive panic*. Recovery moss growing over the sculk is satisfying.

**Technical feasibility:** **Easy-Medium.** Sculk blocks are just block placements via `/setblock`. Sculk sensor activation is harder — they detect vibrations naturally, but we'd need to trigger them artificially (place/break blocks near them, or use `/playsound`). The Darkness effect is `/effect give @a minecraft:darkness 3`. Sculk spread animation would need timed block placement sequences. Main limitation: sculk blocks require 1.19+ (Paper supports this).

---

## Idea 6: Minecart Metric Rails

**Name:** Minecart Metric Rails  
**Extension method:** `WithMetricRails()`  
**What it visualizes:** Time-series metrics — throughput, latency percentiles, queue depths  
**How it looks in Minecraft:**

A **rail network** runs along the village perimeter with **minecarts carrying named items** that represent metric data points. Think of it as a physical strip chart recorder:

- Each metric gets a dedicated rail loop.  
- **Chest minecarts** travel the loop, carrying items that represent values: more items = higher value. The item type indicates the metric (gold ingots = requests/sec, redstone = latency ms, rotten flesh = errors).  
- Rail speed is controlled by **powered rails** — more powered rails = the metric is trending up fast.  
- At each service's building, a **hopper** collects minecarts, showing which services contribute to each metric.  
- A central **sorting station** with labeled item frames shows current values.

Players can ride the minecart to "follow" a metric's journey through the system.

**Fun factor:** A tiny factory-style conveyor belt system carrying your metrics around the village is delightful. The visual of chest carts piling up at a slow service (request queue growing) tells a story without numbers.

**Technical feasibility:** **Hard.** Minecart physics are client-side and can't be reliably controlled via RCON. Spawning them is easy (`/summon`), but managing their lifecycle (despawning old ones, preventing pileups) is complex. Rail construction is straightforward `/setblock` but needs careful powered-rail spacing. Hopper mechanics are server-side but hard to orchestrate via RCON. This idea is visually amazing but technically the hardest on the list.

---

## Idea 7: Villager Trading Hall (Dependency Marketplace)

**Name:** Villager Trading Hall  
**Extension method:** `WithDependencyTraders()`  
**What it visualizes:** Service-to-service API call patterns and data exchange rates  
**How it looks in Minecraft:**

A covered marketplace structure at the village center with **Villager NPCs** representing each service. Each villager has a profession matching its service type:

- **Projects:** Librarian (books = code)  
- **Containers:** Toolsmith (tools = infrastructure)  
- **Databases:** Cartographer (maps = data)  
- **Caches:** Cleric (potions = speed boosts)  

Villagers *walk between each other's stalls* to represent API calls. High-traffic pairs have villagers running back and forth faster. When a service goes down, its villager turns into a **Zombie Villager** (with the cure animation playing when it recovers).

Above the hall, **item frames** on the wall show what data is being exchanged — named items representing API endpoints.

**Fun factor:** Watching villagers hustle between stalls when traffic is high, then seeing one turn into a zombie when Redis goes down, is storytelling through game mechanics. The cure animation on recovery is a natural "healing" metaphor.

**Technical feasibility:** **Medium.** Villager spawning with professions is supported (`/summon villager ~ ~ ~ {VillagerData:{profession:"librarian"}}`). Movement between positions needs repeated `/tp` commands (villagers don't pathfind to coordinates on command). Zombie conversion via `/data merge` or kill+respawn. The RCON budget for moving villagers frequently could be tight — limit to 1 update per cycle, not real-time movement.

---

## Idea 8: Redstone Clock Dashboard

**Name:** Redstone Clock Dashboard  
**Extension method:** `WithRedstoneDashboard()`  
**What it visualizes:** Real-time system metrics as a large mechanical display  
**How it looks in Minecraft:**

A giant wall-mounted display (20×10 blocks) made of **redstone lamps** arranged in a grid — essentially a low-resolution LED scoreboard. Each row represents a resource, each column represents a time bucket (last 10 intervals):

- **Lamp ON (bright):** Healthy during that interval  
- **Lamp OFF (dark):** Unhealthy during that interval  
- **Redstone torch behind lamp:** Currently degraded (flickering effect via rapid on/off)

The display uses **comparators** and **repeaters** to create a shift-register effect — new data enters from the right, old data scrolls left. This is a physical, working Minecraft circuit (not just placed blocks).

Below the display, **concrete blocks** in different colors create a bar chart of response times per service (height = latency).

**Fun factor:** A working redstone scoreboard that updates in real-time is peak Minecraft engineering porn. The shift-register scroll effect makes it feel alive. Conference audiences who understand redstone will absolutely geek out. The concrete bar chart below gives a "mission control" feel.

**Technical feasibility:** **Easy-Medium.** Redstone lamp state via `/setblock` with or without redstone power. The "shift register" effect can be simulated by shifting block states left and placing new state at the right edge — no actual redstone circuit needed (RCON does the work). Concrete bar charts are simple `/fill` commands. This is one of the most RCON-friendly ideas on the list.

---

## Idea 9: Ender Chest Trace Explorer

**Name:** Ender Chest Trace Explorer  
**Extension method:** `WithTraceExplorer()`  
**What it visualizes:** Trace detail — span trees, timing breakdowns, attributes  
**How it looks in Minecraft:**

Each resource building contains an **Ender Chest** that, when the worker detects interaction (via server log monitoring), spawns a **trace exhibit** in a dedicated underground gallery (Y=-64 to -61):

- Each trace becomes a **hallway** branching off the main gallery. Length = total trace duration (1 block per 100ms).  
- **Span segments** are built as colored glass tunnel sections: green glass = fast spans, yellow = slow, red = error spans.  
- **Item frames** on walls display span names, durations, and attributes.  
- **Armor stands** at span boundaries hold named items showing the span operation name.  
- A **soul torch trail** guides players through the critical path (longest span chain).

Walking through a trace hallway literally lets you *walk the timeline* of a request — the distance you travel corresponds to the time the request took.

**Fun factor:** An underground museum of your traces where you physically walk through time is incredible for learning distributed tracing. Seeing a 2-second span as a 20-block-long red glass tunnel makes latency tangible. The critical path soul torch trail teaches you what to optimize.

**Technical feasibility:** **Hard.** Requires trace ingestion (same challenge as Trace River). The hallway construction is many RCON commands per trace (glass segments, item frames, armor stands). Space management is complex — need to clear old traces, manage gallery growth. Ender Chest interaction detection requires server log parsing. Best as a triggered/on-demand feature, not continuous.

---

## Idea 10: Dragon Health Egg

**Name:** Dragon Health Egg  
**Extension method:** `WithDragonEgg()`  
**What it visualizes:** Overall system uptime and SLO compliance  
**How it looks in Minecraft:**

A **Dragon Egg** sits atop a custom obsidian pedestal at the village center — the most precious block in Minecraft representing your system's overall health and uptime. Around the pedestal:

- **End Crystals** (one per monitored resource) float on obsidian pillars in a circle around the egg, beaming light upward when their resource is healthy. This mirrors the End dimension's crystal-and-dragon mechanic.  
- When a resource goes down, its End Crystal "explodes" (particle effect + sound, not actual explosion) and the beam disappears.  
- The Dragon Egg itself emits **portal particles** when uptime SLO is met (e.g., >99.9%). If SLO drops below threshold, the egg teleports to a random nearby position (the actual Dragon Egg mechanic — it runs away from you when clicked).  
- A ring of **End Stone** tiles around the base slowly fills with **Purpur blocks** as uptime accumulates — a physical progress bar toward your SLO target.

**Fun factor:** The Dragon Egg is the rarest block in Minecraft — one per world. Making it represent your system's SLO turns uptime into a treasure to protect. End Crystals exploding when services die is the most dramatic visualization on this list. The egg *running away* when SLO drops is hilarious and terrifying.

**Technical feasibility:** **Easy-Medium.** Dragon Egg placement via `/setblock`. End Crystal spawning via `/summon ender_crystal`. Crystal "explosion" via `/particle` + `/playsound` + `/kill` (the entity, not an actual explosion). Egg teleportation via `/setblock air` + `/setblock dragon_egg` at new coords. Purpur progress ring is simple `/fill`. The SLO calculation is just uptime tracking in the worker. One of the more RCON-efficient ideas.

---

## Summary Matrix

| # | Name | Data Source | RCON Cost | Feasibility | Fun Factor | Sprint Candidate |
|---|------|------------|-----------|-------------|------------|-----------------|
| 1 | Trace River | OTLP Traces | Medium | Medium | ⭐⭐⭐⭐⭐ | S5-S6 |
| 2 | Enchanting Tower | OTLP Metrics | High | Medium-Hard | ⭐⭐⭐⭐ | S5-S6 |
| 3 | Log Campfires | OTLP Logs | Low-Medium | Medium | ⭐⭐⭐⭐ | S5 |
| 4 | Nether Portal Gateway | OTLP Metrics (HTTP) | Low | Medium | ⭐⭐⭐⭐ | S5 |
| 5 | Sculk Error Network | Health + Traces | Low | Easy-Medium | ⭐⭐⭐⭐⭐ | S4-S5 |
| 6 | Minecart Metric Rails | OTLP Metrics | High | Hard | ⭐⭐⭐ | Backlog |
| 7 | Villager Trading Hall | OTLP Traces | Medium | Medium | ⭐⭐⭐⭐ | S5-S6 |
| 8 | Redstone Clock Dashboard | Health history | Low | Easy-Medium | ⭐⭐⭐⭐ | S4 |
| 9 | Ender Chest Trace Explorer | OTLP Traces | High | Hard | ⭐⭐⭐⭐⭐ | S6+ |
| 10 | Dragon Health Egg | Health + SLO | Low | Easy-Medium | ⭐⭐⭐⭐⭐ | S4 |

---

## Rhodey's Recommendation

**Sprint 4 candidates (use existing health data, no new data source needed):**
1. **Dragon Health Egg** (#10) — Low RCON cost, easy feasibility, maximum fun. Ship it.
2. **Redstone Clock Dashboard** (#8) — Health history is already tracked. Just needs time-series storage + lamp grid.
3. **Sculk Error Network** (#5) — Mostly uses existing health data with some error cascade logic.

**Sprint 5 candidates (requires OTLP data ingestion — the big architectural investment):**
1. **Trace River** (#1) — Jeff specifically asked about traces. This is the headline feature.
2. **Log Campfires** (#3) — Low RCON cost, high visual impact.
3. **Nether Portal Gateway** (#4) — Natural extension of the village metaphor.

**Backlog:**
- Enchanting Tower, Minecart Rails, Villager Hall, Trace Explorer — all great but need the OTLP infrastructure first and have higher RCON budgets.






### Critical Architectural Decision Needed

All ideas numbered 1-4, 6-7, and 9 require **consuming OTLP data** (traces, metrics, logs) in the worker service. Today the worker only polls health endpoints. Adding OTLP ingestion is a **cross-cutting architectural change** that should be designed once and implemented as shared infrastructure before any individual feature. This is likely a Sprint 5 epic in itself.

Options:
- **A) Run a secondary OTLP receiver in the worker** — most control, but adds complexity.
- **B) Poll the Aspire dashboard API** — simpler, but the dashboard API isn't designed for this.
- **C) Share the OTLP collector and query stored data** — cleanest but depends on Aspire internals.

This decision should be made before committing to any OTLP-dependent feature.


# Decision: Sprint 4 Building Design Specifications

**By:** Rocket
**Date:** 2026-02-11
**Status:** Proposed

## What

Design specifications for four Sprint 4 building enhancements requested by Jeff:






### 1. Database Cylinder Building
- Radius-3 circle (7-block diameter) fits perfectly in the existing 7×7 grid cell
- Smooth stone walls + polished deepslate cap = "data center" aesthetic
- Domed roof (2-layer: smooth stone slab outer ring, polished deepslate slab inner cap)
- 7 blocks total height (floor + 4 wall layers + 2 dome layers)
- Door on south face (z+0), 3-wide × 2-tall opening
- Health lamp at (x+3, y+3, z+0) — above the door
- ~60 RCON commands to build (3x more than rectangular buildings due to per-row geometry)
- New structure type "Cylinder" in `GetStructureType` for database/postgres/mysql/sqlserver/redis/mongodb resource types






### 2. Azure Flag/Banner
- `light_blue_banner` base with white stripe (`str`) and base (`bs`) patterns
- NBT: `{Patterns:[{Color:0,Pattern:"str"},{Color:0,Pattern:"bs"}]}`
- Placed on rooftop flagpole (2-block oak fence + banner), same pattern as existing Watchtower flag
- Azure detection via `info.Type.Contains("azure")` or name match
- Roof Y varies by structure type (documented per-type)






### 3. Enhanced Building Palettes
- **Watchtower:** Chiseled stone floor, deepslate pillars, polished andesite band, battlements, bookshelves + lantern interior
- **Warehouse:** Orange concrete accent stripe (shipping container look), gray glass, iron trapdoor corrugated roof, chains + soul lanterns
- **Workshop:** Spruce timber frame, dark oak peaked roof, blast furnace + smithing table, redstone torches
- **Cottage:** Mossy cobblestone accents, stripped oak timber frame band, peaked oak stair roof, flower pots + awning






### 4. Dashboard Wall
- 20×10 block frame (polished blackstone) with 18×8 usable redstone lamp grid
- Placement: (X=10, Y=SurfaceY+2, Z=-12) — behind village, facing south
- Block-swap technique (glowstone=lit, redstone_lamp=unlit, gray_concrete=unknown) — no redstone wiring needed
- `/clone` scroll: copies columns 12-28 → 11-27, then writes fresh data at column 28
- Black concrete backing panel for contrast
- Title sign: "ASPIRE Health Dashboard"

## Why

Jeff wants Sprint 4 to include more visually distinct buildings, database-specific structures, Azure branding, and a health dashboard. These designs fit within the existing village grid system and follow established RCON patterns from Sprints 1-3.

## Implementation Notes

- Full RCON command sequences documented in `docs/designs/minecraft-building-reference.md`
- All commands use `CommandPriority.Low` for bulk building
- Door openings cleared LAST in all build sequences (learned from Sprint 3.1)
- Cylinder building is the most RCON-expensive structure (~60 commands vs ~20 for rectangular)
- Dashboard `/clone` is 1 command per scroll tick — very efficient







### Cylinder & Azure resource detection precedence

**By:** Rocket
**Date:** 2026-02-12

**What:** `GetStructureType()` now checks `IsDatabaseResource()` before `IsAzureResource()`. Resources that are both database AND Azure (e.g., `cosmosdb`, `azure.sql`) get the Cylinder structure shape, with the azure banner added as a post-build overlay via `PlaceAzureBannerAsync()`.

**Why:** The database cylinder icon is a stronger visual signal for data stores than the Azure color palette. Azure identity is communicated additively via the rooftop banner, which works on any structure type. This means a CosmosDB resource looks like a database cylinder with an azure flag on top — both identities are visible.

**Impact:** `IsDatabaseResource` and `IsAzureResource` are both `internal static` methods on `StructureBuilder`, available for other services (e.g., Nebula's tests). The detection lists are intentionally broad — `Contains()` matching catches compound types like `azure.postgres` or `sql-server-2022`.







### Visual Bug Fixes: Structure Elevation & Health Lamp Alignment

**By:** Rocket
**What:** Fixed two visual bugs: (1) structures placed 1 block below ground — `GetStructureOrigin()` now returns `SurfaceY + 1`; (2) Warehouse health lamp overlapping cargo door — lamp moved to `y+4` for structures with 3-tall doors.
**Why:** SurfaceY is the topmost solid block. Placing floors there replaces the grass and buries walls. Health lamps at `y+3` overlap 3-tall doors (y+1 to y+3). Both fixes are surgical (VillageLayout.cs, StructureBuilder.cs) with updated tests.
**Status:** ✅ Resolved







### Feature env var checks now require exact `"true"` value

**By:** Shuri
**What:** All `ASPIRE_FEATURE_*` env var checks in `Program.cs` changed from `!string.IsNullOrEmpty(...)` to `== "true"`. This affects 16 feature registrations (15 service DI registrations + 1 peaceful mode check in `ExecuteAsync`). The `With*()` extension methods in `MinecraftServerBuilderExtensions.cs` already set all feature env vars to `"true"`, so no changes were needed on the hosting side.
**Why:** Prevents accidental feature activation from empty strings, whitespace, or junk values in environment variables. Only an explicit `"true"` value activates a feature now.
**Impact:** Any code that sets `ASPIRE_FEATURE_*` env vars must use the exact string `"true"` (lowercase). Other truthy values like `"1"`, `"yes"`, or `"True"` will NOT activate features.
**Status:** ✅ Implemented on sprint-4 branch.







### Village Spacing increased from 10 to 12

**By:** Rocket
**What:** Changed `VillageLayout.Spacing` from 10 to 12, giving a 5-block walking gap between 7×7 structures (was 3 blocks).
**Why:** Buildings were too close together — a 3-block gap between structures made the village feel cramped and hard to navigate. 5 blocks is comfortable for player walking and allows room for doors, switches, and decorative elements without collision. DashboardX and fence perimeter calculations are unaffected (both derive positions dynamically).
**Status:** ✅ Resolved. All 382 tests updated and passing.


# Decision: Explicit Session Lifetime for Minecraft Container

**Date:** 2026-02-11
**Author:** Shuri (Backend Dev)
**Status:** Implemented

## Context

Jeff reported that Docker Desktop sometimes caches container state between Aspire runs, so even without `WithPersistentWorld()` the Minecraft server could retain world data across restarts.

## Decision

Added `.WithLifetime(ContainerLifetime.Session)` to the `AddMinecraftServer()` builder chain in `MinecraftServerBuilderExtensions.cs`.

## Rationale

- `ContainerLifetime.Session` is already the Aspire default, but being explicit:
  1. Documents the intent that ephemeral servers get a truly fresh container each run
  2. Protects against any future Aspire default changes
  3. Makes the behavior discoverable in code review
- The itzg/minecraft-server image stores world data in `/data`. Without a named volume (added by `WithPersistentWorld()`), data lives in the container's writable layer. Session lifetime ensures the container itself is destroyed and recreated.
- No sample app changes needed — the library handles it.

## Alternatives Considered

- **`FORCE_WORLD_COPY=true` env var**: Image-specific, doesn't address container caching.
- **`docker volume rm` documentation**: Manual step, bad DX.
- **Do nothing**: Session is already the default, but Docker Desktop behavior is unpredictable without explicit intent.

## Impact

- `MinecraftServerBuilderExtensions.cs`: 1 line added to builder chain.
- No breaking changes. No new dependencies.







### 2026-02-12: Dashboard lamps use self-luminous blocks instead of redstone power

**By:** Rocket
**What:** Replaced the redstone power layer (`redstone_block` at `x-1` behind `redstone_lamp` at `x`) with direct self-luminous block placement. Healthy = `glowstone`, Unhealthy = `redstone_lamp` (unlit), Unknown = `sea_lantern`. The `/clone` scroll now operates on the lamp layer directly.
**Why:** RCON-issued `setblock redstone_block` does not reliably trigger block updates on Paper servers, causing lamps to light briefly then go dark. Self-luminous blocks require no power propagation — their lit/unlit state is intrinsic to the block type, making the display 100% reliable regardless of server tick timing.
**Status:** ✅ Resolved. Build passes, all 382 tests pass. RCON command count per update cycle halved.





### Language-Based Color Coding for Village Buildings

**By:** Rocket
**Date:** 2026-02-12
**Requested by:** Jeffrey T. Fritz

**What:** Village buildings now use language/technology-specific colors for wool trim and banners instead of a uniform blue. Color mapping: .NET Project → purple, JavaScript/Node → yellow, Python → blue, Go → cyan, Java → orange, Rust → brown, Unknown → white.

**Why:** With multiple projects in a village, all-blue buildings made it impossible to distinguish technology types at a glance. Color coding gives immediate visual feedback about what language/framework each resource uses. Purple for .NET aligns with the .NET brand color. The mapping is based on `resourceType` (all Aspire-hosted projects are .NET → "Project" type → purple) and falls back to name/type substring matching for containers running Node, Python, Go, Java, or Rust workloads.

**Scope:**
- Watchtower (Project): wool trim at y+8 + wall banner on flagpole — both use language color
- Cottage (default/unknown): wool trim at y+4 — uses language color
- Warehouse, Workshop: no wool trim, unchanged
- Cylinder, AzureThemed: own identity materials, unchanged

**Implementation:** `GetLanguageColor(string resourceType, string resourceName)` returns `(wool, banner, wallBanner)` block ID tuple. `BuildWatchtowerAsync` and `BuildCottageAsync` now accept `ResourceInfo` to pass type/name. The method is `internal static` for testability.

**Also fixed:** Watchtower and Azure banner placement — banners were floating standing banners disconnected from the flagpole. Now uses `wall_banner[facing=south]` attached to an extended flagpole.

**Status:** ✅ Implemented. All 382 tests pass.





### 2026-02-12: Integration testing strategy — Hybrid RCON + BlueMap approach

**By:** Rhodey
**What:** Designed integration testing strategy for verifying Minecraft village construction. Evaluated 4 approaches: BlueMap REST API (not viable — no block-level endpoints), Playwright screenshots (good for visual regression, poor for correctness), RCON block verification (reliable and deterministic), and Hybrid RCON + BlueMap (recommended).

**Why:** We need confidence that the worker builds structures correctly at the right coordinates. RCON `execute if block X Y Z <block>` gives exact, immediate, deterministic block-level assertions using our existing `RconClient`. BlueMap's web API only serves pre-rendered tile files — no block query endpoint exists. Playwright screenshot comparison is fragile (non-deterministic 3D rendering, BlueMap version sensitivity, render timing) and should be secondary.

**Key decisions:**
1. **Primary verification via RCON** — `execute if block` for exact coordinate assertions. Zero rendering delay, uses existing infrastructure.
2. **Secondary verification via BlueMap HTTP** — smoke test that BlueMap loads and has map data. Full Playwright screenshots deferred until RCON tests are stable.
3. **Shared test fixture** — Single `MinecraftAppFixture` using `DistributedApplicationTestingBuilder` starts the full AppHost once per test run. Server startup is 45–60s; cannot afford per-test startup.
4. **Separate test project** — `Aspire.Hosting.Minecraft.IntegrationTests` in `tests/`. These are slow tests (2–3 min) that require Docker.
5. **CI runs on Linux only** — Minecraft container is Linux-based. Integration tests run as a separate job in `build.yml`, gated behind unit tests passing. PR CI skips integration tests to avoid 3-minute penalty.
6. **Poll-based readiness** — Fixture polls `execute if block` on a known coordinate every 5s until village is built (max 3 min timeout). More reliable than fixed delays.
7. **First 5 tests** — Fence perimeter, cobblestone paths, watchtower structure, health indicator, BlueMap web UI loads.

**Full design:** `docs/designs/bluemap-integration-tests.md`

**Status:** ✅ Design complete. Ready for implementation.






### 2026-02-12: User directive — Famous Building API
**By:** Jeffrey T. Fritz (via Copilot)
**What:** Add `.AsMinecraftFamousBuilding(BigBenClockTower)` extension method on any Aspire resource, backed by an enum of available famous buildings with fixed building models. Syntax: `.AsMinecraftFamousBuilding(FamousBuilding.BigBenClockTower)`. Each enum value maps to a fixed, detailed Minecraft structure definition.
**Why:** User request — allows developers to assign iconic real-world building representations to their Aspire resources for a more visually rich and personalized Minecraft village experience. Planned for a future sprint.




### 2026-02-12: Fritz's horses easter egg  always present in village
**By:** Jeffrey T. Fritz (via Copilot)
**What:** Three horses are always spawned in the village fence area, named after Fritz's real horses: Charmer (black), Dancer (brown paint), and Toby (Appaloosa). This is not feature-gated  it's an always-on easter egg.
**Why:** User request  captured for team memory




### 2026-02-12: User directive — MonitorAllResources convenience API
**By:** Jeffrey T. Fritz (via Copilot)
**What:** Add a `.MonitorAllResources()` extension method on the Minecraft server resource that automatically discovers and creates buildings for all non-Minecraft resources in the Aspire distributed application. Should exclude the Minecraft server itself and its related resources (worker, BlueMap, etc.) from monitoring.
**Why:** User request — reduces boilerplate in AppHost Program.cs. Instead of manually calling `.WithMonitoredResource()` for each resource, developers can call one method to monitor everything. Planned for next sprint alongside Famous Buildings feature.




### 2026-02-12: Famous Buildings API design
**By:** Rhodey
**What:** Designed the `AsMinecraftFamousBuilding(FamousBuilding)` extension method and `FamousBuilding` enum for assigning iconic real-world buildings (Big Ben, Eiffel Tower, Colosseum, Pyramid, etc.) to any Aspire resource. The API lives on `IResourceBuilder<T> where T : IResource` — not on the Minecraft server builder — because the building choice belongs to the resource being visualized. Selection flows via `FamousBuildingAnnotation` → `WithMonitoredResource` deferred env var callback → `ASPIRE_RESOURCE_{NAME}_FAMOUS_BUILDING` env var → worker reads and overrides auto-detected structure type. Enum has 15 buildings spanning 6 continents, all constrained to 15×15 footprint. Building models are pure C# methods (no JSON/NBT), matching the existing `StructureBuilder` pattern. Feature requires `WithGrandVillage()` for full-size rendering. Full design at `docs/designs/famous-buildings-design.md`.
**Why:** Jeff wants conference demos where resources are represented by recognizable landmarks. The annotation-based approach is order-independent, the env var pattern matches all existing resource metadata flow, and the enum keeps the API surface small and intentional. Two-sprint phasing (API+3 buildings, then remaining 12) avoids a single oversized sprint. Famous buildings override auto-detection but don't break it — resources without annotations continue to work exactly as before.

#

### Key Decisions

1. **Extension method targets `IResourceBuilder<T> where T : IResource`** — broadest constraint; annotation is inert unless resource is monitored.
2. **Annotation + deferred env var callback** — guarantees order-independence (can call `AsMinecraftFamousBuilding` before or after `WithMonitoredResource`).
3. **Pure C# building models** — no JSON schemas, no NBT files, no runtime file loading. Consistent with existing `StructureBuilder` pattern.
4. **15 buildings in the enum** — geographic diversity, Minecraft buildability, distinctive silhouettes at 15–30 block scale.
5. **Requires `WithGrandVillage()`** — famous buildings at 7×7 would be unrecognizable. Worker logs warning and falls back to auto-detection if grid is too small.
6. **Two-sprint phasing** — Sprint A: API + infrastructure + 3 starter models. Sprint B: remaining 12 models. Avoids monolithic sprint.
7. **200 RCON command hard cap per building** — prevents individual models from becoming performance problems.




### 2026-02-12: MonitorAllResources convenience API design
**By:** Rhodey
**What:** Design for a `.MonitorAllResources()` extension method that auto-discovers all non-Minecraft resources in the Aspire application and monitors them in-world, replacing manual `.WithMonitoredResource()` calls. Includes `ExcludeFromMonitoring()` opt-out API, structural exclusion of Minecraft infrastructure (server, worker, children), duplicate prevention, and Famous Building annotation passthrough.
**Why:** Jeff's directive to reduce AppHost boilerplate. Five manual `WithMonitoredResource` calls → one `MonitorAllResources()` call. The convenience API composes cleanly with existing manual calls and doesn't introduce new paradigms. Eager discovery (Option A) chosen over deferred eventing for predictability, debuggability, and consistency with existing `WithMonitoredResource` behavior. Full design at `docs/designs/monitor-all-resources-design.md`.


# Sprint 4 Technical Design Decisions

> **By:** Rhodey (Lead)
> **Date:** 2026-02-12
> **Status:** 📐 Design — pending team review

---



### Decision 1: VillageLayout constants become mutable properties

**What:** `Spacing`, `StructureSize`, and `FenceClearance` change from `const int` to `static int { get; private set; }` with default values matching Sprint 4. A `ConfigureGrandLayout()` method sets them to Grand Village values.

**Why:** Preserves backward compatibility. Without `WithGrandVillage()`, the village is identical to Sprint 4. Avoids a hard fork of `VillageLayout` into two classes. All existing services use `VillageLayout.Spacing` etc. — they don't need code changes, just recompilation.

**Risk:** Mutable statics are a code smell. Mitigated by: `private set`, called once at startup, no thread contention (single-threaded init in `Program.cs`).

---



### Decision 2: Structure size is 15×15, not 11×11 or 21×21

**What:** All buildings expand to 15×15 footprint (13×13 usable interior).

**Why:** 11×11 (9×9 interior) is too small for meaningful multi-floor buildings with staircases — the spiral staircase alone needs 3×3, leaving only 6×6 per floor. 21×21 would be impressive but the RCON cost balloons (>200 commands for a watchtower), spacing goes to 32+ blocks, and the village exceeds world border with just 4 resources. 15×15 is the sweet spot — room for 3 floors with furniture, staircases fit, RCON stays under ~100 commands per building.

---



### Decision 3: Spacing is 24 blocks (15 + 9 gap)

**What:** `Spacing` increases from 12 to 24.

**Why:** Building is 15 blocks wide. Need 9 blocks between buildings for: 3-block walking path + 3-block rail corridor + 3-block walking path. This gives room for rails to run between buildings without clipping walls, and players can walk alongside rails.

**Trade-off:** Village Z-extent doubles per row. 8 resources = Z ~110 blocks. Requires `MAX_WORLD_SIZE` bump to 512.

---



### Decision 4: MAX_WORLD_SIZE bumps from 256 to 512

**What:** Default world border diameter doubles.

**Why:** At 24-block spacing, 8 resources need Z ~110 blocks. With fence clearance and margin, 256 blocks is too tight. 512 gives comfortable room for 20 resources. Memory impact is minimal (~10 MB additional for chunk data in a superflat world).

---



### Decision 5: Minecart rails coexist with redstone wires, not replace them

**What:** `WithMinecartRails()` is a separate feature from `WithRedstoneDependencyGraph()`. Both can be active simultaneously. Rails are offset by 1 block in X from redstone wires.

**Why:** Redstone wires have health-reactive behavior (break on unhealthy, restore on recovery) that's visually distinct and valuable. Rails add a second visual language — physical connection you can ride. Replacing redstone with rails loses the health-reactive visual. Coexistence gives users the choice.

---



### Decision 6: RCON burst mode for initial construction

**What:** `RconService` gets an `EnterBurstMode()` method that temporarily increases `MaxCommandsPerSecond` from 10 to 40 during initial village build.

**Why:** A 6-resource Grand Village with rails sends ~600 commands. At 10 cmd/sec = 60 seconds. At 40 cmd/sec = 15 seconds. The Minecraft server can handle 40 RCON commands/sec for short bursts — the tick budget is 50ms per tick, and simple `/setblock` + `/fill` commands typically complete in <1ms each. Steady-state (health updates) stays at 10 cmd/sec.

---



### Decision 7: Grand Village is opt-in via `WithGrandVillage()`

**What:** New feature is behind a feature flag, not a default behavior change.

**Why:** Breaking the default experience is unacceptable for existing users. The standard 7×7 village is fast to build, works within 256-block world border, and is conference-demo-proven. Grand Village is for users who want the immersive experience and are willing to accept longer build times and larger world requirements.

---


# Decision: Sprint 4 Building Design Specifications

**By:** Rocket
**Date:** 2026-02-11
**Status:** Proposed

## What

Design specifications for four Sprint 4 building enhancements requested by Jeff:



### 2026-02-12: Fritz's horses are always-on, not feature-gated
**By:** Rocket
**What:** HorseSpawnService registered as a plain singleton with non-nullable injection into MinecraftWorldWorker. No ASPIRE_FEATURE_ env var or opt-in check. Horses spawn unconditionally after village structures are built.
**Why:** Easter eggs should be discovered, not configured. Adding a feature flag would defeat the purpose. The service is cheap (3 RCON commands, runs once) and the horses add personality to every village. Fritz's real horses — Charmer, Dancer, and Toby — deserve to always be present.


# Village Spacing & Fence Clearance Update

**Date:** 2026-02-12
**Author:** Rocket (Integration Dev)
**Requested by:** Jeffrey T. Fritz

## Decision

1. **Village spacing doubled from 12 to 24 blocks** — provides 17-block walking gap between 7×7 structures for a much more spacious village feel.
2. **Fence clearance increased from 4 to 10 blocks** — gives horses and players generous roaming space between buildings and the perimeter fence.
3. **Forceload area expanded from `(-10,-10)-(80,80)` to `(-20,-20)-(120,120)`** — covers the larger village footprint with extra margin.
4. **Horse CustomName uses simple string format** — matches `GuardianMobService` pattern (`CustomName:"\"Name\""`) instead of JSON text component, fixing raw JSON display on Paper servers.
5. **Horse spawn Z moved from `BaseZ-2` to `BaseZ-6`** — centers horses in the wider clearance area.

## Rationale

The original 12-block spacing with 7×7 structures left only a 5-block gap — cramped for visual appeal and horse movement. Doubling to 24 blocks creates a proper village feel with wide streets. The 10-block fence clearance gives horses room to trot without clipping into buildings.

## Impact

All layout-dependent services automatically inherit the new positions via `VillageLayout.GetStructureOrigin()`. Test expectations updated in 4 test files. All 382 tests pass.




### 2026-02-12: VillageLayout constants converted to configurable properties

**By:** Shuri
**What:** `Spacing`, `StructureSize`, and `FenceClearance` are now `static int { get; private set; }` instead of `const`. `ConfigureGrandLayout()` sets Grand Village values. `ResetLayout()` (internal) restores defaults for test isolation.
**Why:** Foundation for Milestone 5 Grand Village. Every other issue depends on these being configurable. Default values match Sprint 4 exactly so there's zero regression without the feature flag. `FenceClearance` was introduced to replace the hardcoded 10-block fence gap.


# Decision: RCON Burst Mode API

**Author:** Rocket
**Date:** 2026-02-12
**Issue:** #85

## Context

Milestone 5 Grand Village buildings generate 50–140 RCON commands each. At the steady-state 10 cmd/sec rate, a 6-resource village takes ~60 seconds to build. Burst mode temporarily raises throughput during initial construction.

## Decision

`RconService.EnterBurstMode(int commandsPerSecond = 40)` returns `IDisposable`. Usage:

```csharp
using (rcon.EnterBurstMode())
{
    await BuildAllStructuresAsync(ct);
}
// Rate limit automatically restored to 10 cmd/sec
```

- Thread-safe: only one burst session at a time (SemaphoreSlim).
- Throws `InvalidOperationException` if burst mode is already active.
- Logged at INFO on enter/exit.

## Who Needs to Know

- **Shuri** — no hosting API changes needed; burst mode is internal to the worker.
- **Rhodey** — aligns with Sprint 5 design doc §6 "RCON Burst Mode Design."
- **Nebula** — unit tests for burst mode should cover: enter/exit logging, double-enter rejection, dispose restoration, thread safety.


### 2026-02-12: Integration test infrastructure uses xUnit Collection + Aspire Testing Builder

**By:** Nebula
**What:** Created `tests/Aspire.Hosting.Minecraft.Integration.Tests/` with `MinecraftAppFixture` using `DistributedApplicationTestingBuilder` and xUnit `[Collection("Minecraft")]` pattern. All integration tests share a single Minecraft server instance per test run.
**Why:** Minecraft server startup takes 30–60s — per-test startup is not feasible. The collection fixture pattern ensures one server per run. The `app.GetEndpoint("minecraft", "rcon")` API returns a `Uri` for RCON connectivity. Poll-based readiness (`execute if block` every 5s) is more reliable than fixed delays.
**Affects:** Any future integration tests must use `[Collection("Minecraft")]` and inject `MinecraftAppFixture`. The fixture handles RCON connection and village build readiness.


### 2026-02-12: Use "milestones" not "sprints"
**By:** Jeffrey T. Fritz (via Copilot)
**What:** Refer to work phases as "milestones" instead of "sprints" going forward.
**Why:** User request — captured for team memory


# Decision: v0.5.0 Release Blog Post Structure & Messaging

**Date:** February 2026  
**Author:** Mantis (Blogger)  
**Task:** Write v0.5.0 release blog post (Milestone 5: Grand Village)

---

## What Was Done

Created `docs/blog/sprint-5-release.md` — a 2,800-word release post for v0.5.0, covering:

1. **Hook** — "The village got an upgrade" framing Grand Village as an iterative improvement on existing small village
2. **Building-by-Building Tour** — Architectural details for each grand building type (Watchtower, Warehouse, Workshop, Silo, Azure Pavilion, Cottage), emphasizing walkability and interior detail
3. **Minecart Rails Feature** — Explained as "your dependencies on track," with before/after behavior and visual impact
4. **DoorPosition Architecture Insight** — Highlighted the refactor as an example of invisible architecture that enables all other systems
5. **Bug Fixes Section** — Four fixes tied to the Grand Village rollout (watchtower switches, glow blocks, silo entrance, service adaptation)
6. **Code Comparison** — Toggle between small and grand village modes using identical fluent API syntax
7. **Performance & Compatibility** — Addressed potential concerns upfront (minecart load, chunk optimization, backwards compatibility)
8. **What's Next Tease** — Azure citadel integration and conference demo positioning
9. **Install CTA** — NuGet + GitHub links + user docs reference

---

## Key Decisions Made

### 1. Structure Deviates from Previous Release Posts
**Decision:** Used building-by-building tour instead of "features → code → what's next" structure.

**Why:** v0.5.0 is about *experience* (walking inside your infrastructure) more than mechanics. Readers need to visualize each grand building as they read. A feature list would feel dry. The architectural tour lets them "walk through" the release mentally.

### 2. Minecart Rails Framed as "Dependency Visualization"
**Decision:** Positioned minecart rails as a teaching tool for system architecture, not just a cool animation.

**Why:** The feature's real value is that it makes dependencies *visible in motion*. "Watch minecarts stop when a parent service fails" communicates cascade failures better than a redstone graph. Conference attendees will understand dependency chains instantly by watching carts halt.

### 3. DoorPosition Refactor Highlighted as Architecture Insight
**Decision:** Included a "behind the scenes" section explaining DoorPosition as an architectural pattern.

**Why:** Most release posts skip the "why this was built" in favor of "what to do with it." But developers reading Aspire-Minecraft blog posts are also trying to understand good distributed system design. The DoorPosition record is a clean example of derived positioning — it's the kind of pattern that matters across many systems. Highlighting it signals "this team thinks about architecture."

### 4. Code Example Shows Toggle Pattern
**Decision:** Provided the same AppHost code twice (once without Grand Village, implied; once with), showing `.WithGrandVillage()` and `.WithMinecartRails()` as opt-in toggles.

**Why:** Demonstrates backwards compatibility and makes migration obvious. A developer using v0.4.x can copy their exact AppHost and add two lines.

### 5. No Aggressive Analytics or "Try It Now" Conversion
**Decision:** Kept CTA low-key (standard links, simple install command).

**Why:** This is the *fifth* release in a rapid cadence. Readers who wanted to try it already did. The blog is now for *documentation* and *learning*, not discovery. Heavy conversion tactics feel out of place at this point.

---

## Content Decisions

### Emphasis on Interior Details
Each grand building gets 3–4 bullet points describing what you see *inside*. This is intentional — Aspire-Minecraft's differentiator is walkability. Small villages have one-block-thick walls. Grand villages reward exploration. The blog post should sell that exploration.

### Performance Transparency
Included a "Performance & Compatibility" section addressing potential concerns *before* readers have them:
- "Grand villages are more intensive" (honest)
- Chunks are force-loaded once, not per-tick (technical credibility)
- All existing services adapt (risk mitigation)
- Backwards compatible (adoption path)

This prevents "is this going to slow down my monitor?" questions in issues.

### Azure Citadel Tease
Mentioned the Azure integration as "The Pan" from village to cloud. This is stolen from Rocket's conference demo pitch. Including it in the release post keeps momentum high and signals that the roadmap is actively evolving.

---

## Lessons Learned for Future Release Posts

1. **Building tours work better than feature lists** when the feature is primarily about experience/interaction.
2. **Architecture insights** (like DoorPosition) deserve their own section in release posts — they're not marketing, they're education.
3. **Dependency visualization** is a strong narrative for minecart rails. In demos, people lean forward watching minecarts. Lead with that.
4. **Backwards compatibility upfront** prevents adoption friction. Always explicitly state what didn't change.
5. **Multi-building feature releases** benefit from a scannable format (table or bullet list) showing each building type + role. Readers want to know which structure covers their use case.

---

## Files Changed

- **Created:** `docs/blog/sprint-5-release.md` (2,800 words, release narrative)
- **Updated:** `.ai-team/agents/mantis/history.md` (appended 4 new learnings)
- **Created:** This decision document

---

## Sign-Off

Blog post is ready for publication. No external dependencies; no review gates. Can be merged as-is or tweaked if Jeffrey wants messaging adjustments.

**Next Blog Content Opportunity:** Azure Citadel integration (separate package) — good opportunity for an announcement post covering cloud resource visualization and the "Pan" demo moment.


# Decision: Grand Watchtower Size-Based Branching

**Date:** 2026-02-12
**Author:** Rocket
**Issue:** #78

## Context

The Grand Watchtower (15×15, 20 tall, 3 floors) needs to coexist with the standard watchtower (7×7, 10 tall). The routing in `BuildResourceStructureAsync` dispatches by structure type ("Watchtower"), not by size.

## Decision

`BuildWatchtowerAsync` checks `VillageLayout.StructureSize` at runtime: if 15 (Grand mode), it delegates to `BuildGrandWatchtowerAsync`; otherwise it builds the standard watchtower. This avoids changing the structure type string or the routing switch.

## Consequences

- **No new structure type string.** `GetStructureType` still returns `"Watchtower"` for all Project resources. Other services (beacons, particles, holograms, service switches) continue to work without modification.
- **Health indicator and Azure banner** adapted with size-based conditionals (`VillageLayout.StructureSize == 15`) for X-centering and roof Y.
- Same pattern can be applied to other grand buildings (Warehouse, Workshop, etc.) without touching the routing layer.


### RCON Burst Mode: No-Op on Re-Entry (#85)

**By:** Shuri
**What:** `EnterBurstMode()` now returns a no-op `IDisposable` instead of throwing `InvalidOperationException` when burst mode is already active.
**Why:** Callers using nested `using` blocks (e.g., multiple services building concurrently) don't need try/catch. The first caller owns the burst session; subsequent callers get a harmless no-op disposable. Thread safety maintained via `SemaphoreSlim.Wait(0)`.

### Fence/Forceload Grand Village Verification (#84)

**By:** Shuri
**What:** Verified all fence, path, and forceload code already uses dynamic `VillageLayout` properties — no hardcoded values remain.
**Why:** Prior sprint (#84 history entry) already converted gate position to `BaseX + StructureSize`, fence clearance to `VillageLayout.FenceClearance`, forceload to `GetFencePerimeter(10)`, and `MAX_WORLD_SIZE` to 512. No changes were needed.


# Decision: MinecartRailService Registration Stubbed

**Author:** Shuri
**Date:** 2026-02-12
**Issue:** #79

## Context

`WithMinecartRails()` sets the `ASPIRE_FEATURE_MINECART_RAILS` env var on the worker, and `Program.cs` checks for it. However, `MinecartRailService` does not exist yet — it's planned for Phase 3 of the Milestone 5 design (Rocket's scope).

## Decision

The `ASPIRE_FEATURE_MINECART_RAILS` check in `Program.cs` is wired up with a comment placeholder instead of a `builder.Services.AddSingleton<MinecartRailService>()` call. When Rocket implements the service, they just need to uncomment/add the registration line.

## Rationale

- The env var plumbing is in place end-to-end (extension method → worker env var → Program.cs check).
- Registering a non-existent type would cause a compile error.
- This follows the same pattern used in other milestones where the flag was wired before the service existed.

## Impact

- No behavioral change until `MinecartRailService` is implemented.
- `WithAllFeatures()` will set the flag even though the service isn't registered yet — this is harmless since the flag alone does nothing without the service.

