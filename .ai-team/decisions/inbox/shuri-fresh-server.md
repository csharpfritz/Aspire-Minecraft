# Decision: Explicit Session Lifetime for Minecraft Container

**Date:** 2026-02-11
**Author:** Shuri (Backend Dev)
**Status:** Implemented

## Context

Jeff reported that Docker Desktop sometimes caches container state between Aspire runs, so even without `WithPersistentWorld()` the Minecraft server could retain world data across restarts.

## Decision

Added `.WithLifetime(ContainerLifetime.Session)` to the `AddMinecraftServer()` builder chain in `MinecraftServerBuilderExtensions.cs`.

## Rationale

- `ContainerLifetime.Session` is already the Aspire default, but being explicit:
  1. Documents the intent that ephemeral servers get a truly fresh container each run
  2. Protects against any future Aspire default changes
  3. Makes the behavior discoverable in code review
- The itzg/minecraft-server image stores world data in `/data`. Without a named volume (added by `WithPersistentWorld()`), data lives in the container's writable layer. Session lifetime ensures the container itself is destroyed and recreated.
- No sample app changes needed — the library handles it.

## Alternatives Considered

- **`FORCE_WORLD_COPY=true` env var**: Image-specific, doesn't address container caching.
- **`docker volume rm` documentation**: Manual step, bad DX.
- **Do nothing**: Session is already the default, but Docker Desktop behavior is unpredictable without explicit intent.

## Impact

- `MinecraftServerBuilderExtensions.cs`: 1 line added to builder chain.
- No breaking changes. No new dependencies.
