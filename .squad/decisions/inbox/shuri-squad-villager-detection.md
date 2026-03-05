# Decision: Squad Detection via WithSquadVillagers()

**Author:** Shuri
**Date:** 2026-02-27
**Status:** Implemented

## Context

Jeffrey requested that the Aspire hosting library detect `.squad/team.md` and inject agent names into the worker as an environment variable (`ASPIRE_SQUAD_AGENTS`) so the worker can spawn named villagers.

## Decision

- Created `WithSquadVillagers()` as a standalone opt-in extension method following the existing `With*()` pattern.
- Added it to the `WithAllFeatures()` chain so it's included when users enable everything.
- Parsing uses column-index detection from the header row (not regex) — robust to column reordering.
- Infrastructure agents (Scribe, Ralph) and agents with Silent/Monitor status are excluded by design.
- File discovery walks up from `AppHostDirectory` to find the repo root — same approach as other file-discovery patterns in the codebase.
- Graceful fallback: no `.squad/team.md` = no env var, no error.

## Impact

- `WithAllFeatures()` now includes `WithSquadVillagers()` in its chain.
- Worker needs to read `ASPIRE_SQUAD_AGENTS` env var and spawn villagers (separate task).
- No breaking changes to existing API surface.
