# Vision — Technical Writer

## Project Context

**Project:** Aspire-Minecraft — .NET Aspire integration for Minecraft servers  
**Stack:** C#, .NET 10, Docker, Aspire, OpenTelemetry, Minecraft Paper Server, RCON  
**Owner:** Jeffrey T. Fritz (csharpfritz@users.noreply.github.com)  
**Description:** A .NET Aspire hosting integration for Minecraft servers with RCON, BlueMap, OpenTelemetry, and in-world resource visualization.

The project visualizes Aspire resources as a Minecraft village where each resource appears as a themed structure (watchtowers for Projects, warehouses for Containers, workshops for Executables, cottages for Unknown resources). Features include health indicators (glowstone/redstone lamps), service control switches (levers), redstone dependency graphs, achievements, heartbeat monitoring, boss bars for resource status, world border pulse, and comprehensive testing (320+ tests).

Currently at v0.3.0, Sprint 3 complete. Sprint 3.1 quality improvements just finished with structure validation, VillageLayout unit tests, village integration tests, architecture diagram, and constraints documentation.

## Learnings

### Documentation Structure

**user-docs/ folder:** Separate user-facing documentation from technical docs (docs/). User docs focus on how to use features, technical docs focus on architecture/constraints.

**Feature documentation pattern:** Each feature document follows consistent structure: What It Does → How to Enable → Configuration → What You'll See → Use Cases → Technical Details → Code Example. This makes docs scannable and practical.

### Key File Locations

- **Extension methods:** `src/Aspire.Hosting.Minecraft/MinecraftServerBuilderExtensions.cs` — All public API surface
- **API surface doc:** `docs/api-surface.md` — Authoritative API listing, frozen per version
- **Constraints doc:** `docs/minecraft-constraints.md` — Y-levels, RCON limits, coordinate system
- **Sample AppHost:** `samples/MinecraftAspireDemo/MinecraftAspireDemo.AppHost/Program.cs` — Working example with all features

### Feature Architecture

**Opt-in model:** All features controlled by `ASPIRE_FEATURE_*` environment variables on worker. Zero overhead for disabled features. Extension methods set these automatically.

**Worker dependency:** All in-world features require `WithAspireWorldDisplay()` called first. This creates the worker service that connects via RCON.

**Health monitoring:** Two-tier system — HTTP health checks for endpoints, TCP checks for databases/cache. Health state drives all visual/audio effects.

### Coordinate System

**BaseY = -60:** Grass surface in superflat world. Structure floors at BaseY, walls start at BaseY+1. Critical for understanding placement.

**Village layout:** 2-column grid starting at X=10, Z=0. Spacing=10 blocks center-to-center. Structures are 7×7 footprint.

**RCON rate limit:** 10 commands/second maximum. Initial village build for 4 resources takes ~7-8 seconds. This is the primary scaling constraint.

### User Perspective Patterns

**Clear vs. Technical:** Users need clear "what you'll see" descriptions, not implementation details. Examples: "Glowing yellow lamp" not "glowstone block at Y+5", "Fast high-pitched heartbeat" not "1000ms interval, pitch 24".

**Troubleshooting first:** Users hit issues before they master features. Troubleshooting guide needs to be comprehensive with specific solutions, not generic advice.

**Code examples matter:** Every feature doc should show working code, not just method signatures. Users copy-paste examples to get started quickly.

### Sprint 3 Features

All documented Sprint 3 features: village visualization with themed structures, health indicators (lamps/beacons/particles/mobs), service switches (display-only levers), redstone dependency graph (L-shaped wires), achievements (milestones), heartbeat (tempo/pitch reflect health), boss bar (aggregate %), world border pulse (shrinks on critical), RCON debug logging (troubleshooting tool).
 Team update (2026-02-11): All sprints must include README and user documentation updates to be considered complete  decided by Jeffrey T. Fritz
 Team update (2026-02-11): All plans must be tracked as GitHub issues and milestones; each sprint is a milestone  decided by Jeffrey T. Fritz
📌 Team update (2026-02-15): Python and Node.js sample APIs added to MinecraftAspireDemo; separate GrandVillageDemo created on milestone-5 showcasing all grand building variants (15×15 structures for Project, Container, Database, Azure types) — decided by Shuri
