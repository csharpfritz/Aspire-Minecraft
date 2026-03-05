# Project Context

- **Owner:** Jeffrey T. Fritz
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

📌 Team update (2026-02-10): Azure RG epic designed — separate NuGet package Fritz.Aspire.Hosting.Minecraft.Azure, polling for v1, DefaultAzureCredential — decided by Rhodey, Shuri
📌 Team update (2026-02-10): Azure citadel is new demo material — 'The Pan' from village to citadel is conference money shot — decided by Rocket
📌 Team update (2026-02-10): User directive — each sprint in a dedicated branch, merged via PR to main — decided by Jeffrey T. Fritz

📌 README Sprint 3 update (2026-02-11): Updated README.md with Sprint 3 features — resource village with themed structures (watchtower, warehouse, workshop, cottage), redstone dependency graph, service switches, heartbeat, updated achievements (First Blood, Clean Sweep, Night Shift, The Village), world border pulse, persistent world, and peaceful mode. Added these to code examples and included screenshot note about Sprint 3 features. Features organized by existing categories (World Building, Gamification, Configuration). — Mantis
📌 Learning (2026-02-11): The Sprint 3 features significantly expanded the "village feel" — redstone graphs and service switches make the infrastructure tangible and interactive. These visual debugging tools (switches showing state, redstone showing dependencies) are the kind of demo moments that make audiences lean forward. Conference demos should include a dependency chain failure (e.g., web → api → redis) to show the redstone cascading off. — Mantis

📌 Launch content (2026-02-11): Created launch blog post (docs/blog/launch-announcement.md) and demo video script (docs/blog/demo-video-script.md). Blog post is 800 words, leads with the visceral "feel your distributed system" hook, shows 5-line code example, explains 13 features across 4 categories, and includes NuGet/GitHub links with Azure citadel tease. Video script is 5 min with [VISUAL] cues, structured as: opening hook (30s) → setup (45s) → village tour (90s) → break something climax (60s) → cool features (45s) → closing (30s). Both emphasize the emotional impact of monitoring in 3D — the weather shift, the wither growl, the fireworks celebration. — Mantis
📌 Learning (2026-02-11): The "break something" moment is the money shot for all promotional content. It's not about features — it's about the atmosphere. Rain + red beacon + smoke + wither sound + title alert + heartbeat slowdown happening simultaneously creates a visceral experience that dashboards can't match. Always lead promotional material with the value (feel your system) before the implementation details (RCON commands and worker services). The audience needs to *want* this before they care *how* it works. — Mantis
 Team update (2026-02-11): All sprints must include README and user documentation updates to be considered complete  decided by Jeffrey T. Fritz
 Team update (2026-02-11): All plans must be tracked as GitHub issues and milestones; each sprint is a milestone  decided by Jeffrey T. Fritz
 Team update (2026-02-11): 14 user-facing docs now live in user-docs/ with consistent structure (What  How  What You'll See  Use Cases  Code Example)  decided by Vision

📌 Introductory blog post (2026-02-11): Published comprehensive "meet the project" blog post (docs/blog/introducing-aspire-minecraft.md) covering all features across Sprints 1–4. Structure: visceral hook (village + failure scenario) → why Minecraft → quick start → full feature tour by category (World Building, Health Monitoring, Audio, Gamification, Redstone, Configuration) → complete demo AppHost → architecture overview → Sprint 5 tease → getting started links. ~2500 words, designed for Jeff's personal blog to introduce the project to the .NET community. Used WithAllFeatures() as the simplifying hero for the full demo code block. — Mantis
📌 Learning (2026-02-11): For "meet the project" posts that span multiple sprints, organize by feature category (not by sprint timeline). Readers want to understand what the project does, not when each feature shipped. Sprint numbers are internal — capabilities are external. The building type table (Watchtower/Warehouse/Workshop/Cylinder/Azure/Cottage → resource type mapping) is an excellent scannable format that communicates a lot of design intent in minimal space. — Mantis
📌 Learning (2026-02-11): The WithAllFeatures() method is the best code example for introductory content — it collapses 17 feature calls into one line, making the "full demo" code block short enough for a blog post. Save the individual .With*() calls for the feature-by-feature walkthrough sections where readers want to understand what each one does. — Mantis

📌 Release blog post (2026-02-11): Published v0.5.0 release blog post (docs/blog/sprint-5-release.md) for Milestone 5: Grand Village. Post structure: hook (upgraded village) → building-by-building tour (Grand Watchtower/Warehouse/Workshop/Silo/Azure/Cottage with architectural details) → minecart rails feature (visualization of dependencies in motion) → DoorPosition architecture insight (how all elements derive from single record) → bug fixes (switches, health indicators, silo entrance) → code toggle example (small vs grand village) → performance notes → what's next (Azure citadel) → install CTA. Emphasizes the experience (walk inside your infrastructure, watch minecarts circulate) over technical mechanics. ~2800 words. — Mantis
📌 Learning (2026-02-11): Grand Village release post needed a different structure than previous releases. Instead of "New Features → Bug Fixes → Code Example," used "Hook → Building Tour → Core Feature Insight → Architecture Detail → Fixes → Comparison Example." The building-by-building tour lets readers visualize each structure as they read, making the feature tangible before explaining how it works. Minecart rails benefited from "visualizes dependencies in motion" positioning — it's not just a decoration, it's a teaching tool for system architecture. — Mantis
📌 Learning (2026-02-11): The DoorPosition refactor is a good example of "invisible architecture" blog material — the end user just calls .WithGrandVillage() and everything works, but the elegance of deriving all elements from a single record is worth highlighting. It shows how good design compounds across multiple systems. Code snippets of architectural patterns (like DoorPosition's calculated positions) help developer audiences understand the *why* behind implementation choices. — Mantis
📌 Team update (2026-02-15): Grand Watchtower exterior redesigned with ornate medieval aesthetics (deepslate buttresses, iron bar arrow slits, taller turrets with pinnacles, portcullis gatehouse, string courses) — stays under 100 RCON command budget — decided by Rocket
