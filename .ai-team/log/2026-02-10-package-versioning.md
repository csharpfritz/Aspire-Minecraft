# Session: Package Versioning & Sprint 2 Wrap-Up

**Date:** 2026-02-10
**Requested by:** Jeffrey T. Fritz

## Who Worked

- **Shuri** — Package versioning fix
- **Wong** — Release workflow versioning
- **Rocket** — Beacon color fix

## What Was Done

1. **Shuri** changed hardcoded `<Version>0.1.0</Version>` to `<Version>0.1.0-dev</Version>` in the csproj. CI overrides via `-p:Version=X.Y.Z` from the git tag.
2. **Wong** updated `.github/workflows/release.yml` to extract the version from the git tag (`v0.2.2` → `0.2.2`) and pass it to `dotnet build` and `dotnet pack` via `-p:Version=`.
3. **Rocket** fixed beacon tower glass colors to match the Aspire dashboard resource type palette (Project=blue, Container=purple, Executable=cyan, unhealthy=red, starting=yellow).

## Decisions Made

- Local/dev builds use `0.1.0-dev` suffix; release builds derive version from git tag.
- Release workflow is the single source of truth for NuGet package versions.
- Beacon colors now match Aspire dashboard resource type colors for visual consistency.

## Key Outcomes

- All changes pushed to GitHub and tagged `v0.2.2`.
- NuGet package versioning is now correct: tag `v0.2.2` produces `Fritz.Aspire.Hosting.Minecraft.0.2.2.nupkg`.
