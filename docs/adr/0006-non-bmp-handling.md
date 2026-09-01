# ADR-006: Non-BMP Character Handling

## Status

Accepted

## Context

The glyph renderer operates on `char` (UTF-16 code unit). Characters outside the Basic Multilingual Plane (BMP) are represented as surrogate pairs in .NET. The existing `CaptchaFont.Glyphs` dictionary only supports BMP characters.

## Decision

- **Non-BMP characters are not supported**.
- Characters outside the BMP (e.g., emoji, some historic scripts) are **always rejected**.
- Rejection is reported as `GlyphRenderFailureReason.NonBmpCharacter` in the `Failures` dictionary.
- No attempt is made to render or fallback to the static glyph path for non-BMP.

## Consequences

### Positive

- Simplifies implementation (no surrogate-pair handling).
- Consistent with existing static glyphs (BMP-only).
- Clear boundary for future enhancement (non-BMP support is a separate feature).

### Negative

- Emoji and non-BMP scripts cannot be rendered (even if the font supports them).
- Callers must filter or validate non-BMP input if needed.