# Session: 2026-02-17 CI Test Hang Fix

**Requested by:** Jeff

## What Happened

Nebula diagnosed and fixed a critical CI test hang that was blocking the Windows CI runners for 6+ hours until GitHub Actions timeout killed the job.

## Root Cause

The CI build.yml was running `dotnet test` against the entire solution file (`Aspire-Minecraft.slnx`) with a `--filter "Category!=Integration"` flag to exclude integration tests. However, the `--filter` flag only filters individual test methods at runtime—it does not prevent the test host process from starting. This meant the `Integration.Tests` project was still being loaded and initialized, which references `Aspire.Hosting.Testing` and the AppHost.

On Windows CI runners (without Docker or Minecraft), the AppHost initialization caused the test runner to hang indefinitely until the 6-hour GitHub Actions job timeout was reached.

## Solution

Changed `.github/workflows/build.yml` to explicitly list only the three unit test projects instead of running against the solution:
- `tests/Aspire.Hosting.Minecraft.Tests/`
- `tests/Aspire.Hosting.Minecraft.Worker.Tests/`
- `tests/Aspire.Hosting.Minecraft.Rcon.Tests/`

By listing projects explicitly, the `Integration.Tests` assembly is never loaded at all, eliminating the hang.

## Tradeoffs

- New test projects added to the solution must be manually added to `build.yml` (minor maintenance cost)
- Integration tests remain fully runnable locally via `dotnet test tests/Aspire.Hosting.Minecraft.Integration.Tests/`
- The build step still compiles the full solution, ensuring `Integration.Tests` code stays compilable

## Decision Merged

📌 **Decision:** CI Test Step: Exclude Integration.Tests by Testing Projects Explicitly
- Created in `.ai-team/decisions/inbox/nebula-ci-test-fix.md`
- Merged to `.ai-team/decisions.md` during this session

## Status

✅ Complete. CI tests no longer hang on Windows runners.
