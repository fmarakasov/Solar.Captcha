# ADR-004: Per-Request Rendering

## Status

Accepted

## Context

The issue specifies "no caching" and "on each call, renders all chars and returns immediately." This contrasts with approaches that pre-render a charset at startup.

## Decision

The glyph renderer:

1. **Loads the font once** during construction (or first render, cached thereafter).
2. **Renders on each `Render(string)` call**.
3. **No charset-driven batch generation** — the caller provides the string of characters to render.
4. **No persistent cache** of rendered glyphs between calls.

## Consequences

### Positive

- Simple stateless call semantics.
- No startup latency for glyph pre-rendering.
- No memory pressure from a glyph cache.
- Caller controls exactly which characters are rendered.

### Negative

- Repeated calls to render the same character incur repeated rendering cost.
- Not optimal for high-volume captcha generation with static char sets (but caller can cache `GlyphRenderResult` if needed).