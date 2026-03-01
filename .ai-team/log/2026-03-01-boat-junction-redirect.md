# Session: Boat Junction Redirect Fix

**Requested by:** Jeffrey T. Fritz

## Summary
Jeff tested ice canals — boats slide correctly on blue_ice but get stuck at the trunk canal junction (keeps going west instead of turning south). Rocket spawned to fix by: exposing TrunkCanalX from CanalService, repurposing MoveBoatsAsync to redirect westbound boats to southbound using data merge entity with Motion and tag system.

## Work Done
- Identified boat routing issue at trunk canal junction
- Boats successfully slide on blue_ice but fail to turn at junction
- Solution approach: expose TrunkCanalX from CanalService for use in MoveBoatsAsync
- Redirect logic using data merge entity with Motion component and tag-based filtering

## Status
Rocket assigned for implementation
