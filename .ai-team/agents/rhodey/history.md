# Project Context

- **Owner:** Jeffrey T. Fritz (csharpfritz@users.noreply.github.com)
- **Project:** Aspire.Hosting.Minecraft — .NET Aspire integration for Minecraft servers
- **Stack:** C#, .NET 10, Docker, Aspire, OpenTelemetry, Minecraft Paper Server, RCON
- **Created:** 2026-02-10

## Key Facts

- Three NuGet packages: Aspire.Hosting.Minecraft (hosting lib), Aspire.Hosting.Minecraft.Rcon (RCON client), Aspire.Hosting.Minecraft.Worker (in-world display)
- All packages at version 0.1.0
- Directory.Build.props sets shared NuGet metadata (author, license, repo URL, README)
- Uses itzg/minecraft-server Docker image with Paper server
- RCON for server communication, BlueMap for 3D web maps, DecentHolograms for in-world display
- OpenTelemetry Java agent injected for JVM metrics

## Learnings

<!-- Append new learnings below. Each entry is something lasting about the project. -->

### 2026-02-10: Sprint Planning Decisions

- **NuGet before features:** Pinning floating `Version="*"` deps and adding SourceLink/deterministic builds is Sprint 1 priority — nothing ships to nuget.org without it.
- **OTel jar extraction is the biggest S1 risk:** The 23 MB opentelemetry-javaagent.jar must move to runtime download. This touches container startup and could have edge cases with offline scenarios.
- **All 5 "Must-Have" features are Size S and go into Sprint 1:** Boss bars, title alerts, sounds, weather, particles — they're all RCON one-liners and transform the demo immediately.
- **Feature toggle builder pattern in Sprint 2:** Each Rocket feature should be opt-in via `AddMinecraftServer(opts => ...)`. This keeps the API clean and avoids surprising consumers.
- **Cut line for Sprint 3:** If time is tight, Rail Network drops first, then anything beyond Resource Village + Achievements. Those two are the conference must-haves.
- **Nether Portals, Log Wall, /trigger Commands punted to backlog:** Good ideas but not needed for the initial 3-sprint arc. v0.3.0 candidates.
- **CI in Sprint 1, CD in Sprint 2:** Build workflow ships immediately; NuGet publish automation comes after tests are solid.
- **Blog post gates on release tag:** Mantis doesn't publish until Rhodey cuts the release, avoiding announcing something that isn't live.

📌 Team updates (2026-02-10, consolidated): NuGet hardening completed (Shuri). 3-sprint roadmap adopted (Rhodey). CI/CD pipeline created (Wong). Test infrastructure with 62 tests (Nebula). FluentAssertions removed for licensing (Jeff/Nebula). Single NuGet package as Fritz.Aspire.Hosting.Minecraft (Jeff/Shuri). All work tracked as 34 GitHub issues (Jeff). Redstone Graph + Switches proposed for Sprint 3 (Jeff).

📌 Team updates (2026-02-10, Sprint 2): NuGet version defaults to 0.1.0-dev (Shuri). Release workflow extracts version from git tag (Wong). Beacon colors match Aspire palette (Rocket). ServerProperty API added (Shuri). Hologram line-add bug fixed (Rocket). Azure RG epic designed (Rhodey). Azure SDK separate package recommended (Shuri). Sprint branches with PRs directive (Jeff). API surface frozen at 31 methods for v0.2.0 (Rhodey).

### 2026-02-10: Sprint 2 API Review & Release Prep

