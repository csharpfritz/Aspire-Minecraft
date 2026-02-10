# Decision: Changelog, Symbol Packages, CodeQL Scanning

**By:** Wong
**Issue:** #26

**What:**
1. Changelog generation uses GitHub's built-in `generate_release_notes: true` — no extra tooling.
2. NuGet symbol packages enabled via `IncludeSymbols`/`SymbolPackageFormat` in csproj. Release workflow pushes `.snupkg` explicitly. CI artifacts include `.snupkg`.
3. CodeQL security scanning added as `.github/workflows/codeql.yml` — C# only, default query suite, weekly + push/PR triggers.
4. GitHub Pages/docfx deferred to a future sprint.

**Why:**
- Changelog: GitHub auto-generated release notes are sufficient — the project uses PR-based workflow and the release action already had `generate_release_notes: true`.
- Symbol packages: `.snupkg` enables source-level debugging for consumers via NuGet Symbol Server. Combined with existing SourceLink configuration, consumers get full debug experience.
- CodeQL: Security scanning catches vulnerabilities before they reach main. Weekly schedule ensures dormant repos still get scanned.
- Separate `dotnet nuget push` for `.snupkg` (not relying on auto-detect) because explicit is better than implicit in CI — if snupkg push fails, the step name makes it obvious.
