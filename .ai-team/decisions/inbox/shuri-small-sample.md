### SmallVillageDemo: Minimal 2-resource sample
**By:** Shuri
**Date:** 2026-02-27
**What:** Created `samples/SmallVillageDemo/` with just a web app + PostgreSQL, monitoring both via `.WithMonitoredResource()`. This is the smallest possible Aspire+Minecraft integration sample.
**Why:** The existing GrandVillageDemo has 13 monitored resources. We need a sample that exercises the village layout with only 2 resources to verify spacing, fence perimeter, and building placement scale down correctly. Also serves as a copy-paste starting point for users who want a simple setup.
