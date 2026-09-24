# Solar.Captcha Context

This document defines the domain model and vocabulary for the Solar.Captcha project.

## Glossary

| Term | Definition |
|------|------------|
| **Glyph** | A 14-byte array (`byte[14]`) representing an 8×14 bitmap. Each byte is a row (MSB = leftmost pixel; `0x00` = empty). |
| **Glyph Render** | The act of converting a character to a Glyph. |
| **Glyph Render Result** | A struct containing: (1) `IReadOnlyDictionary<char, Glyph> Glyphs` — successfully rendered, (2) `IReadOnlyDictionary<char, GlyphRenderFailureReason> Failures` — failed chars and reasons. |
| **Glyph Render Failure Reason** | An enum of failure types: `CharacterNotInFont`, `NonBmpCharacter`. Extensible. |
| **Glyph Source** | An `IGlyphRenderer` registered to supply glyphs for the application's declared charset: either the **Static Glyph Path** or the **Dynamic Glyph Path**. Exactly one is registered per application; registering two is a start-up error. |
| **Static Glyph Path** | The existing hard-coded `CaptchaFont.Glyphs` dictionary, registered via `AddStaticGlyphSource()`. Reports unmapped characters through `Failures` like any other Glyph Source — it does not substitute a placeholder. |
| **Dynamic Glyph Path** | The font-based renderer, configured via `GlyphRenderOptions` and implementing `IGlyphRenderer`, registered via `AddFontGlyphSource()`. |
| **Glyph Render Options** | Options class describing the font source: a file path (`FontPath`, configuration-bindable) or a stream factory (`FontStreamFactory`, code-only). Exactly one must be set. Validated at DI startup. |
| **Renderer Construction** | The **Glyph Renderer** (`IGlyphRenderer`) is constructed/injected once (per font), with font loaded from `GlyphRenderOptions`. |
| **Renderer Call** | `Render(string chars) → GlyphRenderResult` — the **Glyph Renderer**'s per-call contract: no caching, no font path. Called exactly once per application, by the **Glyph Set** builder, at start-up (see Renderer Construction vs. how often the call happens). |
| **Explicit Mode Selection** | The application chooses its **Glyph Source** once, at registration time (`AddStaticGlyphSource` or `AddFontGlyphSource`). There is no default; an application with a registered captcha flow but no Glyph Source fails to start. |
| **Glyph Pseudographics** | The ASCII-art rendering of a Glyph used in tests (`GlyphAsciiArt`): one text row per glyph row, `#` = set pixel, `.` = unset. Makes glyphs reviewable in test output. |
| **Required Charset** | The characters an application declares it must be able to draw, via `GlyphSetOptions.Charset`. Must cover the `Letters` of every registered captcha flow; a flow generating a character outside it is a start-up error. |
| **Glyph Set** | An immutable, **total** `char → Glyph` map over the Required Charset, built once at start-up by running the registered Glyph Source over the charset. Construction fails, naming every unresolved character, unless a Fallback Glyph is configured. Drawn from by the **Captcha Image Renderer**, never by looking up a Glyph Source per character. |
| **Fallback Glyph** | An optional Glyph, configured via `GlyphSetOptions.FallbackGlyph`, substituted for every character in the Required Charset that the Glyph Source could not resolve. An explicit, application-level decision — not a behavior any Glyph Source performs on its own. |
| **Captcha Image Renderer** | `ICaptchaImageRenderer` — draws a captcha code to a PNG image from a **Glyph Set** and an injected `System.Random`. Resolved from DI as a singleton; has no static entry point and no implicit default. |

## Implementation Approach

The dynamic glyph renderer is implemented in a **single phase** with **no external font-rendering dependencies**:

- **Lightweight in-repo TrueType/OpenType parser** reads font tables (`head`, `loca`, `glyf`, `hmtx`/`vmtx`, `cmap`) to rasterize glyphs to the 8×14 grid.
- **No SixLabors** or other external font libraries.
- Platform-agnostic: runs on Windows and Linux (Docker).

## See Also

- [ADR-001: Glyph Format](./docs/adr/0001-glyph-format.md)
- [ADR-002: Character Fitting](./docs/adr/0002-character-fitting.md)
- [ADR-003: Baseline Alignment](./docs/adr/0003-baseline-alignment.md)
- [ADR-004: Per-Request Rendering](./docs/adr/0004-per-request-rendering.md)
- [ADR-005: Partial Success Model](./docs/adr/0005-partial-success-model.md)
- [ADR-006: Non-BMP Character Handling](./docs/adr/0006-non-bmp-handling.md)
- [ADR-007: Whole-Call Failure Exception](./docs/adr/0007-failure-exception.md)
- [ADR-008: Static Glyph Path Retention](./docs/adr/0008-static-glyph-path.md)