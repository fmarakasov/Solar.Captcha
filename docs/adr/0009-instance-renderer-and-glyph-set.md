# ADR-009: Instance Renderer with a Start-Up Glyph Set

## Status

Accepted

## Context

`CaptchaImageGenerator` was a `public static class` with a single `GetImage` method. It drew codes through `CaptchaImage`, which resolved each character through `CaptchaFont.GetGlyph(char)` — a static lookup into a hard-coded table that silently substituted a placeholder rectangle for anything unmapped and upper-cased anything mixed-case.

Three problems followed from that shape:

- **The glyph source was not a seam.** A font-based renderer had been built (`IGlyphRenderer`, `GlyphRenderResult`, `GlyphRenderOptions`) but nothing could inject it, because the image pipeline reached into `CaptchaFont` directly. The dynamic path was unreachable through the library's own registrations.
- **Failures were deferred and invisible.** A character with no glyph was drawn as a rectangle, discovered by a human looking at the image, on a request that had already succeeded. Nothing could fail early, because glyph resolution happened per request.
- **Randomness was a process-wide static.** `Random.Shared` was read directly, so a renderer could not be given a deterministic source.

A further constraint emerged while designing the fix: **the DI container cannot be used as the fail-fast point.** `ServiceProviderOptions.ValidateOnBuild` validates only the *shape* of the dependency graph. It never invokes factory delegates or constructors, so a glyph set produced by a factory is not built when the container is built. This was verified directly: a registered factory that throws is not called by `ValidateOnBuild`, while a constructor with an unregistered dependency does produce `AggregateException: Some services are not able to be constructed`.

## Decision

**The image renderer is an instance resolved from DI, and it is given a glyph set that is already complete.**

- `ICaptchaImageRenderer` is the public contract; `CaptchaImageRenderer` is the internal implementation. `CaptchaImageGenerator` and `CaptchaResult` are deleted — `Render` returns PNG bytes, and every member of `CaptchaResult` was either an echo of the caller's argument or unread.
- The renderer's collaborators are constructor parameters: a `GlyphSet` and a `System.Random`. The default registration supplies `Random.Shared`; the contract requires an injected `Random` to be thread-safe, because the renderer is a singleton.
- `GlyphSet` is a public, immutable, **total** map over a declared charset. Construction fails if any declared character has no glyph, naming every unresolved character. A constructed set is therefore a promise that everything it advertises is drawable.
- The set is materialised **once**, by running the registered `IGlyphRenderer` over the declared charset at application start-up. The renderer keeps its per-call contract; it is the *caller* that changed from per-request to once-at-startup.
- There is **no static instance and no default glyph source**. A glyph source is chosen explicitly (`AddFontGlyphSource` or `AddStaticGlyphSource`), registering the same interface, and registering two is an error rather than a silent precedence rule.
- **Start-up failure is the enforcement point.** `AddGlyphSet` registers `ValidateOnStart` for the glyph set options, and the validator builds the set. An application whose charset cannot be resolved refuses to start with an `OptionsValidationException` naming the characters. The same mechanism checks that every registered captcha flow's `Letters` is covered by the declared charset.

## Consequences

### Positive

- The glyph source is a real seam: font-based and static rendering are interchangeable registrations, both producing the same `GlyphSet`.
- An unusable font, an unrenderable charset, or a flow generating characters the application cannot draw is a **start-up error**, not a request that fails in front of a user.
- Rendering has no failure mode for characters it declared it would draw: the lookup cannot miss, so the drawing code has no fallback branch.
- Randomness is injectable, so tests can be deterministic where that buys evidence.
- The image pipeline no longer depends on a static table, so the two glyph sources are genuinely symmetric.

### Negative

- **Breaking change.** `CaptchaImageGenerator.GetImage` and `CaptchaResult` are gone; every caller must resolve `ICaptchaImageRenderer` instead. The captcha flows and their subclasses also take new constructor parameters.
- **A static entry point cannot coexist with an instance one.** A `public static class` and an instance `GetClass`-style member with the same name and parameters cannot both exist (`CS0111`), so the static API had to be removed rather than kept as a façade.
- `ValidateOnStart` fires at `IHost.StartAsync()`, **not** at `BuildServiceProvider()`. Consumers that never start a host — unit tests, custom hosts — must build the set explicitly by resolving `GlyphSet`. The guarantee is "the host refuses to start", not "the container refuses to build".
- No fallback is inferred: without a configured `FallbackGlyph`, an unresolvable character is a hard start-up failure. Applications that *want* a placeholder must say so.
- Choosing the charset is now the application's job, and it must cover every flow it registers. This is deliberate, but it is configuration a consumer can get wrong — which is why getting it wrong fails loudly at start-up.

## See Also

- [ADR-004: Per-Request Rendering](./0004-per-request-rendering.md) — the renderer's own contract, unchanged; only its caller changed.
- [ADR-005: Partial Success Model](./0005-partial-success-model.md) — failures are reported per character, and consumed once at start-up.
- [ADR-007: Whole-Call Failure Exception](./0007-failure-exception.md) — an unloadable font throws `GlyphRendererException`.
- [ADR-008: Static Glyph Path Retention](./0008-static-glyph-path.md) — how the static glyphs became one of two explicit sources.
