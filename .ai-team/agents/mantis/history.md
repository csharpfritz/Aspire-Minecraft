# Project Context

- **Owner:** Jeffrey T. Fritz (csharpfritz@users.noreply.github.com)
- **Project:** Aspire.Hosting.Minecraft — .NET Aspire integration for Minecraft servers
- **Stack:** C#, .NET 10, Docker, Aspire, OpenTelemetry, Minecraft Paper Server, RCON
- **Created:** 2026-02-10

## Key Facts

- Three NuGet packages: Aspire.Hosting.Minecraft (hosting lib), Aspire.Hosting.Minecraft.Rcon (RCON client), Aspire.Hosting.Minecraft.Worker (in-world display)
- Currently at version 0.1.0 — first public release pending
- Key features: Minecraft server as Aspire resource, OpenTelemetry instrumentation, BlueMap web maps, in-world holograms/scoreboards
- Target audience: .NET developers using Aspire who also play Minecraft
- MIT licensed, hosted on GitHub (csharpfritz/Aspire-Minecraft)

## Learnings

<!-- Append new learnings below. Each entry is something lasting about the project. -->

📌 Team update (2026-02-10): 18 features proposed — rich demo material for blog content — decided by Rocket
📌 Team update (2026-02-10): 3-sprint roadmap adopted — Sprint 1 assigns Mantis: blog outline + demo screenshots; blog gates on release tag — decided by Rhodey
📌 Team update (2026-02-10): All sprint work tracked as GitHub issues with team member and sprint labels — decided by Jeffrey T. Fritz
📌 Blog outline (2026-02-10): Created v0.1.0 release blog outline, media plan (18 assets), and demo script (10-min format) in docs/blog/. Blog structure: hook → why → getting started → feature highlights → architecture → what's next. Demo climax is "break a service, watch the world react." — Mantis
📌 Learning (2026-02-10): The demo AppHost includes 5 monitored resources (api, web, cache, db-host, db) — good for showing scoreboard/boss bar at scale. Demo script should always reference the actual sample, not a simplified version. — Mantis
📌 Learning (2026-02-10): Sprint 1 features (boss bar, weather, title alerts, sounds, particles) are the dramatic core of the v0.1.0 story — they transform passive monitoring into visceral feedback. The blog and demo should lead with the "break something" moment. — Mantis

📌 Team update (2026-02-10): NuGet PackageId renamed from Aspire.Hosting.Minecraft to Fritz.Aspire.Hosting.Minecraft (Aspire.Hosting prefix reserved by Microsoft) — decided by Jeffrey T. Fritz, Shuri
📌 Blog post (2026-02-10): Published v0.1.0 release blog post (docs/blog/v0.1.0-release.md), social media thread (docs/blog/social-thread.md), and behind-the-build draft outline (docs/blog/behind-the-build-draft.md). Blog leads with the "break a service, watch the weather change" hook. Code examples use actual sample AppHost API with all 5 Sprint 1 features. Social thread is 7 posts for Twitter/Bluesky. Behind-the-build covers RCON protocol, Aspire resource model, and worker architecture with 3 opening paragraphs written. — Mantis
📌 Learning (2026-02-10): The v0.1.0 blog post code examples must always show `WithAspireWorldDisplay<T>()` before any Sprint 1 `.With*()` calls — the opt-in methods throw if called before the world display is configured. This ordering matters for copy-paste correctness. — Mantis

📌 Team update (2026-02-10): NuGet package version now defaults to 0.1.0-dev; CI overrides via -p:Version from git tag — decided by Shuri
📌 Team update (2026-02-10): Beacon tower colors now match Aspire dashboard resource type palette — update media assets — decided by Rocket
📌 Team update (2026-02-10): WithServerProperty API and ServerProperty enum added — new docs/blog content opportunity — decided by Shuri
📌 Team update (2026-02-10): Sprint 2 API review complete — 10 feature methods available for blog coverage — decided by Rhodey

📌 Deep-dive blog (2026-02-10): Published "Behind the Build" architecture deep-dive (docs/blog/behind-the-build.md). Covers worker service pattern, feature opt-in via env vars, village layout system (2×N grid + dependency ordering), health monitoring pipeline (HTTP/TCP → ResourceMonitor → RCON), RCON command patterns (250ms throttle, token bucket rate limiting, JSON text components, % format specifier workaround), structure type mapping (Project→Watchtower, Container→Warehouse, Executable→Workshop), beacon color palette, heartbeat timing tricks, and OpenTelemetry dual-stream architecture. Includes code snippets from actual source files. — Mantis
📌 Conference demo guide (2026-02-10): Published conference demo walkthrough (docs/blog/conference-demo-guide.md). 6-act demo script (15 min): village tour → feature showcase → break a service (the climax) → recovery celebration. Includes pre-show setup checklist, troubleshooting guide, talking points cheat sheet with RCON commands, and slide suggestions. — Mantis
📌 README overhaul (2026-02-10): Overhauled README.md with categorized feature list (World Building, Health Monitoring, Audio & Effects, Gamification, Configuration), added Quick Start with minimal 10-line code example, added Full Feature Demo section with all 13 features, updated architecture diagram to reflect worker capabilities, and added link to Behind the Build deep-dive. Removed sprint references from code examples — features are now organized by category, not implementation timeline. — Mantis
📌 Learning (2026-02-10): The README needs to lead with the simplest possible code example (6 lines to a working Minecraft server) before showing the full feature demo. Conference audiences need a "this is easy" moment before the "this is powerful" moment. The same applies to blog posts. — Mantis
📌 Learning (2026-02-10): HeartbeatService has a subtle RCON deduplication workaround — it varies the volume by tick count (0.001 increments) to make each playsound command unique. This is a great anecdote for architecture talks about working with constraints. — Mantis
