# Spawn: Nebula — Proactive Milestone Tests & Rewiring

**Agent:** Nebula (Tester)  
**Timestamp:** 2026-03-05  
**Status:** ✅ Complete  
**Impact:** Test coverage expanded, 7 tests updated for TNT minecarts  

## Work Completed

- Wrote 50 proactive test cases covering new milestone features
- Test contracts defined for squad agent parsing, bridge collision, error boats
- Updated 7 existing tests to verify TNT minecart behavior instead of creepers
- Established test pattern: local contract helpers → real implementation swap when code lands

## Key Decisions Made

1. **Proactive Test Contracts:** Local helper implementations serve as executable specs
2. **Rewiring Strategy:** When real code lands (Shuri's parser, Rocket's collision avoidance), replace helpers with actual implementations
3. **Test Structure:** Keep existing assertions intact during rewiring, add edge cases post-rewire

## Test Cases

- Squad agent name parsing (rewire when Shuri's parser available)
- Bridge collision avoidance (rewire when Rocket's collision logic available)
- Error boat/minecart lifecycle (direct integration tests, no rewiring needed)
- Village NPC placement and villager spawning

## Cross-Agent Notes

- Rocket's bridge collision work feeds into existing test skeleton
- Shuri's parsing implementation will integrate with squad name tests
- 640 tests passing (including 50 new proactive tests)
