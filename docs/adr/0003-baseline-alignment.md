# ADR-003: Baseline Alignment

## Status

Accepted

## Context

Different fonts have different baseline positions. The existing static glyphs have a visual baseline that places uppercase letters (e.g., `A`, `H`) centered with minimal top/bottom padding. The dynamic renderer must align characters consistently with this baseline.

## Decision

Use the font's **vertical metrics** (`hhea` table: `ascender`, `descender`, `lineGap`) to compute the baseline position. The algorithm:

1. Calculate the font's baseline position relative to the glyph bounding box.
2. Align the character's glyph such that its baseline matches the target baseline.
3. If the font's metrics produce a visual mismatch with static glyphs, adjust by a configurable offset (default: 0).

## Consequences

### Positive

- Matches the visual appearance of existing static glyphs.
- Uses standard font table data (no custom hacks).
- Configurable for edge cases.

### Negative

- Requires parsing font metrics tables.
- Some fonts may still require manual offset tuning.