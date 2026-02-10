# Project Context

- **Owner:** Jeffrey T. Fritz (csharpfritz@users.noreply.github.com)
- **Project:** Aspire.Hosting.Minecraft — .NET Aspire integration for Minecraft servers
- **Stack:** C#, .NET 10, Docker, Aspire, OpenTelemetry, Minecraft Paper Server, RCON
- **Created:** 2026-02-10

## Key Facts

- Three NuGet packages: Aspire.Hosting.Minecraft (hosting lib), Aspire.Hosting.Minecraft.Rcon (RCON client), Aspire.Hosting.Minecraft.Worker (in-world display)
- All packages at version 0.1.0, packable via `dotnet pack -o nupkgs`
- Hosting lib depends on Aspire.Hosting and the Rcon project
- Worker depends on Microsoft.Extensions.Hosting, Http, and OpenTelemetry packages
- Rcon depends only on Microsoft.Extensions.Logging.Abstractions
- Content files: bluemap/core.conf and otel/opentelemetry-javaagent.jar bundled with hosting package
- Uses PackageReference with Version="*" for Aspire packages (floating versions)

## Learnings

<!-- Append new learnings below. Each entry is something lasting about the project. -->

### NuGet Readiness Audit (2026-02-10)

- **Build status:** `dotnet build Aspire-Minecraft.slnx` succeeds cleanly (0 errors, 0 warnings on src projects).
- **Pack status:** `dotnet pack -o nupkgs` succeeds. Produces three `.nupkg` files. Two sample projects correctly emit "not packable" warnings.
- **Package sizes:** Hosting=~41 MB (due to 23 MB otel jar), Rcon=~20 KB, Worker=~28 KB.
- **Content files:** `bluemap/core.conf` and `otel/opentelemetry-javaagent.jar` are packed as both `content/` and `contentFiles/` entries — correct for consumer copy-to-output behavior.
- **Floating versions:** All three csproj files use `Version="*"` on PackageReference. Pack resolves these to concrete versions in the nuspec (e.g., Aspire.Hosting → 13.1.0). This is fine for local dev but is fragile for reproducible builds and should be pinned before publishing.
- **Directory.Build.props** (`Directory.Build.props`): Sets shared metadata — Authors, License (MIT), PackageProjectUrl, RepositoryUrl, RepositoryType, PackageReadmeFile. Conditionally includes repo-root README.md in all packages.
- **Missing from all csproj/props:** `GenerateDocumentationFile`, `EnablePackageValidation`, `SourceLink`, `Deterministic`, `ContinuousIntegrationBuild`, `EmbedUntrackedSources`, `PackageIcon`, `PackageReleaseNotes`.
- **README:** All three packages share the repo-level `README.md`. No per-package README.
- **CI/CD:** No `.github/workflows/` files exist. Only `.github/agents/squad.agent.md`.
- **XML docs:** Present on public APIs in all three projects (confirmed by grep). But `GenerateDocumentationFile` is not enabled, so no XML doc file is shipped in the nupkg.
- **No icon:** `img/sample-1.png` exists but no `PackageIcon` property is set. NuGet.org will show a default icon.

📌 Team update (2026-02-10): 18 Minecraft interaction features proposed across 3 tiers — decided by Rocket
📌 Team update (2026-02-10): 3-sprint roadmap adopted — Sprint 1 assigns Shuri: pin deps, NuGet hardening, extract otel jar, verify pack — decided by Rhodey
