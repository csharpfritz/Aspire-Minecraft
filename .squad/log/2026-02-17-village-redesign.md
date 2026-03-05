# 2026-02-17: Village Redesign Architecture — Implementation Plan

**Requested by:** Jeffrey T. Fritz

## What Happened

1. **Rhodey** created comprehensive village redesign architecture proposal covering:
   - Phased implementation plan (6 phases, 3-week timeline)
   - VillageLayout spacing expansion from 24→36 blocks for Grand layout
   - Custom Docker image strategy (ghcr.io/csharpfritz/aspire-minecraft-server)
   - Canal system architecture with blue ice floors and water-based routing
   - Error boat spawning and lifecycle management (health-based triggers)
   - Track/canal bridge interactions

2. **Shuri** assigned to Phase 1: Village Layout Expansion (3–4 days)
   - Increase VillageLayout.Spacing to 36
   - Add canal and lake constants
   - Update MAX_WORLD_SIZE from 512 to 768

3. **Wong** assigned to Phase 2: Custom Docker Image (2–3 days)
   - Dockerfile extending itzg/minecraft-server:latest
   - Pre-bake BlueMap, DecentHolograms, OTEL agent
   - Publish to GHCR

4. **Design defaults approved:**
   - Canal floor: **blue ice** (fast boats, magical aesthetic)
   - Walls: **stone brick** (medieval village consistency)
   - Boats: **oak boats** (classic)
   - Registry: **GHCR** (GitHub Container Registry)
   - Error trigger: **health-status-based** (v1), future log-based via OTLP (v2)
   - Lake feature: simple catch basin with dock

## Status

**Decisions merged.** Architecture ready for phase 1 implementation start. Phased plan removes blocking dependencies — Phase 1 + Phase 2 execute in parallel, unblocking Phases 3–5.
