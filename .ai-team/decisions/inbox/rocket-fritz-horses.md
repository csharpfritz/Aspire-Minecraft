### 2026-02-12: Fritz's horses are always-on, not feature-gated
**By:** Rocket
**What:** HorseSpawnService registered as a plain singleton with non-nullable injection into MinecraftWorldWorker. No ASPIRE_FEATURE_ env var or opt-in check. Horses spawn unconditionally after village structures are built.
**Why:** Easter eggs should be discovered, not configured. Adding a feature flag would defeat the purpose. The service is cheap (3 RCON commands, runs once) and the horses add personality to every village. Fritz's real horses — Charmer, Dancer, and Toby — deserve to always be present.
