# Decision: Squad Villager NPC Spawning in Worker

**Author:** Rocket
**Date:** 2026-03-05
**Status:** Implemented

## Context

Shuri implemented `WithSquadVillagers()` on the hosting side, which parses `.squad/team.md` and injects agent names as the `ASPIRE_SQUAD_AGENTS` environment variable. The worker needed a service to consume this and spawn corresponding NPC villagers in the Minecraft village.

## Decision

- Created `SquadVillagerService` as a singleton following the existing `VillagerService` pattern.
- Registered conditionally in `Program.cs` — only when `ASPIRE_SQUAD_AGENTS` is present (no allocation if unused).
- Villagers are spread around building entrances and pathways, avoiding the fruit stand area where the 5 existing NPCs (Maddy, Damian, Fowler, Brady, Scott) live.
- Each villager is tagged `squad_villager` for easy cleanup via `kill @e[tag=squad_villager]`.
- Villagers are `Invulnerable:1b` so players can't kill them, and `NoAI:0` so they wander naturally.
- Runs after structures are built (same timing as `VillagerService`).

## Impact

- New file: `src/Aspire.Hosting.Minecraft.Worker/Services/SquadVillagerService.cs`
- Modified: `src/Aspire.Hosting.Minecraft.Worker/Program.cs` (DI registration + worker injection)
- No breaking changes — graceful no-op when env var is absent.
