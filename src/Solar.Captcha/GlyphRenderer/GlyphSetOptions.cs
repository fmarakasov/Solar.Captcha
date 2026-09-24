using System;

namespace Solar.Captcha.GlyphRenderer;

/// <summary>
/// Describes the glyph set an application needs: the charset it must be able to draw, and
/// optionally a glyph to substitute for characters that the glyph source cannot resolve.
/// </summary>
/// <remarks>
/// Configured through <c>AddGlyphSet</c>. There is no implicit default: an application must
/// declare the characters it intends to draw, and start-up fails if they cannot be resolved
/// (see ADR-008 and ADR-009).
/// </remarks>
public sealed class GlyphSetOptions
{
    /// <summary>
    /// Gets or sets the characters the application must be able to draw. Comparison is
    /// case-insensitive; the value is normalised to upper case.
    /// </summary>
    public string Charset { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets an optional glyph (one byte per row, <see cref="GlyphRenderOptions.GlyphHeight"/>
    /// rows) substituted for every character in <see cref="Charset"/> that the glyph source cannot
    /// resolve.
    /// </summary>
    /// <remarks>
    /// When <see langword="null"/>, an unresolvable character fails start-up instead of being
    /// drawn as something else. This is an explicit, application-level decision, deliberately
    /// not a behaviour baked into the glyph source.
    /// </remarks>
    public byte[]? FallbackGlyph { get; set; }

    /// <summary>
    /// Validates the option values.
    /// </summary>
    /// <exception cref="ArgumentException">Thrown when a value is unusable.</exception>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Charset))
        {
            throw new ArgumentException("Charset must be set.", nameof(Charset));
        }

        foreach (var character in Charset)
        {
            if (char.IsSurrogate(character))
            {
                throw new ArgumentException(
                    $"Charset contains the non-BMP character U+{(int)character:X4}. " +
                    "Glyphs are limited to single UTF-16 code units, so this character cannot be drawn.",
                    nameof(Charset));
            }
        }

        if (FallbackGlyph is not null && FallbackGlyph.Length != GlyphRenderOptions.GlyphHeight)
        {
            throw new ArgumentException(
                $"FallbackGlyph must be exactly {GlyphRenderOptions.GlyphHeight} bytes (one per glyph row), " +
                $"but was {FallbackGlyph.Length}.",
                nameof(FallbackGlyph));
        }
    }
}
