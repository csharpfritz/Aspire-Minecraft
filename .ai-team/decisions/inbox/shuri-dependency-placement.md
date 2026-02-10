### Resource Dependency Placement + RCON Rate-Limiting

**By:** Shuri
**Issue:** #29

**What:**
1. **RCON rate-limiting:** Added `CommandPriority` enum and priority-aware `SendCommandAsync` overload. Token bucket rate limiter (default 10 commands/sec). High-priority commands bypass limits; low-priority commands queue in a bounded channel.
2. **Dependency placement:** `ResourceInfo` now carries a `Dependencies` list populated from `ASPIRE_RESOURCE_{NAME}_DEPENDS_ON` env vars. `VillageLayout.ReorderByDependency()` uses BFS topological sort to place parents before children.
3. **Hosting integration:** `WithMonitoredResource()` accepts `params string[] dependsOn` and auto-detects `IResourceWithParent`. Emits `_DEPENDS_ON` env var to the worker.

**Why:**
- Rate limiting prevents RCON command storms during cascading failures (multiple resources flapping simultaneously). The 250ms deduplication only catches identical commands; rate limiting catches total volume.
- Dependency placement ensures that services which depend on each other (e.g., API → Database) appear adjacent in the Minecraft village grid, making the visual layout match the logical architecture.
- `IResourceWithParent` auto-detection means users get dependency visualization without extra API calls for parent-child relationships already declared in Aspire.

**Design decisions:**
- Token bucket over sliding window: simpler, O(1) per check, well-understood pattern.
- `Channel<T>` for command queue: bounded (100), DropOldest prevents unbounded memory growth during sustained overload.
- `Dependencies` parameter defaults to null/empty for backward compatibility — no existing test or code changes needed.
- BFS topological sort handles cycles gracefully by appending remaining nodes.

**Status:** ✅ Resolved. Build passes, 303 tests pass.
