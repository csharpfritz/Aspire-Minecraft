# Decision: RCON Burst Mode API

**Author:** Rocket
**Date:** 2026-02-12
**Issue:** #85

## Context

Milestone 5 Grand Village buildings generate 50–140 RCON commands each. At the steady-state 10 cmd/sec rate, a 6-resource village takes ~60 seconds to build. Burst mode temporarily raises throughput during initial construction.

## Decision

`RconService.EnterBurstMode(int commandsPerSecond = 40)` returns `IDisposable`. Usage:

```csharp
using (rcon.EnterBurstMode())
{
    await BuildAllStructuresAsync(ct);
}
// Rate limit automatically restored to 10 cmd/sec
```

- Thread-safe: only one burst session at a time (SemaphoreSlim).
- Throws `InvalidOperationException` if burst mode is already active.
- Logged at INFO on enter/exit.

## Who Needs to Know

- **Shuri** — no hosting API changes needed; burst mode is internal to the worker.
- **Rhodey** — aligns with Sprint 5 design doc §6 "RCON Burst Mode Design."
- **Nebula** — unit tests for burst mode should cover: enter/exit logging, double-enter rejection, dispose restoration, thread safety.