- **API surface is clean and consistent.** All 10 feature extension methods (5 Sprint 1 + 5 Sprint 2) follow identical patterns: same signature shape, guard clause, env var naming (`ASPIRE_FEATURE_*`), and XML docs.
- **No breaking changes needed.** Sprint 2 methods (`WithActionBarTicker`, `WithBeaconTowers`, `WithFireworks`, `WithGuardianMobs`, `WithDeploymentFanfare`) are exact copies of the Sprint 1 pattern.
- **5 non-breaking concerns documented** for Sprint 3: add `WithAllFeatures()` convenience, tighten env var checks to `== "true"`, extract duplicated `ParseConnectionString`, add `IRconCommandSender` interface, consider auto-discovery of monitored resources.
- **Demo AppHost updated** with all Sprint 2 feature calls chained alongside Sprint 1 calls.
- **README updated** with 11 new feature bullet points (Sprint 1 + Sprint 2) and full code sample showing all features enabled.
- **Build:** 0 warnings, 0 errors. **Tests:** 248 pass (186 worker + 45 RCON + 17 hosting).

📌 Team update (2026-02-10): Sprint 2 API review complete — 10 feature methods consistent, no breaking changes, 5 additive recommendations for Sprint 3, demo + README updated — decided by Rhodey

📌 Team update (2026-02-10): NuGet package version now defaults to 0.1.0-dev; CI overrides via -p:Version from git tag — decided by Shuri
📌 Team update (2026-02-10): Release workflow extracts version from git tag and passes to dotnet build/pack — decided by Wong
📌 Team update (2026-02-10): Beacon tower colors now match Aspire dashboard resource type palette (blue/purple/cyan/red/yellow) — decided by Rocket
📌 Team update (2026-02-10): WithServerProperty API and ServerProperty enum added for server.properties configuration — decided by Shuri
📌 Team update (2026-02-10): Hologram line-add bug fixed (RCON throttle was dropping duplicate commands) — decided by Rocket

### 2026-02-10: Azure Resource Group Integration — Architecture Trade-offs

- **Separate NuGet package is the right call for Azure.** Putting `Azure.ResourceManager.*` deps into the main package penalizes every consumer, even those who never touch Azure. `Fritz.Aspire.Hosting.Minecraft.Azure` keeps the dependency graph clean and follows .NET ecosystem conventions.
- **The critical architectural insight is that Azure is a new discovery source, not a new rendering pipeline.** `AzureResourceMonitor` should emit the same `ResourceInfo`/`ResourceStatusChange` records that the existing worker services already consume. All 10+ in-world services (beacons, particles, boss bar, guardian mobs, etc.) work unchanged.
- **Polling beats Event Grid for v1.** Event Grid requires Azure infrastructure setup (topics, subscriptions, ingress) that destroys the "clone and run" developer experience. Polling at 30s intervals stays well within ARM API rate limits (~6,000 calls/hr vs ~12,000/hr cap).
- **Scale is the biggest unknown.** A production Azure RG can have 200+ resources. The current 2-column village layout at 10-block spacing would stretch 1,000+ blocks — well beyond beacon render distance (256 blocks) and the configured `MAX_WORLD_SIZE` (256). A `MaxResources` cap and default resource type exclusion list are mandatory for v1.
- **RCON throughput is a hidden constraint for Azure.** 50 resources × ~15 fill commands × 250ms throttle = ~3 minutes for initial world build. The Aspire path typically has 3–8 resources, so this never surfaced. May need batch mode or throttle bypass for initial construction.
- **`DefaultAzureCredential` is the right default but first-run DX will be rough.** The credential chain's error messages are notoriously confusing for devs who haven't done `az login`. A connectivity pre-check in the worker before building structures would save support headaches.

### 2026-02-10: API Surface Freeze & Demo Review for v0.2.0

- **API surface is frozen at 31 public methods** across `MinecraftServerBuilderExtensions`, plus 4 public types (`MinecraftServerResource`, `ServerProperty`, `MinecraftGameMode`, `MinecraftDifficulty`) and 6 RCON types. Full listing committed to `docs/api-surface.md`.
- **All 13 feature methods (5 Sprint 1 + 5 Sprint 2 + 3 Sprint 3) follow identical patterns:** same signature shape, guard clause, env var naming, fluent return, XML docs. `WithRedstoneDependencyGraph` (Sprint 3, in progress by Rocket) also follows the pattern.
- **No internal type leakage detected.** `WorkerBuilder`, `MonitoredResourceNames`, annotations, and all Worker service types are properly `internal`.
- **XML documentation is complete** on all public types and methods — no gaps found.
- **Demo AppHost cleaned up:** features now grouped by sprint with clear comment headers. `WithWorldBorderPulse` moved from Sprint 2 group to Sprint 3 where it belongs. Server config, integrations, and monitored resources each have their own section.
- **README updated** with 3 missing Sprint 3 features (World Border Pulse, Heartbeat, Achievements) in both the feature list and the code sample.
- **Build passes:** 0 errors, 1 pre-existing warning (CS8604 nullable in MinecraftServerResource).

