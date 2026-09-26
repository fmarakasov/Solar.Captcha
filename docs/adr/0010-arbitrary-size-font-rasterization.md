# ADR-0010: Arbitrary-Size Font Rasterization and Package-Wide Non-Anti-Aliased Rendering

## Status

Accepted

## Context

The initial font rendering implementation (`GlyphRasterizer`) was hard-wired to generate fixed 8×14 bitmapped glyphs for letter CAPTCHAs (ADR-0001). However, rendering analog clock dial numerals requires rendering glyphs at variable sizes (e.g. 15px) while measuring character metrics and horizontal advances.

Duplicating TrueType outline decoding and contour flattening across different rasterizers would introduce maintenance overhead and divergent bug surfaces. Furthermore, introducing anti-aliased font rendering or sub-pixel smoothing would require complex alpha blending pipelines, third-party rasterization libraries, and non-deterministic pixel output.

## Decision

1. **Unified Outline Rasterizer**:
   Move TrueType outline flattening and ray-casting into a shared `OutlineRasterizer` in `Solar.Captcha.Fonts`. The rasterizer converts TrueType vector outlines into binary pixel coverage masks at arbitrary scales and bounding boxes.
2. **Specialized 8×14 Glyph Adapter**:
   Retain `GlyphRasterizer` under `Solar.Captcha.GlyphRenderer` as a thin adapter that computes the fixed 8×14 bounding box and baseline offsets, invokes `OutlineRasterizer.Fill`, and packs bits into `byte[14]`.
3. **General `RasterFont` Typography**:
   Introduce `RasterFont` under `Solar.Captcha.Raster` providing character measurement (`MeasureText`, `GetAdvance`, `GetAscender`, `GetDescender`) and arbitrary-size outline rendering.
4. **Package-Wide Non-Anti-Aliased Rendering**:
   Confirm that all rasterization across the library remains strictly binary (non-anti-aliased). Pixels are either covered (`1`) or uncovered (`0`), ensuring deterministic rendering and zero external dependencies.
