using System;
using System.IO;
using Solar.Captcha.Fonts;

namespace Solar.Captcha.Raster;

/// <summary>
/// A TrueType font that can be rasterized at any pixel size, for drawing text on a
/// <see cref="RasterCanvas"/>.
/// </summary>
/// <remarks>
/// The font is parsed once, when the instance is created, and every render call works from the
/// parsed representation (ADR-004). Glyphs are fitted to the requested size by scaling on the
/// font's em square, so a family renders consistently at any size. Rendering is not cached:
/// the same character at the same size produces the same mask every time (ADR-0010).
/// </remarks>
public sealed class RasterFont
{
    private readonly TrueTypeFont _font;

    private RasterFont(TrueTypeFont font) => _font = font;

    /// <summary>Gets the font family name as recorded in the font's <c>name</c> table, if present.</summary>
    public string? FamilyName => _font.FamilyName;

    /// <summary>Gets the font's design units per em, the basis for every size calculation.</summary>
    public int UnitsPerEm => _font.UnitsPerEm;

    /// <summary>
    /// Loads a font from a file on disk.
    /// </summary>
    /// <param name="fontPath">The path to a TrueType or OpenType font file.</param>
    /// <returns>The parsed font.</returns>
    /// <exception cref="ArgumentNullException">When <paramref name="fontPath"/> is <see langword="null"/>.</exception>
    /// <exception cref="GlyphRenderer.GlyphRendererException">
    /// When the file cannot be read or is not a font this library supports.
    /// </exception>
    public static RasterFont FromFile(string fontPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fontPath);
        return new RasterFont(TrueTypeFont.Load(fontPath));
    }

    /// <summary>
    /// Loads a font from a stream factory, for fonts that are not a file on disk — an embedded
    /// resource, a byte array, a remote stream.
    /// </summary>
    /// <param name="streamFactory">
    /// Opens the font stream. Invoked exactly once, when this method is called; the stream it
    /// returns is read to the end and disposed by this method, so the factory must return a
    /// fresh, readable stream each time.
    /// </param>
    /// <returns>The parsed font.</returns>
    /// <exception cref="ArgumentNullException">When <paramref name="streamFactory"/> is <see langword="null"/>.</exception>
    /// <exception cref="GlyphRenderer.GlyphRendererException">
    /// When the stream cannot be read or is not a font this library supports.
    /// </exception>
    public static RasterFont FromStream(Func<Stream> streamFactory)
    {
        ArgumentNullException.ThrowIfNull(streamFactory);

        using var stream = streamFactory();
        return new RasterFont(TrueTypeFont.Load(stream, "stream"));
    }

    /// <summary>
    /// Measures the height of one line of text: the distance between consecutive baselines.
    /// </summary>
    /// <param name="pixelSize">The em size in pixels.</param>
    /// <returns>The line height in pixels.</returns>
    public float GetLineHeight(float pixelSize) => (GetAscender(pixelSize) - GetDescender(pixelSize));

    /// <summary>Gets the distance from the baseline to the top of the text, in pixels.</summary>
    /// <param name="pixelSize">The em size in pixels.</param>
    /// <returns>The ascender in pixels.</returns>
    public float GetAscender(float pixelSize) => Scale(pixelSize) * _font.Ascender;

    /// <summary>Gets the distance from the baseline to the bottom of the text, in pixels.</summary>
    /// <param name="pixelSize">The em size in pixels.</param>
    /// <returns>The descender in pixels; negative when it sits below the baseline.</returns>
    public float GetDescender(float pixelSize) => Scale(pixelSize) * _font.Descender;

    /// <summary>
    /// Measures the horizontal extent of a string.
    /// </summary>
    /// <param name="text">The text to measure.</param>
    /// <param name="pixelSize">The em size in pixels.</param>
    /// <returns>The advance width in pixels. Characters the font cannot map contribute nothing.</returns>
    /// <exception cref="ArgumentNullException">When <paramref name="text"/> is <see langword="null"/>.</exception>
    public float MeasureText(string text, float pixelSize)
    {
        ArgumentNullException.ThrowIfNull(text);

        var advance = 0f;
        foreach (var character in text)
        {
            advance += GetAdvance(character, pixelSize);
        }

        return advance;
    }

    /// <summary>
    /// Gets the horizontal advance of one character, in pixels.
    /// </summary>
    /// <param name="character">The character to measure.</param>
    /// <param name="pixelSize">The em size in pixels.</param>
    /// <returns>The advance in pixels, or 0 when the font has no mapping for the character.</returns>
    public float GetAdvance(char character, float pixelSize)
    {
        var glyphIndex = _font.GetGlyphIndex(character);
        if (glyphIndex == 0)
        {
            return 0f;
        }

        return Scale(pixelSize) * _font.GetAdvanceWidth(glyphIndex);
    }

    /// <summary>
    /// Rasterizes one character into a coverage mask positioned relative to a pen on the baseline.
    /// </summary>
    /// <param name="character">The character to rasterize.</param>
    /// <param name="pixelSize">The em size in pixels.</param>
    /// <param name="placement">Receives the mask and its pen-relative position.</param>
    /// <returns>
    /// <see langword="true"/> when the font has an outline for the character; <see langword="false"/>
    /// when it has no mapping or no <c>glyf</c> outline (as is the case for a space).
    /// </returns>
    internal bool TryGetPlacement(char character, float pixelSize, out GlyphPlacement placement)
    {
        placement = default;

        var glyphIndex = _font.GetGlyphIndex(character);
        if (glyphIndex == 0)
        {
            return false;
        }

        var outline = _font.GetOutline(glyphIndex);
        if (outline is null || outline.PointCount == 0)
        {
            return false;
        }

        var (minX, minY, maxX, maxY) = OutlineRasterizer.Bounds(outline);
        if (maxX <= minX || maxY <= minY)
        {
            return false;
        }

        var scale = Scale(pixelSize);

        var width = Math.Max(1, (int)MathF.Ceiling((maxX - minX) * scale));
        var height = Math.Max(1, (int)MathF.Ceiling((maxY - minY) * scale));

        // Place the mask's own pixel grid so that the outline's left-most point lands on column 0
        // and the baseline sits `ascender`-worth below the top of the mask's font-space box.
        var baselineRow = (int)MathF.Round(maxY * scale);

        var coverage = new byte[width * height];
        OutlineRasterizer.Fill(coverage, width, height, outline, scale, xOffset: 0, baselineY: baselineRow);

        placement = new GlyphPlacement
        {
            Coverage = coverage,
            Width = width,
            Height = height,
            OffsetX = (int)MathF.Round(minX * scale),
            OffsetY = -baselineRow,
            Advance = scale * _font.GetAdvanceWidth(glyphIndex),
        };

        return true;
    }

    private float Scale(float pixelSize) => pixelSize / _font.UnitsPerEm;
}
