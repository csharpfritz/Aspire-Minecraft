# Spawn: Shuri — Squad Detection in Aspire Hosting

**Agent:** Shuri (Backend Dev)  
**Timestamp:** 2026-03-05  
**Status:** ✅ Complete  
**Impact:** .squad/team.md integration layer added  

## Work Completed

- Implemented `WithSquadVillagers()` extension method
- Integrated into `WithAllFeatures()` chain for seamless adoption
- Parses `.squad/team.md` agent names via column-index detection (robust to reordering)
- Injects `ASPIRE_SQUAD_AGENTS` environment variable to worker
- Excludes infrastructure agents (Scribe, Ralph) and non-active agents

## Key Decisions Made

1. **Column-Index Detection:** Regex-free parsing strategy for robustness
2. **Graceful Fallback:** No `.squad/team.md` = no error, clean no-op
3. **File Discovery:** Walks up from AppHostDirectory to find repo root
4. **Status Filtering:** Infrastructure and monitoring agents excluded by design

## Artifacts

- Aspire.Hosting.Minecraft/Extension/WithSquadVillagers.cs — new extension
- Program.cs — registration in DI + WithAllFeatures() chain

## Cross-Agent Notes

- Rocket consumes ASPIRE_SQUAD_AGENTS in SquadVillagerService
- Worker spawns named villagers based on parsed team roster
