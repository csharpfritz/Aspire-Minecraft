# Decision: Replace Creeper Minecarts with TNT Minecarts

**Author:** Rocket (Integration Dev)  
**Requested by:** Jeffrey T. Fritz  
**Date:** 2025-07-14

## Context

Error minecarts previously spawned a `minecraft:minecart` carrying a `minecraft:creeper` passenger to visually represent unhealthy resources. This required a workaround for peaceful mode: briefly setting `difficulty peaceful` to clear existing hostiles, then switching to `difficulty easy` with `doMobSpawning false` — because true peaceful mode would despawn the summoned creepers.

## Decision

Replaced `minecraft:minecart` + creeper passenger with `minecraft:tnt_minecart` (Fuse:-1, inert/never explodes). This is a single entity rather than a minecart-with-passenger compound.

Restored simple `difficulty peaceful` since TNT minecarts are entities, not hostile mobs — peaceful mode won't despawn them.

## Changes

- **ErrorBoatService.cs**: Summon command now uses `tnt_minecart` with `Fuse:-1`. Removed creeper cleanup from `CleanupBoatsAsync`. Cleanup now targets `type=minecraft:tnt_minecart` instead of `type=minecraft:minecart`.
- **Program.cs**: Peaceful mode is now a single `difficulty peaceful` command — no more easy-mode/doMobSpawning workaround.

## Trade-offs

- TNT minecarts are visually distinct (TNT texture) which arguably signals "error" more clearly than a silent creeper
- `Fuse:-1` ensures no accidental explosions — the TNT is purely decorative
- Simpler peaceful mode logic reduces RCON chatter by 2 commands at startup
