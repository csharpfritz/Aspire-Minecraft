# Static Configuration Pattern

**Confidence:** low
**Source:** earned

## When to Use

When a class has compile-time constants (`const`) that need to become runtime-configurable without breaking existing consumers.

## Pattern

1. Convert `const` fields to `static T { get; private set; } = <original value>`.
2. Add a public `Configure*()` method that sets the new values. Call once at startup.
3. Add an `internal static Reset*()` method that restores defaults — needed for test isolation since `private set` prevents external reset.
4. Ensure integer division preserves behavior: `7 / 2 = 3` matches the old hardcoded `3`.

## Key Considerations

- Default values **must** match the original constants exactly for backward compatibility.
- `const` → `static property` is a binary-breaking change (const values are inlined by the compiler). Only safe when all consumers are recompiled together (e.g., internal types, same solution).
- The `Reset()` method should be `internal` and gated behind `InternalsVisibleTo` so only test projects can call it.
- Place the `Configure*()` call in startup code before any consumer reads the properties.

## Example

```csharp
internal static class Layout
{
    public static int Size { get; private set; } = 7;

    public static void ConfigureExpanded() => Size = 15;

    internal static void ResetLayout() => Size = 7;
}
```
