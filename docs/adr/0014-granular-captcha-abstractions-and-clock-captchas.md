# ADR-0014: Granular Captcha Abstractions and Clock Captcha Flows

## Status

Accepted

## Context

`Solar.Captcha` originally provided `ISessionBasedCaptcha` and `IStatelessCaptcha` with method signatures tightly coupled to fixed PNG letter captchas, where dimensions (`width`, `height`) and validation parameters (`ignoreCase`) were passed on each call.

With the addition of the animated clock challenge engine (`ClockGifRenderer` in `Solar.Captcha.Raster`), the library requires a unified, first-class architecture that supports both letter and clock captchas across session-based, stateless (Data Protection), and shared-key stateless flows.

Furthermore, dimension settings (`Width`, `Height`), case-sensitivity rules (`IgnoreCase`), and MIME content types (`ContentType`, e.g., `image/png` vs `image/gif`) belong to configuration options rather than per-request method parameters.

## Decision

1. **Granular and Generic Core Interfaces**:
   - Introduce `ISessionCaptcha` and `ISessionCaptcha<TOptions>` where `TOptions : class`.
   - Introduce `IStatelessCaptcha` and `IStatelessCaptcha<TOptions>` where `TOptions : class`.
   - Method signatures on `ISessionCaptcha` (`GenerateCaptchaImageBytes`, `GenerateCaptchaImageFileStream`, `Validate`) and `IStatelessCaptcha` (`GenerateCaptcha`, `Validate`) are parameter-free for dimensions, drawing their parameters directly from their bound options.

2. **Hierarchical Options Model**:
   - `CaptchaOptionsBase`: Common validation rules (`BlockedCodes`, `IgnoreCase`).
   - `SessionBasedImageCaptchaOptions`: Extends `CaptchaOptionsBase` with `Width`, `Height`, `SessionName`, and `ContentType`.
   - `StatelessImageCaptchaOptions`: Extends `CaptchaOptionsBase` with `Width`, `Height`, `TokenExpiration`, and `ContentType`.
   - Specialized option classes inherit for specific flows and renderers:
     - `BasicLetterSessionCaptchaOptions`, `ClockSessionCaptchaOptions`
     - `BasicLetterStatelessCaptchaOptions`, `ClockStatelessCaptchaOptions`
     - `SharedKeyStatelessLetterCaptchaOptions`, `SharedKeyStatelessClockCaptchaOptions`

3. **Clock Captcha Implementations**:
   - Clock captchas generate an animated GIF of an analog clock with a moving second hand and static hour/minute hands pointing to target time values.
   - The challenge code represents the answerable time in a standardized format (e.g., `HH:mm`).
   - Concrete implementations:
     - `SessionBasedClockCaptcha : SessionBasedCaptcha<ClockSessionCaptchaOptions>`
     - `StatelessClockCaptcha : StatelessCaptcha<ClockStatelessCaptchaOptions>`
     - `SharedKeyStatelessClockCaptcha : SharedKeyStatelessCaptcha<SharedKeyStatelessClockCaptchaOptions>`

4. **DI Registration Extensions**:
   - Provide dedicated, discoverable registration methods in `CaptchaServiceCollectionExtensions`:
     - `AddSessionBasedClockCaptcha`, `AddStatelessClockCaptcha`, `AddSharedKeyStatelessClockCaptcha`
     - `AddSessionBasedLetterCaptcha`, `AddStatelessLetterCaptcha`, `AddSharedKeyStatelessLetterCaptcha`
     - Clean aliases `AddSessionBasedCaptcha`, `AddStatelessCaptcha`, `AddSharedKeyStatelessCaptcha` defaulting to the letter-based flows.

5. **Generalized Image Renderer Seam (renderer-per-format behind a common base)**:
   - `ICaptchaImageRenderer` becomes the common base contract, carrying only what every format shares: the produced `ContentType` (`image/png`, `image/gif`).
   - Each format gets its own derived renderer contract with a content-shaped `Render`:
     - the existing glyph/letter renderer (PNG, glyph lookup, `CaptchaFontStyle`, `DrawLines`),
     - a clock renderer (animated GIF, hours/minutes, dial geometry, colours, font).
   - The byte-producing `Render` signature moves off the common base: no flow branches on challenge type to render, and no fake `captchaCode` parameter is forced onto a clock.
   - Renderer registration is decoupled from `AddGlyphSet`: a clock flow registers its renderer without requiring a Glyph Set, a Required Charset, or a glyph source. `AddGlyphSet` continues to register the letter renderer.
   - Captcha flows expose `ContentType` so consumers and middleware set the response MIME type from the flow rather than hardcoding `image/png`.

6. **Clock Code Format and Answer Normalization**:
   - The challenge code is the displayed time, canonically formatted as `H:mm` (hour without a leading zero, two-digit minute; e.g. `3:45`).
   - Hours are drawn from `1..12`; minutes advance in a configurable `MinuteStep` whose default is 5, because the dial's readability resolution is the hour dash spacing.
   - Validation normalizes the submitted answer rather than demanding one spelling: surrounding whitespace is trimmed, hour/minute separators (`:`, `.`, `-`, space) are treated as equivalent, leading zeros on the hour are ignored, and the result must parse as an hour in `1..12` with a minute in `0..59` that is a multiple of `MinuteStep`. So `3:45`, `03.45`, `345`, and `3 45` all validate against `3:45`.
   - Time selection uses a cryptographically secure generator, matching the letter flows; `ClockGifRenderer`'s injectable `Random` remains confined to visual noise.
