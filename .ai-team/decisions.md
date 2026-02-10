# Decisions

> Shared decision log. All agents read this before starting work. Scribe merges new decisions from the inbox.

### 2026-02-10: NuGet packages are NOT ready for nuget.org publication

**By:** Shuri
**What:** Completed a full NuGet readiness audit of all three packages. Build and pack succeed, but several blocking and important gaps remain before publishing to nuget.org.
**Why:** Publishing packages with floating `Version="*"` dependencies, no SourceLink, no package validation, no CI/CD pipeline, and a 41 MB package (embedded otel jar) would create a poor consumer experience and make debugging/versioning impossible. These must be addressed first.

Key blockers:
1. Floating `Version="*"` references in all three csproj files must be pinned to specific versions.
2. No CI/CD pipeline exists — no automated build/test/publish workflow.
3. The 23 MB `opentelemetry-javaagent.jar` embedded in the hosting package inflates it to 41 MB — consider downloading at runtime instead.

Key improvements needed:
- Add `GenerateDocumentationFile`, `EnablePackageValidation`, SourceLink packages, `Deterministic`/`ContinuousIntegrationBuild` to `Directory.Build.props`.
- Add a `PackageIcon` (create a proper icon, not the sample screenshot).
- Consider per-package READMEs for better nuget.org presentation.
- Create a GitHub Actions CI/CD workflow for build + pack + publish.

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
