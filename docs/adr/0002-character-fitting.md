# ADR-002: Character Fitting

## Status

Accepted

## Context

Real fonts have variable aspect ratios. The glyph box is fixed at 8×14. When rendering a character, we must decide how to fit it into this constrained space.

## Decision

Characters are fitted into the 8×14 box as follows:

1. **Scale uniformly** to fit within 14 rows (height constraint is primary).
2. **Preserve aspect ratio** — do not stretch the character non-uniformly.
3. **Center horizontally** within the 8-column width; pad left/right with `0x00` as needed.
4. **Vertical alignment**: The character's baseline is aligned using the font's internal metrics to match the visual baseline of existing static glyphs.

## Consequences

### Positive

- Consistent visual appearance with existing static glyphs.
- No distortion of character shapes.
- Clear, deterministic algorithm.

### Negative

- Narrow characters (e.g., `i`, `l`) will have more horizontal padding than wide characters (e.g., `W`, `M`).
- Requires correct handling of font metrics to align baselines.