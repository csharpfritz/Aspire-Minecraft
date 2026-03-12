### WithExternalAccess() call order matters for BlueMap
**By:** Nebula
**Date:** 2026-02-27
**What:** `WithExternalAccess()` only marks endpoints that exist at call time. If called before `WithBlueMap()`, BlueMap's endpoint won't be marked external. Correct order: `.WithBlueMap().WithExternalAccess()`. This is documented with a passing test.
**Who should know:** Shuri (implementer), anyone writing AppHost samples or docs for #102.