📌 Team update (2026-02-10, consolidated): API surface frozen for v0.2.0 (Rhodey). Azure SDK separate package recommended (Shuri). Sprint branches with PRs directive (Jeff).

### 2026-02-10: Core Architecture Documentation

- **docs/architecture-diagram.md created** — comprehensive visual documentation of the Minecraft coordinate system, village layout, and structure placement. Includes Y-level breakdown (Y=-64 to Y=-59), 2×N grid layout with exact coordinate calculations, structure footprint visualization, fence perimeter math, and 4-resource village example. Uses ASCII art for accessibility. This is the reference for contributors learning the coordinate math.
- **docs/minecraft-constraints.md created** — authoritative source of truth for all Minecraft building rules. Documents Y-level conventions (BaseY=-60 as grass surface), structure size limits (7×7 footprint, heights: watchtower 10 blocks, warehouse/cottage 5 blocks, workshop 7 blocks), RCON rate limits (10 cmd/sec, 250ms throttle, high-priority bypass), Z-coordinate conventions (hollow vs solid structure front walls), path placement (BaseY-1 with grass cleared), fence placement (BaseY with 4-block clearance), and maximum resource limits (~45 resources before world border issues at 256 blocks).
- **Key architectural insights captured**: RCON throughput is the hidden bottleneck for large deployments (50 resources = ~75 seconds initial build at 10 cmd/sec). World border at 256 blocks limits village to ~45 resources safely. Azure RG integration will need MaxResources cap and resource type filtering.
- **Documentation placement rationale**: Both files in docs/ root for high visibility. architecture-diagram.md is educational (helps contributors understand), minecraft-constraints.md is prescriptive (enforces consistency). Together they form complete reference for world-building features.
 Team update (2026-02-11): All sprints must include README and user documentation updates to be considered complete  decided by Jeffrey T. Fritz
 Team update (2026-02-11): All plans must be tracked as GitHub issues and milestones; each sprint is a milestone  decided by Jeffrey T. Fritz
