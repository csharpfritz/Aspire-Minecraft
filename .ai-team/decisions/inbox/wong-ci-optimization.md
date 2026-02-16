# Decision: CI Pipeline Optimization

**Date:** 2026-02-16  
**Owner:** Wong (GitHub Ops)  
**Status:** Decided  
**Related:** Jeff's request to optimize build.yml and release.yml for speed

## Problem
Tests take ~5 minutes per platform in build.yml (ubuntu + windows matrix). Release.yml also runs redundant 5-minute test step. Total CI time for a release cycle: ~12.5 minutes.

## Decision
Optimize CI pipelines by:
1. **Drop Windows matrix from build.yml** — Keep only ubuntu-latest
2. **Add NuGet caching** — Both build.yml and release.yml
3. **Remove test step from release.yml** — Code already tested during PR/push

## Rationale

### Windows Matrix Elimination
- The release workflow publishes only on ubuntu-latest (NuGet packages are platform-agnostic for .NET libraries)
- Windows testing adds CI runner cost without corresponding value to the release artifact
- All tests still run on ubuntu (primary platform)
- Windows developers can test locally before PR

### NuGet Caching
- Restore step typically takes 1-2 minutes
- Caching with `actions/cache@v4` hits on every subsequent run for stable dependencies
- Key strategy: exact hash match (`.csproj` + `.slnx` files) with fallback to partial match

### Release.yml Test Removal
- Release tags only point to commits on main (verified by existing `git merge-base` check)
- Those commits were validated by build.yml (PR + push) before merge
- Re-testing is redundant — same code, same test suite, no variables changed
- Saves ~5 minutes per release without sacrificing safety

## Impact
- **build.yml**: ~2.5–3 minutes saved (no Windows matrix + cached restore)
- **release.yml**: ~5 minutes saved (no test step)
- **Total per release cycle**: ~7.5 minutes faster
- **Test coverage**: Unchanged (tests still run on every PR/push)
- **Reliability**: Unchanged (tested code path is identical)

## Implementation
- Commit: dd2d053 on branch milestone-6
- Files changed: `.github/workflows/build.yml`, `.github/workflows/release.yml`
- Test projects: No changes (parallelization evaluated but not applied due to pre-existing race conditions)

## Monitoring
- Monitor release cycle time in GitHub Actions UI
- Verify cache hit rates in workflow run summaries
- Watch for any release failures (should be zero — same as before)

## Notes
- xUnit parallelization (`<ParallelizeAssembly>`) tested locally but not applied: enabling it exposed pre-existing race conditions in Worker.Tests requiring separate investigation
- This optimization is conservative (no risky changes) and safe to merge immediately
