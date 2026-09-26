# ADR-0012: Streaming GIF89a Encoder with Exact Palette Construction and LZW Compression

## Status

Accepted

## Context

Clock CAPTCHA generation requires animated GIF output with 60 frames sweeping a second hand across a 60-second loop (`image/gif`). Holding 60 uncompressed RGBA framebuffers in memory simultaneously requires ~16 MB per request, causing memory bloat under load.

Additionally, GIF encoding requires indexing pixels into a color table of at most 256 colors and compressing the indices using variable-width LZW encoding.

## Decision

1. **Streaming GIF89a Encoding**:
   Introduce `GifEncoder` implementing a streaming pipeline (`Begin` → `WriteFrame` → `End`). Only a single active RGBA frame (~271 KB) is alive at any point during encoding.
2. **Exact Palette Construction with Uniform Fallback**:
   Construct the global color table (`GifPalette`) from the first frame plus explicitly seeded solid colors. If total unique colors exceed 256 (due to continuous gradients), apply uniform distance sampling across the color-sorted set to preserve gradient ramps while ensuring all seeded solid colors survive.
3. **In-House LZW Compression**:
   Implement `LzwEncoder` following standard GIF LZW specifications: initial Clear code, code-width expansion on power-of-two dictionary bounds, dictionary reset at 4096 entries, and length-prefixed sub-block chunking.
4. **Test-Only Verification Decoder**:
   Maintain an independent LZW and GIF parser (`GifReader` / `LzwDecoder`) in the test project to round-trip and verify encoded byte streams against regressions.
