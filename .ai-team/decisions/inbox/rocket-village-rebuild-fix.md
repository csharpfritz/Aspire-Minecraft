# Decision: Village Structure Idempotent Building Pattern

**Date:** 2026-02-11  
**Decider:** Rocket (Integration Dev)  
**Status:** Implemented

## Context

Village structures were being rebuilt every 10-second display update cycle, causing visible glitching in-game. The `StructureBuilder.UpdateStructuresAsync()` method was calling `BuildResourceStructureAsync()` for every resource on every cycle without checking if the structure already existed.

Additionally, cobblestone paths were placed at `BaseY - 1` (Y=-61) which is underground in superflat worlds, making them invisible to players.

## Decision

Implemented idempotent building pattern for village structures:

1. **Structure Tracking**: Added `HashSet<string> _builtStructures` to track which resources have had their structures built
2. **Build Once Pattern**: Modified update loop to:
   - Check if structure already exists via `_builtStructures.Contains(name)`
   - If not built: call `BuildResourceStructureAsync()` and add to set
   - If already built: only update health indicator via `PlaceHealthIndicatorAsync()`
3. **Path Y-Level Fix**: Reverted all cobblestone path placements from `BaseY - 1` to `BaseY` so they sit on grass surface

## Rationale

- **Performance**: Eliminates redundant RCON commands for structure building every 10 seconds
- **Visual Stability**: Prevents the "glitching" effect where structures briefly change shape during rebuilds
- **Element Preservation**: Prevents structure rebuilds from overwriting decorative elements (switches, signs, lamps)
- **Path Visibility**: Paths at `BaseY` replace grass blocks and are visible/walkable; paths at `BaseY - 1` are buried in dirt

This follows the same pattern already established for fence (`_fenceBuilt` flag) and paths (`_pathsBuilt` flag).

## Consequences

### Positive
- Buildings remain stable and don't glitch every 10 seconds
- Paths are visible and walkable on the ground surface
- Switches, signs, and other decorative elements persist correctly
- Reduced RCON command volume (better performance)
- All 303 existing tests pass

### Negative
- Structures cannot be "refreshed" if manually destroyed in-game without restarting the worker
- Resource name is used as tracking key (if a resource is renamed and added again, it would build a new structure)

### Neutral
- Pattern is consistent with existing fence/path building flags
- Health indicators still update dynamically every cycle as intended

## Implementation Details

**File**: `src/Aspire.Hosting.Minecraft.Worker/Services/StructureBuilder.cs`

- Added field: `private readonly HashSet<string> _builtStructures = new(StringComparer.OrdinalIgnoreCase);`
- Modified `UpdateStructuresAsync()` to check `_builtStructures` before building
- Reverted path Y-coordinates from `VillageLayout.BaseY - 1` to `VillageLayout.BaseY` in three locations (main boulevard, cross paths, entry path)

## Alternatives Considered

1. **Time-based rebuild**: Only rebuild structures every N minutes instead of every cycle
   - Rejected: Still causes glitching, just less frequently
2. **Change detection**: Compare current vs desired structure state and only update differences
   - Rejected: Too complex; requires querying and parsing Minecraft world state via RCON
3. **Manual refresh command**: Add an RCON command to force structure rebuild
   - Deferred: Could be added later if needed, but not required for normal operation

## Related Decisions

- Fence perimeter uses `_fenceBuilt` flag (similar pattern)
- Paths use `_pathsBuilt` flag (similar pattern)
- Service switches already placed once then only update state on transitions
