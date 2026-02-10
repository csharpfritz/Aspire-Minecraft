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
