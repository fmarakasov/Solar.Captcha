using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Solar.Captcha.Tests.GlyphRenderer;

/// <summary>
/// Renders glyph bitmaps in the Solar.Captcha glyph format (one byte per row,
/// MSB = leftmost pixel) as ASCII pseudographics. A glyph becomes a small text
/// picture, so test output can be validated visually - a human can confirm that
/// the glyph for 'A' actually looks like an 'A'.
/// </summary>
internal static class GlyphAsciiArt
{
    /// <summary>The number of pixel columns encoded in each glyph byte.</summary>
    public const int Columns = 8;

    /// <summary>The default character used for a set pixel.</summary>
    public const char PixelOn = '#';

    /// <summary>The default character used for an unset pixel.</summary>
    public const char PixelOff = '.';

    /// <summary>
    /// Renders a single glyph as pseudographics: one text row per glyph row.
    /// Rows are separated by '\n' (not the platform newline) so the result is
    /// byte-for-byte identical on every platform and safe to assert on.
    /// </summary>
    /// <param name="glyph">The glyph bitmap: one byte per row, MSB = leftmost pixel.</param>
    /// <param name="pixelOn">The character used for a set pixel.</param>
    /// <param name="pixelOff">The character used for an unset pixel.</param>
    /// <returns>The glyph as an 8-column, <c>glyph.Length</c>-row text picture.</returns>
    public static string ToAsciiArt(byte[] glyph, char pixelOn = PixelOn, char pixelOff = PixelOff)
    {
        ArgumentNullException.ThrowIfNull(glyph);

        var builder = new StringBuilder(glyph.Length * (Columns + 1));
        for (int y = 0; y < glyph.Length; y++)
        {
            if (y > 0)
            {
                builder.Append('\n');
            }

            AppendRow(builder, glyph[y], pixelOn, pixelOff);
        }

        return builder.ToString();
    }

    /// <summary>
    /// Renders a glyph as pseudographics, preceded by a header naming the character.
    /// </summary>
    /// <param name="character">The character the glyph was rendered for.</param>
    /// <param name="glyph">The glyph bitmap.</param>
    /// <param name="pixelOn">The character used for a set pixel.</param>
    /// <param name="pixelOff">The character used for an unset pixel.</param>
    public static string ToAsciiArt(char character, byte[] glyph, char pixelOn = PixelOn, char pixelOff = PixelOff)
        => $"'{character}':\n{ToAsciiArt(glyph, pixelOn, pixelOff)}";

    /// <summary>
    /// Renders several glyphs as pseudographics, each preceded by a header naming
    /// its character and separated by a blank line.
    /// </summary>
    /// <param name="glyphs">The glyphs to render, keyed by character.</param>
    /// <param name="pixelOn">The character used for a set pixel.</param>
    /// <param name="pixelOff">The character used for an unset pixel.</param>
    public static string ToAsciiArt(
        IReadOnlyDictionary<char, byte[]> glyphs,
        char pixelOn = PixelOn,
        char pixelOff = PixelOff)
    {
        ArgumentNullException.ThrowIfNull(glyphs);

        var builder = new StringBuilder();
        // Order by character so the output does not depend on dictionary iteration order.
        foreach (var character in glyphs.Keys.OrderBy(c => c))
        {
            if (builder.Length > 0)
            {
                builder.Append("\n\n");
            }

            builder.Append(ToAsciiArt(character, glyphs[character], pixelOn, pixelOff));
        }

        return builder.ToString();
    }

    /// <summary>
    /// Renders several glyphs as pseudographics and writes them to the test output,
    /// so the rendered shapes can be inspected in the test results.
    /// </summary>
    /// <param name="glyphs">The glyphs to render, keyed by character.</param>
    public static void WriteToTestOutput(IReadOnlyDictionary<char, byte[]> glyphs)
        => TestContext.Out.WriteLine(ToAsciiArt(glyphs));

    /// <summary>
    /// Builds a single row of pseudographics from one glyph byte.
    /// </summary>
    private static void AppendRow(StringBuilder builder, byte row, char pixelOn, char pixelOff)
    {
        for (int x = 0; x < Columns; x++)
        {
            bool isSet = (row & (0x80 >> x)) != 0;
            builder.Append(isSet ? pixelOn : pixelOff);
        }
    }
}
