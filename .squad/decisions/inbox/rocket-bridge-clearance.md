# Bridge Clearance Standard: SurfaceY+4 Deck Height

**Date:** 2025-07-18
**By:** Rocket
**Status:** Decided

## Decision
Bridge deck height is SurfaceY+4 (3 blocks above rail height at SurfaceY+1). Air clearing for bridges starts at SurfaceY+2, never SurfaceY+1, to preserve rails underneath. Bridge supports use `oak_fence` instead of solid blocks so minecarts can pass through at rail height.

## Why
- A minecart (~0.7 blocks) carrying a creeper (~1.7 blocks) needs ~2.5 blocks clearance. SurfaceY+2 deck only gave 1 block above rails — guaranteed collision.
- The old air clear at SurfaceY+1 destroyed E-W rails placed by CanalService before BridgeService ran, breaking the entire rail network at bridge intersections.
- `stone_bricks` supports at SurfaceY+1 were solid blocks on the rail plane — minecarts can't pass through solid blocks but CAN pass through `oak_fence` posts.

## Impact
- BridgeService.cs: `deckY = sy + 4`, 3-step ramps, fence supports
- Any future bridge types must follow the same clearance standard
- Build order (canals → bridges → rails) is now safe because bridges don't clear rail height
