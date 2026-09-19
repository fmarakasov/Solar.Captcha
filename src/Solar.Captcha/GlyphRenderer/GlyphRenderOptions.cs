using System;
using System.IO;

namespace Solar.Captcha.GlyphRenderer;

/// <summary>
/// Options for the font-based glyph renderer. Configured per renderer instance
/// via the Options pattern; the font is resolved from <see cref="FontPath"/>.
/// </summary>
public sealed class GlyphRenderOptions
{
    /// <summary>Default glyph width in pixels. Matches the static glyph format.</summary>
    public const int DefaultGlyphWidth = 8;

    /// <summary>Default glyph height in pixels. Matches the static glyph format.</summary>
    public const int DefaultGlyphHeight = 14;

    /// <summary>
    /// Gets or sets the path to the TrueType/OpenType font file (.ttf/.otf) used for rendering.
    /// </summary>
    public string FontPath { get; set; } = string.Empty;

    /// <summary>Gets or sets the glyph width in pixels. Must be 1..32. Defaults to 8.</summary>
    public int GlyphWidth { get; set; } = DefaultGlyphWidth;

    /// <summary>Gets or sets the glyph height in pixels. Must be 1..64. Defaults to 14.</summary>
    public int GlyphHeight { get; set; } = DefaultGlyphHeight;

    /// <summary>
    /// Validates that the options are usable: the font path is not empty and the file exists.
    /// </summary>
    /// <exception cref="ArgumentException">Thrown when options are invalid.</exception>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(FontPath))
        {
            throw new ArgumentException("FontPath must be set.", nameof(FontPath));
        }

        if (!File.Exists(FontPath))
        {
            throw new ArgumentException($"Font file does not exist: '{FontPath}'.", nameof(FontPath));
        }

        if (GlyphWidth is < 1 or > 32)
        {
            throw new ArgumentOutOfRangeException(nameof(GlyphWidth), GlyphWidth, "GlyphWidth must be between 1 and 32.");
        }

        if (GlyphHeight is < 1 or > 64)
        {
            throw new ArgumentOutOfRangeException(nameof(GlyphHeight), GlyphHeight, "GlyphHeight must be between 1 and 64.");
        }
    }
}