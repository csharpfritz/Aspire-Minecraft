### 2026-02-11: User Documentation Structure in user-docs/

**By:** Vision

**What:** Created comprehensive user documentation in `user-docs/` folder with guides for getting started, configuration, features, troubleshooting, and examples. Documentation follows consistent structure across all feature guides and emphasizes user perspective over technical implementation.

**Why:** 

1. **Separation of concerns:** User documentation (`user-docs/`) is separate from technical documentation (`docs/`). Users need "how to use" guides, not architecture deep-dives.

2. **Consistent structure:** Each feature document follows the same pattern (What It Does → How to Enable → What You'll See → Use Cases → Code Example) making docs scannable and predictable.

3. **User-centric language:** Documentation uses concrete, observable descriptions ("glowing yellow lamp", "fast high-pitched heartbeat") instead of technical implementation details ("glowstone block at Y+5", "1000ms interval, pitch 24").

4. **Comprehensive examples:** Every feature includes working code examples that users can copy-paste. Examples section shows real-world patterns for common scenarios (full stack apps, demos, ambient monitoring, etc.).

5. **Troubleshooting first:** Dedicated troubleshooting guide covers common issues with specific solutions, not generic advice. Organized by category (installation, startup, world generation, features, connection, performance).

6. **Progressive disclosure:** Documentation starts simple (README → Getting Started → Configuration) then provides deep-dives for specific features. Users can go as deep as they need.

**Impact:** Users can now understand and use all features without reading source code or technical architecture documents. Documentation is ready for external users (NuGet package consumers).
