# Bridge Clearance & Rail Continuity Fix  2026-03-03

## Context
Jeff reported three issues from in-game testing with screenshot evidence:
1. Rails stop at bridges instead of continuing under them
2. Bridge clearance too low for minecart + creeper passengers (~2.5 blocks needed)
3. N-S rails through town need power (redstone torches)

## Root Cause
- BridgeService deck at SurfaceY+2 only gives 1 block clearance above rails at SurfaceY+1
- Bridge air-clear command removes rails at SurfaceY+1
- Bridge support pillars use solid stone_bricks at rail height

## Fix (Rocket)
- Raised bridge deck from SurfaceY+2 to SurfaceY+4 (3-block clearance)
- Air clear starts at SurfaceY+2 to preserve rail blocks at SurfaceY+1
- Bridge supports changed from stone_bricks to oak_fence (minecarts pass through)
- 3-step ramps replace 2-step to reach new deck height

## Agents
- Rocket: BridgeService.cs edits, CanalService.cs verification
- Scribe: This log

## Branch
minecart-boats
