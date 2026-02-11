# Decision: Peaceful Mode Implementation

**Date:** 2026-02-10  
**Decider:** Rocket (Integration Dev)  
**Status:** Implemented

## Context

User requested a feature to eliminate hostile mobs (zombies, skeletons, creepers) from the Minecraft world to create a safer environment for monitoring infrastructure.

## Decision

Implemented `WithPeacefulMode()` extension method using `/difficulty peaceful` Minecraft command instead of gamerules.

## Rationale

1. **`/difficulty peaceful` vs gamerule approach:**
   - `difficulty peaceful` is the standard Minecraft way to eliminate hostiles
   - Immediately removes all existing hostile mobs
   - Prevents hostile mob spawning
   - Preserves passive mob spawning (cows, pigs, sheep)
   - More idiomatic than using `doMobSpawning` gamerule (which stops ALL mobs)

2. **One-time execution pattern:**
   - Command executes once at server startup after RCON connection
   - No service class needed — single RCON command is sufficient
   - Follows initialization pattern similar to `WorldBorderService.InitializeAsync()`

3. **Env var: `ASPIRE_FEATURE_PEACEFUL`**
   - Consistent with other opt-in features
   - Checked directly in `MinecraftWorldWorker.ExecuteAsync()` after resource discovery
   - No conditional DI registration needed (no service class)

## Implementation

- Extension method: `MinecraftServerBuilderExtensions.WithPeacefulMode()`
- Worker logic: Direct check in `MinecraftWorldWorker.ExecuteAsync()`
- Demo updated: Added `.WithPeacefulMode()` to Sprint 3 features
- API surface doc updated

## Alternatives Considered

- **Gamerule `doMobSpawning false`:** Stops ALL mob spawning including passives
- **Separate service class:** Overkill for single one-time command
- **Server property `DIFFICULTY=peaceful`:** Container-level, but less flexible for opt-in pattern

## Impact

- Opt-in feature, no effect on existing deployments
- All existing tests pass
- Consistent with team's feature opt-in architecture
