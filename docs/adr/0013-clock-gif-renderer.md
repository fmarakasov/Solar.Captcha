# ADR-0013: Self-Contained Animated Clock CAPTCHA Renderer and Font Resolution

## Status

Accepted

## Context

To make host applications (such as `Humanometr.Clock`) pure clients with minimal boilerplate, the composition logic for analog clock CAPTCHAs—including gradient background generation, noisy/circular dial outline calculation, 12-hour dash distribution, metric-centered numeral drawing, and multi-frame second-hand rotation—should reside in `Solar.Captcha`.

Furthermore, host environments need to discover TrueType font files from system directories across Windows (`C:\Windows\Fonts`) and Linux/container platforms (`/usr/share/fonts`, `fonts/`) by family name (`Arial`).

## Decision

1. **`ClockGifRenderer` and `ClockRenderOptions`**:
   Provide a high-level renderer in `Solar.Captcha.Raster` that accepts geometric, color, font, and time parameters and outputs valid 60-frame animated GIF streams.
2. **`RasterFontResolver`**:
   Provide a font discovery utility that inspects TrueType `name` tables in standard OS and application font directories to locate font files by family name, falling back to case-insensitive file name matching.
3. **Pure Client Architecture**:
   `Humanometr.Clock` removes all drawing, trigonometric calculations, and SixLabors dependencies, acting as a thin configuration and DI adapter over `ClockGifRenderer`.
