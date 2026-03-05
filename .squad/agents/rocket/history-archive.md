# Rocket's History Archive

This archive contains condensed summaries of earlier milestones (2026-02-10 through 2026-02-14). Detailed entries are in the main history.md. This document preserves the key learnings for historical context.

## Milestones 1-4 Summary (2026-02-10 to 2026-02-14)

**Worker Architecture:** MinecraftWorldWorker polls every 10s with 2-min broadcast cycle. RconService enforces rate limiting (10 cmd/s standard, 40 cmd/s burst) with 250ms dedup throttle and OTEL tracing. 13 feature toggles across Sprints 1-3 (particles, weather, boss bar, sounds, action bar ticker, beacons, fireworks, guardians, fanfare, heartbeat, achievements, redstone dependency graph, service switches) follow consistent `With{Feature}()` pattern with conditional DI.

**Building System Evolution:** Started with 4 themed 7×7 structures (Watchtower/Warehouse/Workshop/Cottage). Sprint 3 added village fence (oak perimeter with paths and gates) and service switches. Sprint 4 added database cylinders and Azure-themed cottages with language-based color coding. All services (HologramManager, ScoreboardManager, StructureBuilder, etc.) register as singletons in main worker loop.

**Dashboard and Rendering:** Moved from complex per-resource scrolling displays to unified Redstone Dashboard Wall (west side of village at X=-5). Uses `/clone` shift-register for scrolling instead of per-lamp updates. Lamps use self-luminous blocks (glowstone=healthy, redstone_lamp unlit=unhealthy, sea_lantern=unknown) — not redstone power which is unreliable on Paper servers.

**RCON Insights:** 250ms dedup throttle on identical commands requires unique strings to force re-execution. `/fill ... hollow` is most efficient wall building. `/clone` is single command regardless of grid size (perfect for scrolling). Binary search terrain probing via `setblock` discovers surface Y-level. Village spacing evolved: 10→12→24 blocks. Burst mode via `EnterBurstMode()` returns IDisposable with thread-safe SemaphoreSlim.

**Language-based Color Coding:** Implemented for wool trim and banners (.NET=purple, Node=yellow, Python=blue, Go=cyan, Java=orange, Rust=brown, Unknown=white).

**Easter Egg:** Fritz's 3 named horses (Charmer/Dancer/Toby) with variants, spawned in southern clearance area, always-on (not feature-gated).

**Key Learnings:**
- Any service depending on RCON or discovered resources must NOT be independent BackgroundService — must be singleton called from MinecraftWorldWorker main loop.
- Clear critical openings (doors, windows) LAST in multi-stage structure builds.
- Path depth matters — flush paths replace surface layer, not sit on top.
- Idempotent building prevents decorative element overwrites and visual glitching.
- Wall banners require solid block support in facing direction.
- Standing banners need solid block beneath them.

## Dynamic Terrain Detection (2026-02-11)

TerrainProbeService runs ONCE at startup using binary search with `setblock` to find highest solid block. Cleans up probe blocks on completion. All services use `VillageLayout.SurfaceY` instead of hardcoded BaseY.

## Sprint 3 Bug Fixes

**Bug 1 — Sprint 3 services not running:** HeartbeatService, RedstoneDependencyService, ServiceSwitchService were independent BackgroundServices, so started before RCON connected. Fixed by converting to singletons in MinecraftWorldWorker main loop.

**Bug 2 — Only 2 of 4 beacon beams visible:** Hardcoded beacon positions overlapped structure footprints, blocking sky access. Fixed by deriving beacon positions from `VillageLayout.GetStructureOrigin()` and placing behind buildings.

**Key learning:** Services depending on RCON must NOT be BackgroundServices — must integrate into MinecraftWorldWorker's main loop which handles lifecycle correctly.

## Village Spacing & Placement Consolidation (2026-02-11 to 2026-02-15)

**Spacing doubled (12→24):** Gives 17-block walking gap between 7×7 structures. All structure placement derives from `GetStructureOrigin()` which uses Spacing constant.

**Placement methods standardized:** Health lamp, azure banner, sign now use `StructureSize / 2` for adaptive positioning across 7×7 and grand variants.

**Idempotent building:** HashSet tracks built resources; structures build once, then only health indicators update.

**Peaceful mode:** `WithPeacefulMode()` → `/difficulty peaceful` once at startup.

## MCA Inspector Milestone (2026-02-15)

NBT library evaluation completed — recommended fNbt 1.0.0 (netstandard2.0 compatibility, 200+ stars, Jul 2025 active). Designed custom AnvilRegionReader for parsing MCA file headers and bit-packed palette decoding. Phase 1 (library) and Phase 2 (test infrastructure) timeline ~1.5 weeks.

## Acceptance Testing Infrastructure (2026-02-15)

Structural validation requirements — all tests verify door accessibility, staircase connectivity, wall-mounted items. Created 86 structural geometry tests. Fill-overlap detection is standard test pattern for new building types.

---

**Archive Created:** 2026-02-27  
**Main history continues with:** Grand Watchtower (Milestone 5), Canal System Redesign (2026-02-19), Per-Building Canal System (2026-02-23), and subsequent milestones.
