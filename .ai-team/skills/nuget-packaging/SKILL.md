---
name: "nuget-packaging"
description: "NuGet packaging patterns for Aspire.Hosting.Minecraft packages"
domain: "dotnet-nuget"
confidence: "high"
source: "manual"
---

## Context
These patterns apply to all three NuGet packages in this repo: Aspire.Hosting.Minecraft, Aspire.Hosting.Minecraft.Rcon, and Aspire.Hosting.Minecraft.Worker. They ensure packages are NuGet.org-ready with reproducible builds.

## Patterns

### Never Use Floating Versions
All `PackageReference` entries must have exact pinned versions (e.g., `Version="13.1.0"`). Never use `Version="*"` or version ranges. NuGet.org rejects packages with floating version references and they break reproducible builds.

### Directory.Build.props Owns Shared Config
Shared metadata (Authors, License, RepositoryUrl) and build hardening properties live in `Directory.Build.props` at the repo root. Individual csproj files only add project-specific properties (PackageId, Version, Description, PackageTags, IsPackable).

### Required Hardening Properties
Every packable project inherits these from `Directory.Build.props`:
- `GenerateDocumentationFile` — ships XML docs in the nupkg
- `EnablePackageValidation` — catches breaking API changes between versions
- `Deterministic` — ensures byte-identical builds from same source
- `ContinuousIntegrationBuild` (CI-only) — embeds correct source paths for SourceLink
- `EmbedUntrackedSources` — ensures all source files are available in SourceLink
- `Microsoft.SourceLink.GitHub` — enables debugger source mapping to GitHub

### Per-Package READMEs
Each packable project has its own `README.md` in the project directory, included in the nupkg via:
```xml
<None Include="README.md" Pack="true" PackagePath="\" />
```
The `PackageReadmeFile` property in `Directory.Build.props` is set to `README.md`. Do NOT use the repo-root README for packages.

### Pack and Verify Workflow
```bash
dotnet restore
dotnet build -c Release
dotnet pack -c Release -o nupkgs
```
Check for: zero warnings on src projects, correct package sizes, no floating versions in generated nuspec.

## Anti-Patterns
- **Floating `Version="*"`** — will be rejected by NuGet.org and breaks reproducibility
- **Shared repo-root README for all packages** — each package needs its own focused README for NuGet.org
- **Missing `EnablePackageValidation`** — won't catch breaking API changes between releases
- **Missing SourceLink** — consumers can't step into source during debugging
