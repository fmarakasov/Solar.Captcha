using System;
using System.Collections.Generic;
using Solar.Captcha.Fonts;

namespace Solar.Captcha.GlyphRenderer;

/// <summary>
/// A TrueType-based glyph renderer that produces 8×14 bitmaps matching the
/// Solar.Captcha static glyph format. Characters are fitted to the glyph box
/// per ADR-002/003: scale to fit, preserve aspect, center horizontally,
/// align baseline using font metrics.
/// </summary>
internal sealed class GlyphRenderer : IGlyphRenderer
{
    private readonly TrueTypeFont _font;

    /// <summary>
    /// Creates a renderer from pre-validated options.
    /// </summary>
    /// <param name="options">Pre-validated options containing the font source (path or stream factory).</param>
    /// <exception cref="GlyphRendererException">Thrown if the font cannot be loaded.</exception>
    public GlyphRenderer(GlyphRenderOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        try
        {
            _font = LoadFont(options);
        }
        catch (Exception ex) when (ex is GlyphRendererException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new GlyphRendererException($"Failed to load font from {DescribeSource(options)}.", ex);
        }
    }

    private static TrueTypeFont LoadFont(GlyphRenderOptions options)
    {
        if (options.FontStreamFactory is { } streamFactory)
        {
            // The factory creates the stream; the renderer owns and disposes it.
            using var stream = streamFactory();
            return TrueTypeFont.Load(stream, "stream");
        }

        return TrueTypeFont.Load(options.FontPath);
    }

    private static string DescribeSource(GlyphRenderOptions options) =>
        options.FontStreamFactory is not null ? "the configured stream" : $"'{options.FontPath}'";

    /// <inheritdoc />
    public GlyphRenderResult Render(string characters)
    {
        if (string.IsNullOrEmpty(characters))
        {
            return new GlyphRenderResult(
                new Dictionary<char, byte[]>(),
                new Dictionary<char, GlyphRenderFailureReason>());
        }

        var glyphs = new Dictionary<char, byte[]>();
        var failures = new Dictionary<char, GlyphRenderFailureReason>();

        // De-duplicate input characters.
        var seen = new HashSet<char>();
        foreach (char c in characters)
        {
            if (!seen.Add(c))
            {
                continue;
            }

            // Skip non-BMP (surrogate pairs appear as two chars; only the first would be high surrogate).
            if (char.IsSurrogate(c))
            {
                failures[c] = GlyphRenderFailureReason.NonBmpCharacter;
                continue;
            }

            // Glyph index 0 is the TrueType '.notdef' glyph: GetGlyphIndex returns it for a
            // character with no cmap entry, so 0 means "not in font" rather than a successful
            // (blank) render.
            int glyphIndex = _font.GetGlyphIndex(c);
            if (glyphIndex <= 0)
            {
                failures[c] = GlyphRenderFailureReason.CharacterNotInFont;
                continue;
            }

            var outline = _font.GetOutline(glyphIndex);
            if (outline is null)
            {
                failures[c] = GlyphRenderFailureReason.CharacterNotInFont;
                continue;
            }

            try
            {
                byte[] glyph = GlyphRasterizer.Rasterize(outline, _font);
                glyphs[c] = glyph;
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or IndexOutOfRangeException)
            {
                failures[c] = GlyphRenderFailureReason.CharacterNotInFont;
            }
        }

        return new GlyphRenderResult(glyphs, failures);
    }
}