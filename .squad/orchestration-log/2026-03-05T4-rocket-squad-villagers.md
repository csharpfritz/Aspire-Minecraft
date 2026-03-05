# Spawn: Rocket — SquadVillagerService Implementation

**Agent:** Rocket (Integration Dev)  
**Timestamp:** 2026-03-05  
**Status:** ✅ Complete  
**Impact:** Squad NPC villagers feature complete  

## Work Completed

- Created `SquadVillagerService` singleton class
- Reads `ASPIRE_SQUAD_AGENTS` environment variable (injected by Shuri's hosting layer)
- Spawns named villagers distributed around village (not in fruit stand area)
- Applied invulnerability + natural wandering (NoAI:0 with Invulnerable:1b)
- Tagged villagers with `squad_villager` for easy cleanup
- Registered conditionally in Program.cs (no allocation when unused)

## Key Decisions Made

1. **Conditional Registration:** Only allocated if ASPIRE_SQUAD_AGENTS present
2. **Invulnerable Design:** Players can't accidentally kill squad NPCs
3. **Natural Wandering:** NoAI:0 allows exploration behavior
4. **Placement Strategy:** Avoid fruit stand (reserved for Maddy, Damian, Fowler, Brady, Scott)
5. **Cleanup Tag:** squad_villager tag for selective kill commands

## Artifacts

- src/Aspire.Hosting.Minecraft.Worker/Services/SquadVillagerService.cs — new service
- src/Aspire.Hosting.Minecraft.Worker/Program.cs — DI + timing registration

## Integration

- Runs after structures are built (same timing as VillagerService)
- Uses VillageLayout.GetFruitStandBounds() to avoid collision
- Graceful fallback when ASPIRE_SQUAD_AGENTS absent
