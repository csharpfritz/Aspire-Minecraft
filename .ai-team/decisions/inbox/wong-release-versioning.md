### Release workflow now extracts version from git tag

**By:** Wong
**What:** Updated `.github/workflows/release.yml` to extract the semantic version from the git tag (`v0.2.1` → `0.2.1`) and pass it to `dotnet build` and `dotnet pack` via `-p:Version=`. The GitHub Release step now includes the version in its name. The CI workflow (`build.yml`) is intentionally unchanged — CI builds use the default csproj version.
**Why:** Previously, every tag-triggered release produced `0.1.0` packages regardless of the actual tag. This meant pushing a `v0.2.0` tag would publish `Fritz.Aspire.Hosting.Minecraft.0.1.0.nupkg` — wrong version, and `--skip-duplicate` would silently skip it if `0.1.0` already existed on NuGet.org. Now the tag is the single source of truth for release versions.
**Status:** ✅ Resolved.
