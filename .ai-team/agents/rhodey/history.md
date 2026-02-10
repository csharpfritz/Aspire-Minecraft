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

📌 Team update (2026-02-10): NuGet packages blocked from publication — floating deps, no CI/CD, bloated jar must be fixed first — decided by Shuri
📌 Team update (2026-02-10): 18 Minecraft interaction features proposed across 3 tiers (must-have/nice-to-have/stretch) — decided by Rocket
📌 Team update (2026-02-10): 3-sprint roadmap adopted (Ship It → Polish & Atmosphere → Showstopper) with per-agent assignments — decided by Rhodey

📌 Team update (2026-02-10): Sprint 1 — Shuri completed NuGet hardening (6 deps pinned, SourceLink, deterministic builds, per-package READMEs, OTel jar kept with TODO) — decided by Shuri
📌 Team update (2026-02-10): Sprint 1 — Wong completed CI/CD pipeline (build.yml + release.yml + PR template) — decided by Wong
📌 Team update (2026-02-10): Sprint 1 — Nebula completed test infrastructure (62 tests, 0 failures, InternalsVisibleTo pattern) — decided by Nebula
📌 Team update (2026-02-10): FluentAssertions v8 licensing concern flagged — needs resolution before v0.1.0 release — decided by Nebula
📌 Team update (2026-02-10): All sprint work tracked as 34 GitHub issues with team member and sprint labels, 6 Sprint 1 issues closed — decided by Jeffrey T. Fritz

📌 Team update (2026-02-10): FluentAssertions fully removed — replaced with xUnit Assert, zero licensing risk — decided by Jeffrey T. Fritz, Nebula
📌 Team update (2026-02-10): Single NuGet package consolidation — only Aspire.Hosting.Minecraft is packable now — decided by Jeffrey T. Fritz, Shuri
📌 Team update (2026-02-10): Redstone Dependency Graph + Service Switches proposed as Sprint 3 flagship feature — decided by Jeffrey T. Fritz

📌 Team update (2026-02-10): NuGet PackageId renamed from Aspire.Hosting.Minecraft to Fritz.Aspire.Hosting.Minecraft (Aspire.Hosting prefix reserved by Microsoft) — decided by Jeffrey T. Fritz, Shuri

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
