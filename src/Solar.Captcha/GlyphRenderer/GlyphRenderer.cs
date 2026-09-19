using System;
using System.Collections.Generic;

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
    private readonly GlyphRenderOptions _options;

    /// <summary>
    /// Creates a renderer from pre-validated options.
    /// </summary>
    /// <param name="options">Pre-validated options containing the font path.</param>
    /// <exception cref="GlyphRendererException">Thrown if the font cannot be loaded.</exception>
    public GlyphRenderer(GlyphRenderOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        try
        {
            _font = TrueTypeFont.Load(options.FontPath);
        }
        catch (Exception ex) when (ex is GlyphRendererException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new GlyphRendererException($"Failed to load font from '{options.FontPath}'.", ex);
        }
    }

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

            int glyphIndex = _font.GetGlyphIndex(c);
            if (glyphIndex < 0)
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
                byte[] glyph = GlyphRasterizer.Rasterize(outline, _font, _options.GlyphWidth, _options.GlyphHeight);
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