� Team update (2026-02-11): CI pipelines now skip docs-only changes (docs/**, user-docs/**, *.md, .ai-team/**)  decided by Wong
 Team update (2026-02-11): 14 user-facing docs now live in user-docs/ with consistent structure (What  How  What You'll See  Use Cases  Code Example)  decided by Vision

### 2026-02-11: Sprint 4 Planning Analysis

- **Project maturity assessment:** v0.3.1 released. 31 extension methods, 361 tests, 24 worker services, 14 user-docs, full CI/CD (build + release + CodeQL). API surface frozen since v0.2.0. README is comprehensive. The project is past "proof of concept" and entering "polish for adoption" territory.
- **Tech debt identified:** (1) `WithAllFeatures()` convenience method still missing (recommended since Sprint 2 API review). (2) Feature env var checks use `!string.IsNullOrEmpty` instead of `== "true"` (Sprint 2 recommendation #4). (3) No `IRconCommandSender` interface for testability (Sprint 2 recommendation #3). (4) User-docs missing coverage for 5 features: Weather Effects, Particle Effects, Sound Effects, Fireworks, Deployment Fanfare. (5) SourceLink floating version `8.*` in Directory.Build.props not pinned.
- **Azure epic status:** Architecture fully designed, SDK research done, visual design complete. Ready for Phase 1 implementation. Separate NuGet package (`Fritz.Aspire.Hosting.Minecraft.Azure`) is the agreed approach. NOT ready for Sprint 4 — it's a multi-sprint epic marked for v1.0/Sprint 5+.
- **Feature gaps for new users:** No `WithAllFeatures()` convenience (13 `With*()` calls is intimidating). No interactive "getting started" experience. No NuGet package icon. No GitHub Pages documentation site.
- **Only 1 open GitHub issue:** #48 (Pre-baked Docker image — v1.0/Sprint 5+).
- **Key observation:** The project has shipped 3 sprints of features. Sprint 4 should focus on DX polish, missing docs, convenience APIs, and making the NuGet package irresistible to first-time users — not new Minecraft features.

### 2026-02-12: Sprint 4 Brainstorming — Aspire Observability Visualization Ideas

- **Jeff's request:** "What would be fun to browse and wander around in Minecraft? Some way to visualize traces?" — Jeff is looking to go beyond resource health into OpenTelemetry observability data (traces, metrics, logs).
- **10 ideas brainstormed**, ranging from Easy to Hard feasibility. Full write-up in `.ai-team/decisions/inbox/rhodey-sprint4-visualization-ideas.md`.
- **Top 3 for Sprint 4 (no new data source needed):** Dragon Health Egg (SLO visualization with Ender Dragon theming), Redstone Clock Dashboard (time-series health display), Sculk Error Network (cascading failure visualization using Deep Dark blocks).
- **Jeff's trace interest → Trace River is the headline feature for Sprint 5.** Water channels between buildings with boats representing requests, lava for errors. Requires OTLP trace ingestion — a new architectural capability the worker doesn't have today.
- **Critical architectural decision identified:** All OTLP-dependent features (7 of 10 ideas) require the worker to consume trace/metric/log data. Today it only polls health endpoints. Must design the OTLP ingestion architecture before committing to any individual feature. Three options documented: secondary OTLP receiver, Aspire dashboard API polling, or shared collector.
- **RCON budget is the hidden constraint** for continuous visualization features. Metrics Tower and Minecart Rails have high RCON costs. Dragon Egg and Sculk Network are RCON-efficient. This should influence prioritization.

### 2026-02-12: Sprint 4 Technical Design Decisions

- **Full technical design created** at `docs/designs/sprint-4-design.md` covering Redstone Dashboard Wall, Enhanced Building Architecture, and Sprint 4 scope with 14 issues.
- **Redstone Dashboard Wall** placed at X=-5, west of village, facing east. Uses `/clone` shift-register technique for RCON-efficient scrolling (N+1 commands per cycle instead of N×columns). Health history stored in a `HealthHistoryTracker` ring buffer. Concrete bar charts below show uptime percentages.
- **Jeff's enhanced building vision implemented:** Database resources (postgres, redis, sqlserver, etc.) get cylindrical buildings using 3-block radius in the 7×7 grid with polished deepslate. Azure resources get light_blue_banner flags on rooftops. Azure detection via resource type string matching (no SDK dependency). New `AzureThemed` structure type is a Cottage variant with light_blue_concrete walls.
- **Building type mapping expanded:** Project→Watchtower, Container→Warehouse, Executable→Workshop, Database→Cylinder, Azure→AzureThemed, Unknown→Cottage. Azure banner is additive — a database cylinder from Azure gets both the cylinder shape AND the azure banner.
- **Sprint 4 scoped to 14 issues:** 4 dashboard, 3 enhanced buildings, 1 Dragon Egg, 3 DX polish (WithAllFeatures, env var tightening, welcome teleport), 3 documentation (README, user-docs, tests). Cut line: welcome teleport drops first, then Dragon Egg.
- **Key design principle:** Azure detection is a visual signal (banner), not an architectural integration. String matching on resource types keeps the main package free of Azure SDK dependencies, consistent with the separate-package decision from Sprint 2.
- **Cylinder RCON cost is ~88 commands** (4× a watchtower) due to circular geometry. Acceptable as one-time build; databases are typically <30% of resources.
- **Decisions logged** in `.ai-team/decisions/inbox/rhodey-sprint4-design.md` (7 decisions).