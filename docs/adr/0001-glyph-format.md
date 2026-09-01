# ADR-001: Glyph Format

## Status

Accepted

## Context

The existing `CaptchaFont.Glyphs` dictionary stores each character as a 14-byte array representing an 8×14 bitmap. Each byte is one row of 8 bits (MSB = leftmost pixel). This format is consumed by `CaptchaImageGenerator` and `CaptchaImage` without transformation.

## Decision

The new dynamic glyph renderer will produce glyphs in the **same binary 1-bit format**:

- **Size**: Exactly `byte[14]` (14 rows × 8 bits per row)
- **Byte order**: Row-major; each byte represents one row from top to bottom
- **Bit order**: MSB (bit 7) = leftmost pixel of that row; LSB (bit 0) = rightmost
- **Empty value**: `0x00` (all pixels off in that row)
- **Anti-aliasing**: Not supported. Every pixel is either fully on (1) or fully off (0).

## Consequences

### Positive

- No breaking changes to the image rendering pipeline (`CaptchaImage`, PNG encode, scaling in `DrawCaptchaText`).
- Direct drop-in replacement: the `GlyphRenderResult` can be used anywhere `CaptchaFont.Glyphs` is used.
- Simple, deterministic output for testing.

### Negative

- Cannot render anti-aliased or grayscale glyphs.
- Font rendering quality limited by the 8×14 grid (inherent constraint).