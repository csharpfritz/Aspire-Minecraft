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
