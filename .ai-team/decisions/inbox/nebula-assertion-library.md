### FluentAssertions removed — replaced with xUnit built-in Assert

**By:** Nebula
**What:** Removed FluentAssertions package from both test projects (Aspire.Hosting.Minecraft.Rcon.Tests and Aspire.Hosting.Minecraft.Tests). Replaced all 95 assertion calls across 5 test files with xUnit's built-in `Assert` class. Removed `using FluentAssertions;` from all files. 62 tests, 0 failures after migration.
**Why:** FluentAssertions v8 (Xceed) has commercial licensing incompatible with this MIT-licensed open-source project. Jeff directed us to drop it entirely. xUnit's `Assert` was chosen over Shouldly or TUnit because all existing assertion patterns were simple (equality, boolean, null, empty, contains, throws) and mapped 1:1 to `Assert.*` methods — zero new dependencies, zero licensing risk. Shouldly would have been a fine alternative but adds an unnecessary package when xUnit covers everything we need.
**Status:** ✅ Resolved. FluentAssertions fully removed from both .csproj files and all test code.
