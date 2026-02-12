# Decision: MinecartRailService Registration Stubbed

**Author:** Shuri
**Date:** 2026-02-12
**Issue:** #79

## Context

`WithMinecartRails()` sets the `ASPIRE_FEATURE_MINECART_RAILS` env var on the worker, and `Program.cs` checks for it. However, `MinecartRailService` does not exist yet — it's planned for Phase 3 of the Milestone 5 design (Rocket's scope).

## Decision

The `ASPIRE_FEATURE_MINECART_RAILS` check in `Program.cs` is wired up with a comment placeholder instead of a `builder.Services.AddSingleton<MinecartRailService>()` call. When Rocket implements the service, they just need to uncomment/add the registration line.

## Rationale

- The env var plumbing is in place end-to-end (extension method → worker env var → Program.cs check).
- Registering a non-existent type would cause a compile error.
- This follows the same pattern used in other milestones where the flag was wired before the service existed.

## Impact

- No behavioral change until `MinecartRailService` is implemented.
- `WithAllFeatures()` will set the flag even though the service isn't registered yet — this is harmless since the flag alone does nothing without the service.
