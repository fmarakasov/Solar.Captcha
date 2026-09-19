using System;
using System.IO;

namespace Solar.Captcha.GlyphRenderer;

/// <summary>
/// Options for the font-based glyph renderer. Configured per renderer instance
/// via the Options pattern; the font is resolved from <see cref="FontPath"/>.
/// </summary>
/// <remarks>
/// The glyph grid is fixed at 8×14 pixels by the Solar.Captcha glyph format (ADR-001):
/// each glyph is exactly <c>byte[14]</c> with one byte per row and 8 bits per row, which is
/// what the static glyph path and the image pipeline consume. The size is therefore not
/// configurable — a wider grid would be dropped by the single-byte row packing, and a
/// different height would produce a bitmap the pipeline cannot index.
/// </remarks>
public sealed class GlyphRenderOptions
{
    /// <summary>Glyph width in pixels. Fixed at 8 by the Solar.Captcha glyph format (ADR-001).</summary>
    public const int GlyphWidth = CaptchaFont.GlyphWidth;

    /// <summary>Glyph height in pixels. Fixed at 14 by the Solar.Captcha glyph format (ADR-001).</summary>
    public const int GlyphHeight = CaptchaFont.GlyphHeight;

    /// <summary>
    /// Gets or sets the path to the TrueType/OpenType font file (.ttf/.otf) used for rendering.
    /// </summary>
    public string FontPath { get; set; } = string.Empty;

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
    }
}