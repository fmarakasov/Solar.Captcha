# ADR-008: Static Glyph Path Retention

## Status

Accepted

## Context

The existing `CaptchaFont.Glyphs` dictionary provides a hard-coded set of ASCII glyphs (digits + uppercase English letters). This is a static fallback for backward compatibility.

## Decision

- **`CaptchaFont.Glyphs` is retained** as the "static glyph path".
- The static path is **not removed or deprecated**.
- **Explicit caller selection**: the caller chooses the renderer implementation at call time (DI resolves either static or dynamic).
- **No implicit fallback**: the dynamic renderer does not silently fall back to the static dictionary if a character fails.

## Consequences

### Positive

- Backward compatibility for callers relying on static glyphs.
- No breaking changes to existing integrations.
- Clear separation of concerns: static (hardcoded) vs. dynamic (font-based).

### Negative

- Two code paths to maintain (static + dynamic).
- Callers must explicitly select which path to use.