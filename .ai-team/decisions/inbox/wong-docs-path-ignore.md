### 2026-02-10: Documentation path filters added to GitHub Actions workflows

**By:** Wong

**What:** Added `paths-ignore` filters to build.yml, release.yml, and codeql.yml to skip CI/CD pipelines when only documentation files change. Ignored paths: `docs/**`, `user-docs/**`, `*.md` (root-level), `.ai-team/**`.

**Why:** Documentation updates (README, user docs, team state) don't affect code correctness, test outcomes, or package output. Running full build/test/pack cycles wastes CI runner minutes and creates noise in the workflow history. This change ensures CI resources are spent only on actual code changes.

**Impact:** PRs and commits that only touch markdown files or documentation directories will not trigger builds, tests, or CodeQL scans. The scheduled CodeQL run (Mondays) is unaffected and always runs regardless of path filters.
