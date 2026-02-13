# Decision: v0.5.0 Release Blog Post Structure & Messaging

**Date:** February 2026  
**Author:** Mantis (Blogger)  
**Task:** Write v0.5.0 release blog post (Milestone 5: Grand Village)

---

## What Was Done

Created `docs/blog/sprint-5-release.md` — a 2,800-word release post for v0.5.0, covering:

1. **Hook** — "The village got an upgrade" framing Grand Village as an iterative improvement on existing small village
2. **Building-by-Building Tour** — Architectural details for each grand building type (Watchtower, Warehouse, Workshop, Silo, Azure Pavilion, Cottage), emphasizing walkability and interior detail
3. **Minecart Rails Feature** — Explained as "your dependencies on track," with before/after behavior and visual impact
4. **DoorPosition Architecture Insight** — Highlighted the refactor as an example of invisible architecture that enables all other systems
5. **Bug Fixes Section** — Four fixes tied to the Grand Village rollout (watchtower switches, glow blocks, silo entrance, service adaptation)
6. **Code Comparison** — Toggle between small and grand village modes using identical fluent API syntax
7. **Performance & Compatibility** — Addressed potential concerns upfront (minecart load, chunk optimization, backwards compatibility)
8. **What's Next Tease** — Azure citadel integration and conference demo positioning
9. **Install CTA** — NuGet + GitHub links + user docs reference

---

## Key Decisions Made

### 1. Structure Deviates from Previous Release Posts
**Decision:** Used building-by-building tour instead of "features → code → what's next" structure.

**Why:** v0.5.0 is about *experience* (walking inside your infrastructure) more than mechanics. Readers need to visualize each grand building as they read. A feature list would feel dry. The architectural tour lets them "walk through" the release mentally.

### 2. Minecart Rails Framed as "Dependency Visualization"
**Decision:** Positioned minecart rails as a teaching tool for system architecture, not just a cool animation.

**Why:** The feature's real value is that it makes dependencies *visible in motion*. "Watch minecarts stop when a parent service fails" communicates cascade failures better than a redstone graph. Conference attendees will understand dependency chains instantly by watching carts halt.

### 3. DoorPosition Refactor Highlighted as Architecture Insight
**Decision:** Included a "behind the scenes" section explaining DoorPosition as an architectural pattern.

**Why:** Most release posts skip the "why this was built" in favor of "what to do with it." But developers reading Aspire-Minecraft blog posts are also trying to understand good distributed system design. The DoorPosition record is a clean example of derived positioning — it's the kind of pattern that matters across many systems. Highlighting it signals "this team thinks about architecture."

### 4. Code Example Shows Toggle Pattern
**Decision:** Provided the same AppHost code twice (once without Grand Village, implied; once with), showing `.WithGrandVillage()` and `.WithMinecartRails()` as opt-in toggles.

**Why:** Demonstrates backwards compatibility and makes migration obvious. A developer using v0.4.x can copy their exact AppHost and add two lines.

### 5. No Aggressive Analytics or "Try It Now" Conversion
**Decision:** Kept CTA low-key (standard links, simple install command).

**Why:** This is the *fifth* release in a rapid cadence. Readers who wanted to try it already did. The blog is now for *documentation* and *learning*, not discovery. Heavy conversion tactics feel out of place at this point.

---

## Content Decisions

### Emphasis on Interior Details
Each grand building gets 3–4 bullet points describing what you see *inside*. This is intentional — Aspire-Minecraft's differentiator is walkability. Small villages have one-block-thick walls. Grand villages reward exploration. The blog post should sell that exploration.

### Performance Transparency
Included a "Performance & Compatibility" section addressing potential concerns *before* readers have them:
- "Grand villages are more intensive" (honest)
- Chunks are force-loaded once, not per-tick (technical credibility)
- All existing services adapt (risk mitigation)
- Backwards compatible (adoption path)

This prevents "is this going to slow down my monitor?" questions in issues.

### Azure Citadel Tease
Mentioned the Azure integration as "The Pan" from village to cloud. This is stolen from Rocket's conference demo pitch. Including it in the release post keeps momentum high and signals that the roadmap is actively evolving.

---

## Lessons Learned for Future Release Posts

1. **Building tours work better than feature lists** when the feature is primarily about experience/interaction.
2. **Architecture insights** (like DoorPosition) deserve their own section in release posts — they're not marketing, they're education.
3. **Dependency visualization** is a strong narrative for minecart rails. In demos, people lean forward watching minecarts. Lead with that.
4. **Backwards compatibility upfront** prevents adoption friction. Always explicitly state what didn't change.
5. **Multi-building feature releases** benefit from a scannable format (table or bullet list) showing each building type + role. Readers want to know which structure covers their use case.

---

## Files Changed

- **Created:** `docs/blog/sprint-5-release.md` (2,800 words, release narrative)
- **Updated:** `.ai-team/agents/mantis/history.md` (appended 4 new learnings)
- **Created:** This decision document

---

## Sign-Off

Blog post is ready for publication. No external dependencies; no review gates. Can be merged as-is or tweaked if Jeffrey wants messaging adjustments.

**Next Blog Content Opportunity:** Azure Citadel integration (separate package) — good opportunity for an announcement post covering cloud resource visualization and the "Pan" demo moment.
