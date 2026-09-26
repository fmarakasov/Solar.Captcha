# ADR-0011: Dependency-Free 2D Raster Canvas and Drawing Primitives

## Status

Accepted

## Context

Client projects such as `Humanometr` require 2D drawing capabilities (linear gradients, polygon strokes, lines with thickness, circular discs, text placement, and pixel compositing) to construct analog clock faces and visual challenges.

Historically, these operations relied on `SixLabors.ImageSharp` and `SixLabors.ImageSharp.Drawing`. Due to licensing changes, these dependencies are toxic and must be completely avoided.

## Decision

1. **`Solar.Captcha.Raster` Namespace**:
   Provide lightweight, dependency-free 2D graphics primitives:
   - `RasterColor`: 32-bit straight RGBA structure supporting hex parsing and color constants.
   - `Point` / `PointF`: In-house coordinate structures.
   - `RasterPen`: Stroke width and color description with round caps/joins modeled via disc stamping.
   - `LinearGradientBrush`: Two-point gradient interpolator with clamp, repeat, and reflect wrapping modes.
   - `RasterCanvas`: 2D RGBA frame buffer offering `Clear`, `SetPixel` (with alpha blending), `GetPixel`, `FillLinearGradient`, `FillCircle`, `DrawLine`, `StrokePolygon`, and `DrawText`.
2. **Decoupled Encoders**:
   `RasterCanvas` manages only pixel buffers. Format encoding is handled by decoupled static and streaming encoders (`PngEncoder`, `GifEncoder`).
