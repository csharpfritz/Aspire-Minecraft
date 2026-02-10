# Session: 2026-02-10 — Team Init + Sprint Plan

**Requested by:** Jeffrey T. Fritz

## Team Initialization

Team initialized with MCU casting:

| Callsign | Role |
|----------|------|
| Rhodey | Lead |
| Shuri | Backend Dev |
| Rocket | Integration Dev |
| Nebula | Tester |
| Mantis | Blogger |
| Wong | GitHub Ops |

Mantis and Wong were added mid-session to cover blogging and CI/CD operations.

## Key Work Completed

### Shuri — NuGet Readiness Audit
- `dotnet build` and `dotnet pack` succeed (0 errors).
- Three packages produced: Hosting (~41 MB), Rcon (~20 KB), Worker (~28 KB).
- **Blockers found:** floating `Version="*"` deps, no CI/CD pipeline, 23 MB embedded otel jar inflates hosting package.
- **Missing:** SourceLink, `EnablePackageValidation`, `GenerateDocumentationFile`, `PackageIcon`, per-package READMEs.

### Rocket — Feature Brainstorm
- Proposed 18 Minecraft interaction features across 3 tiers:
  - **Must-Have (5):** Boss bars, title alerts, sounds, weather, particles — all Size S.
  - **Nice-to-Have (6):** Action bar ticker, fireworks, guardian mobs, world border, beacons, deployment fanfare.
  - **Stretch (7):** Themed village, redstone heartbeat, nether portals, log wall, /trigger commands, achievements, rail network.

### Rhodey — 3-Sprint Plan
- **Sprint 1 "Ship It":** NuGet hardening + 5 must-have features + test infra + CI workflow. ~22 items.
- **Sprint 2 "Polish & Atmosphere":** Feature toggles, medium features, CD pipeline, v0.1.0 release + blog. ~20 items.
- **Sprint 3 "Showstopper":** Themed architecture, achievements, rail network, conference demo polish. ~18 items.
- Cut line: Rail Network drops first; Resource Village + Achievements are must-haves for conference.

## Decisions Made

1. NuGet readiness blockers must be resolved before any publication (Shuri).
2. 18 features prioritized into 3 tiers by effort and impact (Rocket).
3. 3-sprint roadmap adopted with per-agent task assignments and dependencies (Rhodey).
