# Decision: Heartbeat service uses BackgroundService pattern

**By:** Rocket
**Issue:** #27

**What:** `HeartbeatService` is the first feature to use `BackgroundService` (via `AddHostedService`) instead of the standard singleton pattern called from `MinecraftWorldWorker`. This gives it an independent timing loop for sub-second pulse intervals (1–4 seconds depending on health).

**Why:** The main worker loop runs on a 10-second `DisplayUpdateInterval`. A heartbeat that only fires every 10 seconds wouldn't feel like a pulse. The independent loop allows the heartbeat to run at 1-second intervals when healthy, creating an audible rhythm that players can hear and interpret.

**Implications:**
- Future features that need their own timing (animations, continuous effects) can follow this pattern.
- `HeartbeatService` does NOT inject into `MinecraftWorldWorker` — it runs independently but shares `RconService` and `AspireResourceMonitor` singletons.
- The RCON throttle deduplication is handled by micro-varying volume (0.001 increments per tick) — inaudible but makes each command string unique.

**Status:** ✅ Implemented. Build passes, 303 tests pass.
