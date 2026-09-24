# ADR-008: Static Glyph Path as an Explicit Source

## Status

Accepted (amended by [ADR-009](./0009-instance-renderer-and-glyph-set.md))

## Context

The existing `CaptchaFont.Glyphs` dictionary provides a hard-coded set of ASCII glyphs (digits + uppercase English letters), authored as bitmaps. It was described as a "static fallback for backward compatibility".

Two things were wrong with that framing:

- **It was not a fallback, it was a silent substitution.** `CaptchaFont.GetGlyph` filled unmapped characters with a placeholder rectangle, so a caller could not tell a real glyph from a missing one, and nothing could fail early because nothing ever failed.
- **It was not backward compatible.** Nothing in the image pipeline could reach the font-based renderer, so the static table was not a fallback *alongside* the dynamic path — it was the only path.

## Decision

- **`CaptchaFont.Glyphs` is retained** as the "static glyph path", and is reachable through `AddStaticGlyphSource()`.
- The static path is **not deprecated**; it is one of two equal glyph sources, both implementing `IGlyphRenderer`.
- **Explicit registration selection**: the application chooses the source once, at registration time. Registering two sources throws, so the outcome never depends on registration order. There is no default source.
- **The source never falls back.** The static source reports unmapped characters through `GlyphRenderResult.Failures` exactly as the font-based renderer does; it no longer substitutes a rectangle. Its `FallbackGlyph` constant is deleted.
- **Fallback is a build-time, application-level decision.** If the application wants unresolvable characters drawn as something, it configures `GlyphSetOptions.FallbackGlyph`, and the glyph set builder substitutes it for every unresolved character in the declared charset. The renderer sources remain honest about what they could not resolve.

## Consequences

### Positive

- The two sources behave identically under start-up validation: a character that cannot be drawn fails start-up regardless of which source is registered. There is no path that quietly survives a hole.
- Both sources are expressed in the same terms (`IGlyphRenderer` → `GlyphRenderResult`), so a third source can be added without touching the image pipeline.
- Substitution is now visible and intentional: a rectangle in the output means somebody configured a fallback, not that the library guessed.

### Negative

- **Not backward compatible**, contrary to the original framing. `AddStaticGlyphSource()` must be registered or nothing renders.
- Two source implementations to maintain.
- Applications that relied on the old automatic placeholder for unusual characters must now either add those characters to the charset or configure `FallbackGlyph`; otherwise start-up fails.
