# ADR-007: Whole-Call Failure Exception

## Status

Accepted

## Context

Some failures affect the entire render call, not individual characters. The primary example is an unloadable font file (invalid path, corrupt file, etc.).

## Decision

- Whole-call failures throw a custom **`GlyphRendererException`**.
- This exception wraps the underlying error with context (font path, inner exception).
- The exception is thrown at render time (not at renderer construction, unless options validation catches it).
- Per-character failures are **not** thrown; they are captured in `GlyphRenderResult.Failures`.

## Consequences

### Positive

- Clear distinction between "this character failed" and "the entire call failed".
- Callers can catch `GlyphRendererException` for whole-call recovery (e.g., retry with a fallback font).
- Debugging context (path, inner exception) aids troubleshooting.

### Negative

- Additional exception type to document and test.