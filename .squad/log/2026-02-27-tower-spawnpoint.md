# 2026-02-27 Tower Spawn Point

**Requested by:** Jeffrey T. Fritz

## Summary

Rocket added a `/setworldspawn` command to `GrandObservationTowerService.BuildTowerAsync`. Players now spawn on the tower rooftop facing south (yaw 0), one block above the roof.

## Details

- **Spawn Location:** Tower center X, SurfaceY + TowerHeight + 1, tower center Z
- **Spawn Orientation:** Facing south (yaw 0)
- **Build Status:** Passed
- **Tests:** 81 tower tests pass
- **Commit:** f6dd950
- **Branch:** village-polish
