using System;
using System.IO;

namespace Solar.Captcha.GlyphRenderer;

/// <summary>
/// Options for the font-based glyph renderer. Configured per renderer instance
/// via the Options pattern; the font is resolved from <see cref="FontPath"/> or
/// <see cref="FontStreamFactory"/>.
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
    /// <remarks>
    /// Set either this or <see cref="FontStreamFactory"/>, not both.
    /// </remarks>
    public string FontPath { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a factory that opens the TrueType/OpenType font to render with, for fonts that
    /// are not a file on disk — an embedded resource, a byte array, a remote stream.
    /// </summary>
    /// <remarks>
    /// The factory is invoked once, when the renderer is constructed; the stream it returns is read
    /// to the end and disposed by the library, so the factory must return a fresh, readable stream.
    /// A delegate cannot come from configuration, so this source is code-only. Set either this or
    /// <see cref="FontPath"/>, not both.
    /// </remarks>
    public Func<Stream>? FontStreamFactory { get; set; }

    /// <summary>
    /// Validates that the options are usable: exactly one font source is set, and a font path
    /// points at an existing file.
    /// </summary>
    /// <exception cref="ArgumentException">Thrown when options are invalid.</exception>
    public void Validate()
    {
        var hasPath = !string.IsNullOrWhiteSpace(FontPath);

        if (hasPath && FontStreamFactory is not null)
        {
            throw new ArgumentException(
                "Set either FontPath or FontStreamFactory, not both.", nameof(FontPath));
        }

        if (!hasPath && FontStreamFactory is null)
        {
            throw new ArgumentException(
                "Set either FontPath or FontStreamFactory.", nameof(FontPath));
        }

        if (hasPath && !File.Exists(FontPath))
        {
            throw new ArgumentException($"Font file does not exist: '{FontPath}'.", nameof(FontPath));
        }
    }
}