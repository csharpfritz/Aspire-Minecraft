# Project Context

- **Owner:** Jeffrey T. Fritz (csharpfritz@users.noreply.github.com)
- **Project:** Aspire.Hosting.Minecraft — .NET Aspire integration for Minecraft servers
- **Stack:** C#, .NET 10, Docker, Aspire, OpenTelemetry, Minecraft Paper Server, RCON
- **Created:** 2026-02-10

## Key Facts

- Three NuGet packages: Aspire.Hosting.Minecraft, Aspire.Hosting.Minecraft.Rcon, Aspire.Hosting.Minecraft.Worker
- Version 0.1.0, packed via `dotnet pack -o nupkgs`
- GitHub repo: csharpfritz/Aspire-Minecraft
- No CI/CD pipeline exists yet (no .github/workflows/ found)
- .gitattributes configured for merge=union on .ai-team/ files
- MIT licensed

## Learnings

<!-- Append new learnings below. Each entry is something lasting about the project. -->

📌 Team update (2026-02-10): NuGet packages blocked — no CI/CD pipeline exists, must be created — decided by Shuri
📌 Team update (2026-02-10): 3-sprint roadmap adopted — Sprint 1 assigns Wong: build.yml CI, release.yml stub, branch protection — decided by Rhodey

### Sprint 1 — CI/CD Pipeline Created (2026-02-10)

**Workflows created:**
- `.github/workflows/build.yml` — CI on push/PR to `main`, matrix build (ubuntu + windows), restore → build → test → pack → upload artifacts. Concurrency groups cancel stale runs.
- `.github/workflows/release.yml` — Publishes to NuGet.org on `v*` tag push. Also creates a GitHub Release with nupkg files attached. Uses `softprops/action-gh-release@v2`.
- `.github/PULL_REQUEST_TEMPLATE.md` — Standard PR template with What/Why/Testing/Checklist sections.

**Key decisions:**
- No separate `pr-validation.yml` — `build.yml` already triggers on PRs, so a separate workflow would be duplicate work and wasted runner minutes.
- NuGet artifact upload restricted to `ubuntu-latest` to avoid duplicate artifact names in the matrix.
- `--skip-duplicate` on nuget push so re-running a tag workflow doesn't fail if packages were already published.
- Used `dotnet-version: '10.0.x'` to match the `net10.0` target framework in Directory.Build.props.

**Secrets required:**
- `NUGET_API_KEY` — must be added in GitHub repo Settings → Secrets → Actions. Generate from nuget.org account → API Keys.

📌 Team update (2026-02-10): NuGet hardening completed — 6 floating deps pinned, SourceLink/deterministic builds added to Directory.Build.props — decided by Shuri
📌 Team update (2026-02-10): Test infrastructure created — 62 tests (45 RCON + 17 hosting) now available for CI execution — decided by Nebula
📌 Team update (2026-02-10): All sprint work tracked as GitHub issues with team member and sprint labels — decided by Jeffrey T. Fritz

📌 Team update (2026-02-10): Single NuGet package consolidation — only Aspire.Hosting.Minecraft is packable, CI/CD should build/pack accordingly — decided by Jeffrey T. Fritz, Shuri

📌 Team update (2026-02-10): NuGet PackageId renamed from Aspire.Hosting.Minecraft to Fritz.Aspire.Hosting.Minecraft (Aspire.Hosting prefix reserved by Microsoft) — decided by Jeffrey T. Fritz, Shuri
