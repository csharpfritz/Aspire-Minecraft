### Sprint 2 feature decisions — action bar ticker, beacon towers, boss bar app name

**By:** Rocket
**Issues:** #38, #20, #22
**What:** Three new Sprint 2 features implemented following the established opt-in env var pattern. Boss bar now supports configurable app name via `ASPIRE_APP_NAME` (fallback: "Aspire"). Action bar ticker cycles TPS/MSPT/healthy count/RCON latency via `title @a actionbar`. Beacon towers build iron+beacon+glass structures per resource with green/red health indication.

**Key decisions:**
- **`WithBossBar()` signature:** Added optional `string? appName = null` parameter to maintain backward compatibility. Existing callers don't break.
- **Action bar ticker reads fresh metrics:** Each tick polls TPS/MSPT/list directly via RCON rather than sharing cached values from the main worker loop. This keeps the ticker self-contained and avoids coupling to the main loop's timing.
- **Beacon tower Z offset:** Towers placed at Z=8 (vs existing structures at Z=0) with same X spacing to avoid collisions while keeping resources visually grouped.
- **Single-layer iron base:** Minecraft requires minimum 3x3 iron for beacon activation. Single layer (not pyramid) is sufficient and keeps structures compact.
- **Plain strings for action bar:** Used `title @a actionbar "{text}"` with plain strings, NOT JSON text components, consistent with Sprint 1 convention.

**Status:** ✅ Implemented. 248 tests pass (0 failures).
