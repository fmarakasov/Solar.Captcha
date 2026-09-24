using System;
using System.Collections.Generic;
using System.Threading;

namespace Solar.Captcha.GlyphRenderer;

/// <summary>
/// Builds the application's <see cref="GlyphSet"/> once, by running the registered
/// <see cref="IGlyphRenderer"/> over the declared charset.
/// </summary>
/// <remarks>
/// The factory is the single point where the glyph source is executed, so it is the single point
/// where an unresolvable character can be dealt with: substituted with the configured fallback
/// glyph, or turned into a failure. <see cref="GlyphSetStartupValidator"/> drives it during host
/// start-up so that a failure refuses to start the application rather than surfacing on the first
/// captcha request.
/// </remarks>
internal sealed class GlyphSetFactory(IGlyphRenderer renderer)
{
    private readonly IGlyphRenderer _renderer = renderer ?? throw new ArgumentNullException(nameof(renderer));
    private readonly Lock _gate = new();

    private GlyphSet? _glyphSet;

    /// <summary>
    /// Gets the glyph set, building it on first use.
    /// </summary>
    /// <param name="options">The declared charset and optional fallback glyph.</param>
    /// <returns>The glyph set.</returns>
    /// <exception cref="GlyphRendererException">
    /// Thrown when a character in the charset cannot be resolved and no fallback glyph is configured.
    /// </exception>
    public GlyphSet Get(GlyphSetOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (_glyphSet is { } existing)
        {
            return existing;
        }

        lock (_gate)
        {
            return _glyphSet ??= Build(options);
        }
    }

    private GlyphSet Build(GlyphSetOptions options)
    {
        options.Validate();

        var charset = GlyphSet.NormalizeCharset(options.Charset);
        var rendered = _renderer.Render(charset);

        var glyphs = new Dictionary<char, byte[]>(rendered.Glyphs);
        if (options.FallbackGlyph is { } fallback)
        {
            foreach (var character in rendered.Failures.Keys)
            {
                glyphs[character] = fallback;
            }
        }

        // GlyphSet rejects any character still without a glyph, naming them all.
        return new GlyphSet(charset, glyphs);
    }
}
