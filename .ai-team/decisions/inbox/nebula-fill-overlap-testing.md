# Decision: Fill-Overlap Detection as Standard Test Pattern

**Author:** Nebula  
**Date:** 2026-02-15  
**Status:** Proposed

## Context

Every building type constructs structures by issuing multiple `/fill` commands in sequence. When a later fill writes to coordinates that overlap an earlier fill, blocks are silently overwritten. This has caused bugs (e.g., Grand Watchtower gatehouse overwriting arrow slits, wool trim overwriting stair caps) that escaped code review because existing tests only checked command string format, not geometric correctness.

## Decision

All new building types and structural changes MUST have fill-overlap detection tests. The `FillOverlapDetectionTests` class provides reusable infrastructure:

1. **ParseFillCommand** — extracts bounding boxes from `/fill` command strings
2. **DetectSolidOnSolidOverlaps** — checks all fill pairs for overlapping volumes
3. **IsIntentionalLayering** — whitelists known architectural patterns

## Intentional Overlap Whitelist

These patterns are normal Minecraft building technique:
- Same block type (redundant fills are harmless)
- Fence gate replacing fence
- Smaller detail volume over larger structural volume
- Interior furnishing inside wall volumes
- Same material family (cracked → polished stone)
- Wool/banner decorative trim
- Gatehouse stonework over window elements

## Impact

- **Rocket**: New building types must pass fill-overlap detection tests
- **Nebula**: Add a test for each new structure type
- **All**: Any fill overlap not in the whitelist is a potential bug
