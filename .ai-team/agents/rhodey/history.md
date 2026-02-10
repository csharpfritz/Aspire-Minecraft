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
