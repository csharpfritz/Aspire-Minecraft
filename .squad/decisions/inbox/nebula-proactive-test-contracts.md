### Proactive Milestone Feature Tests — Test Contract Patterns

**By:** Nebula
**Date:** 2026-02-27

**What:**
- Squad agent name parsing tests use a local reference implementation of the parsing logic. When Shuri's actual parser lands, these tests should be rewired to call the real implementation instead of the test-local `ParseSquadAgentNames` method.
- Bridge collision tests define `BridgeOverlapsFruitStand` and `ShiftBridgeAwayFromStand` locally. When Rocket adds collision avoidance to `BridgeService`, these helpers should be replaced with the real methods.
- ErrorCreeperSpawnTests exercise `ErrorBoatService` directly — these are integration tests that work today and require no rewiring.

**Why:**
Proactive test patterns (writing tests before implementation) require a local contract definition. This is intentional — the local helpers serve as executable specifications. When the real code lands, the team should:
1. Replace local parsing/collision helpers with the real implementation
2. Verify all existing assertions still hold
3. Add any additional tests for edge cases discovered during implementation
