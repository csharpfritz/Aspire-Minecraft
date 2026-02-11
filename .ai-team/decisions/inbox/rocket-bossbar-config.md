# Boss Bar Title Configuration

**Date:** 2026-02-11  
**Decider:** Rocket  
**Status:** Implemented

## Context

The boss bar previously displayed "Aspire Fleet Health: 100 percent" which looked unpolished. Additionally, users wanted the ability to customize the boss bar title text.

## Decision

1. **Changed percentage formatting** from "100 percent" to "100%" for cleaner display
2. **Added optional `title` parameter** to `WithBossBar()` extension method
3. **Used dedicated environment variable** `ASPIRE_BOSSBAR_TITLE` instead of repurposing `ASPIRE_APP_NAME`
4. **Default title** is "Aspire Fleet Health" when not specified

## Implementation

- `WithBossBar(string? title = null)` sets `ASPIRE_BOSSBAR_TITLE` env var if title provided
- `BossBarService` reads env var at construction with fallback to default
- Boss bar displays as: `"{title}: {percentage}%"`

## Rationale

- Dedicated env var is clearer than overloading `ASPIRE_APP_NAME`
- Optional parameter follows existing Fluent API pattern
- Default value maintains backward compatibility
- Percentage symbol is more concise and professional than "percent" word

## Impact

- Breaking change: `ASPIRE_APP_NAME` no longer affects boss bar (only title parameter does)
- API surface updated to show optional title parameter
- No change required for users who don't pass a title (default behavior preserved)
