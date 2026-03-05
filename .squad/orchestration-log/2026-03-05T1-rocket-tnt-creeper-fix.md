# Spawn: Rocket — TNT Minecart & Peaceful Mode Fix

**Agent:** Rocket (Integration Dev)  
**Timestamp:** 2026-03-05  
**Status:** ✅ Complete  
**Impact:** Core error visualization refactored  

## Work Completed

- Diagnosed peaceful mode despawning summoned creepers in error minecarts
- Replaced creeper-passenger minecarts with TNT minecarts (Fuse:-1, inert)
- Simplified peaceful mode: restored single `difficulty peaceful` command
- Added thread-safety locks to ErrorBoatService
- Created `GetFruitStandBounds()` helper in VillageLayout for collision avoidance

## Key Decisions Made

1. **TNT Minecarts Over Creepers:** Single entity type (not minecart-with-passenger), prevents despawning, signals error visually with TNT texture
2. **Peaceful Mode Restored:** No need for `difficulty easy` + `doMobSpawning false` workaround anymore

## Artifacts

- ErrorBoatService.cs — updated spawn command, new cleanup logic
- Program.cs — simplified peaceful mode sequence
- VillageLayout.cs — GetFruitStandBounds() for shared positioning

## Cross-Agent Notes

- Nebula's test cases updated to verify TNT minecart behavior
- VillageLayout bounds shared with SquadVillagerService for placement
