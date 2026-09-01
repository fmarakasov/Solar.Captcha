# Solar.Captcha Context

This document defines the domain model and vocabulary for the Solar.Captcha project.

## Glossary

| Term | Definition |
|------|------------|
| **Glyph** | A 14-byte array (`byte[14]`) representing an 8×14 bitmap. Each byte is a row (MSB = leftmost pixel; `0x00` = empty). |
| **Glyph Render** | The act of converting a character to a Glyph. |
| **Glyph Render Result** | A struct containing: (1) `IReadOnlyDictionary<char, Glyph> Glyphs` — successfully rendered, (2) `IReadOnlyDictionary<char, GlyphRenderFailureReason> Failures` — failed chars and reasons. |
| **Glyph Render Failure Reason** | An enum of failure types: `CharacterNotInFont`, `NonBmpCharacter`. Extensible. |
| **Static Glyph Path** | The existing hard-coded `CaptchaFont.Glyphs` dictionary, retained for backward compatibility. |
| **Dynamic Glyph Path** | The new font-based renderer, configured via `GlyphRenderOptions` and implementing `IGlyphRenderer`. |
| **Glyph Render Options** | Options class bound from configuration, containing the font file path. Validated at DI startup. |
| **Renderer Construction** | The renderer is constructed/injected once (per font), with font loaded from `GlyphRenderOptions`. |
| **Renderer Call** | `Render(string chars) → GlyphRenderResult` — per-request, no caching, no font path. |
| **Explicit Mode Selection** | The caller chooses the renderer implementation at call time (DI resolves either static or dynamic). |

## See Also

- [ADR-001: Glyph Format](./docs/adr/0001-glyph-format.md)
- [ADR-002: Character Fitting](./docs/adr/0002-character-fitting.md)
- [ADR-003: Baseline Alignment](./docs/adr/0003-baseline-alignment.md)
- [ADR-004: Per-Request Rendering](./docs/adr/0004-per-request-rendering.md)
- [ADR-005: Partial Success Model](./docs/adr/0005-partial-success-model.md)
- [ADR-006: Non-BMP Character Handling](./docs/adr/0006-non-bmp-handling.md)
- [ADR-007: Whole-Call Failure Exception](./docs/adr/0007-failure-exception.md)
- [ADR-008: Static Glyph Path Retention](./docs/adr/0008-static-glyph-path.md)