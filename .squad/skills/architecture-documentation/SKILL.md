---
name: "architecture-documentation"
description: "How to create reference documentation for complex coordinate systems and building constraints"
domain: "documentation"
confidence: "low"
source: "earned"
---

## Context

When a project involves complex spatial logic (coordinate systems, layout algorithms, size constraints) or technical constraints (rate limits, API boundaries), create two complementary reference documents:

1. **Architecture diagram** (educational) — visual explanations that help contributors understand the math and logic
2. **Constraints documentation** (prescriptive) — authoritative rules that enforce consistency

This skill applies when:
- Code uses non-obvious coordinate calculations or spatial algorithms
- There are hard technical limits (API rates, world boundaries, size caps)
- Multiple services/features must coordinate on shared conventions (e.g., all features use same Y-level convention)
- New contributors need to understand "why these specific numbers"

## Patterns

### Two-Document Pattern

**Document 1: Visual Architecture (`docs/architecture-diagram.md`)**
- ASCII art or mermaid diagrams showing spatial relationships
- Step-by-step coordinate calculation examples with real numbers
- Cross-sections showing different perspectives (top-down, side view)
- Example scenarios ("4-resource village coordinates")
- "Why" explanations: design rationale, trade-offs

**Document 2: Constraints Reference (`docs/[domain]-constraints.md`)**
- Table of contents for quick lookup
- Prescriptive rules in "must/never" language
- Exact constants and formulas (with source file references)
- Critical warnings highlighted with ⚠️ emoji
- Performance characteristics and scaling limits
- Anti-patterns section ("❌ Wrong: ... ✅ Correct: ...")

### Structure Patterns

Both documents should include:
- **Source file references** at the end — link to authoritative code
- **Cross-references** between docs (architecture → constraints, constraints → architecture)
- **Version-specific info** (e.g., "BaseY=-60 for superflat worlds")
- **Scaling limits** explicitly documented

### Content Organization

**Architecture diagrams:**
1. Big picture first (full coordinate system)
2. Zoom into specifics (individual structure footprints)
3. Worked examples with real numbers
4. Edge cases and scale limits last

**Constraints:**
1. Table of contents (long document, need quick lookup)
2. Foundational conventions first (coordinate system)
3. Building blocks next (structures, sizes)
4. Derived rules after (paths, fences — depend on structures)
5. System-level limits last (scale, performance)

## Examples

### ASCII Art for Y-Level Breakdown

```
Y=-59:  Air (player walking level)
        ↑ Players walk here
        │
Y=-60:  █████████████████████  ← BaseY (grass block surface)
        │                        Structures place floors here
```

**Why this works:** Visual hierarchy shows relationships. Annotations explain purpose, not just labels.

### Coordinate Calculation Example

```
col = index % 2           // Alternates 0, 1, 0, 1, ...
row = index / 2           // Increments every 2 resources: 0, 0, 1, 1, 2, 2, ...
x = BaseX + (col × 10)    // Result: 10, 20, 10, 20, 10, 20, ...
```

**Why this works:** Formula + concrete results + inline comments. Reader sees pattern immediately.

### Critical Rule Documentation

```markdown
### ⚠️ Critical Y-Level Rules

1. **Never place fences at `BaseY + 1`** — they will float one block above the ground.
2. **Paths must be at `BaseY - 1`** with grass cleared at `BaseY` — this makes them flush with the surrounding terrain.
```

**Why this works:** Emoji draws attention. "Never" is unambiguous. Explanation shows consequence of violation.

### Performance Constraint Table

```markdown
| Operation | Command Count | Estimated Time (at 10 cmd/sec) |
|-----------|---------------|--------------------------------|
| Single structure | ~15-20 commands | 1.5-2 seconds |
| 50-resource village | ~800-900 commands | 80-90 seconds |
```

**Why this works:** Concrete numbers. Shows scaling behavior. Helps users understand cost.

## Anti-Patterns

❌ **Single monolithic document** — hard to navigate, mixes "how it works" with "how to use it"

❌ **Diagrams without numbers** — "structures are spaced apart" is vague; "Spacing=10 blocks center-to-center" is precise

❌ **Constants without source references** — reader can't verify or update; always cite source file and line

❌ **Rules without rationale** — "Don't place fences at Y+1" → why? Add consequence: "they will float in the air"

❌ **Examples without scaling limits** — show 4-resource village working fine, hide that 50+ resources break

❌ **Missing cross-references** — constraints doc should point to architecture doc for visual explanations

❌ **Stale examples** — if constants change in code, docs break. Use `grep` to verify constants match.
