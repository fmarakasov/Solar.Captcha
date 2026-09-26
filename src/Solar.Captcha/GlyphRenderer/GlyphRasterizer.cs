using System;
using Solar.Captcha.Fonts;

namespace Solar.Captcha.GlyphRenderer;

/// <summary>
/// Rasterizes a TrueType glyph outline into the fixed Solar.Captcha glyph bitmap:
/// 8 pixels wide, 14 tall, one byte per row, MSB of each byte = leftmost pixel.
/// </summary>
/// <remarks>
/// This is the 8×14 specialisation of <see cref="OutlineRasterizer"/>. It computes the fit
/// (uniform scale, preserved aspect ratio, horizontal centering, baseline on the font ascender —
/// see ADR-002/003), delegates the contour fill to the shared rasterizer, and packs the resulting
/// coverage mask into the glyph bit format (ADR-001). Keeping the fit here rather than in the
/// rasterizer is what allows the same fill to serve arbitrary pixel grids without changing a
/// single bit of the glyph output.
/// </remarks>
internal static class GlyphRasterizer
{
    /// <summary>
    /// Rasterizes the outline into the fixed Solar.Captcha glyph grid.
    /// </summary>
    /// <param name="outline">The glyph outline, in font units.</param>
    /// <param name="font">The font the outline came from, used for its vertical metrics.</param>
    /// <returns>
    /// Fourteen bytes: one byte per pixel row, MSB = leftmost pixel. An empty or degenerate
    /// outline yields fourteen zero bytes rather than throwing.
    /// </returns>
    public static byte[] Rasterize(GlyphOutline outline, TrueTypeFont font)
    {
        ArgumentNullException.ThrowIfNull(outline);
        ArgumentNullException.ThrowIfNull(font);

        const int width = CaptchaFont.GlyphWidth;
        const int height = CaptchaFont.GlyphHeight;

        var result = new byte[height];
        if (outline.PointCount == 0)
        {
            return result;
        }

        var (minX, minY, maxX, maxY) = OutlineRasterizer.Bounds(outline);
        if (maxX <= minX || maxY <= minY)
        {
            return result;
        }

        var ascender = font.Ascender;
        var emHeight = ascender - font.Descender;
        if (emHeight <= 0)
        {
            emHeight = maxY - minY;
            ascender = maxY;
        }

        var scale = Math.Min((float)height / emHeight, (float)width / (maxX - minX));
        if (scale <= 0f)
        {
            return result;
        }

        var baselineY = (int)MathF.Round(scale * ascender);
        var glyphPixelWidth = scale * (maxX - minX);
        var xOffset = (int)MathF.Round((width - glyphPixelWidth) / 2f);

        var coverage = new byte[width * height];
        OutlineRasterizer.Fill(coverage, width, height, outline, scale, xOffset, baselineY);

        for (var py = 0; py < height; py++)
        {
            byte row = 0;
            for (var px = 0; px < width; px++)
            {
                if (coverage[(py * width) + px] != 0)
                {
                    row |= (byte)(0x80 >> px);
                }
            }

            result[py] = row;
        }

        return result;
    }
}
