# Decision: Blog outline structure and media plan for v0.1.0

**By:** Mantis
**Date:** 2026-02-10

## What

Created three deliverables in `docs/blog/`:

1. **`v0.1.0-release-outline.md`** — Full blog post outline with 7 sections, placeholder code snippets from the actual API, and suggested social media copy.
2. **`v0.1.0-media-plan.md`** — 18 visual assets (11 screenshots, 4 GIFs, 3 composites) with exact capture instructions, required setup, and expected visuals.
3. **`v0.1.0-demo-script.md`** — 10-minute 4-act demo script: show code → tour the world → break something → recovery.

## Key Decisions

- **Blog narrative arc:** Hook with the absurd premise ("your distributed system fights back"), prove it's real with code, then show the drama of Sprint 1 features (weather, boss bars, title alerts).
- **Demo climax is the "break" moment:** Stopping a service and watching 6 feedback channels react simultaneously is the most compelling demo beat. The recovery (sun comes out) is the payoff.
- **18 media assets planned:** Covers blog, social media, and conference slides. The hero shot (split-screen dashboard + Minecraft world) is the most important single asset.
- **Media captures require Sprint 1 features:** Boss bar, weather, title alerts, particles, and sound features from Rocket must be implemented before media can be captured.
- **Blog gates on release tag:** Outline is ready; final blog should publish same day as the v0.1.0 NuGet tag.

## Dependencies

- Rocket's Sprint 1 features (boss bar, weather, title alerts, sounds, particles) must be complete before media capture.
- The blog references the actual sample `Program.cs` — if the sample API changes, update the blog code snippets.

## Why

This is the first public release. The blog post is the primary announcement channel and needs to land the "why should I care" immediately. .NET devs using Aspire are the audience — they know Aspire, they need to see that Minecraft integration is both fun and technically real.
