### World Border Pulse on Critical Failure (Issue #28)

**By:** Shuri
**What:** Added `WorldBorderService` and `WithWorldBorderPulse()` extension method. World border shrinks from 200→100 blocks over 10s when >50% of monitored resources are unhealthy, restores to 200 over 5s on recovery. Red warning tint at 5 blocks from border edge during critical state.
**Why:** Adds dramatic visual/physical feedback for critical failures — players literally feel the world closing in. Follows existing opt-in feature pattern (`ASPIRE_FEATURE_WORLDBORDER`).
**Status:** ✅ Implemented. Build passes, 248 tests pass.
