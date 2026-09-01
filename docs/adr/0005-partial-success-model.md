# ADR-005: Partial Success Model

## Status

Accepted

## Context

The input string may contain a mix of renderable and non-renderable characters. The renderer should not abort on the first failure; instead, it should render what it can and report failures.

## Decision

The `GlyphRenderResult` captures both successes and failures:

- `Glyphs`: Dictionary of successfully rendered characters → glyphs.
- `Failures`: Dictionary of failed characters → failure reason.

A call always returns a result; it never throws due to per-character failures. Partial success is the expected outcome when some characters cannot be rendered.

The caller decides how to handle partial success:
- Use only the successful glyphs.
- Treat partial failure as an error condition.
- Retry with a different font or fallback.

## Consequences

### Positive

- Flexible: callers choose their error-handling strategy.
- Consistent: no "fast-fail" behavior surprises.
- Supports mixed-char strings (e.g., Latin + Cyrillic).

### Negative

- Callers must check both `Glyphs` and `Failures` if they care about failures.
- All-fail results (empty `Glyphs`) are possible but valid.