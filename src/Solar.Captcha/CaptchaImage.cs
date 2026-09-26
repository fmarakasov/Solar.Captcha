using System;

using Solar.Captcha.Raster;

namespace Solar.Captcha;

internal sealed class CaptchaImage : IDisposable
{
    public int Width { get; }
    public int Height { get; }

    // RGBA pixel data: [y * Width * 4 + x * 4 + channel]
    private readonly byte[] _pixels;

    public CaptchaImage(int width, int height)
    {
        Width = width;
        Height = height;
        _pixels = new byte[width * height * 4];
    }

    public void Fill(byte r, byte g, byte b)
    {
        for (var i = 0; i < _pixels.Length; i += 4)
        {
            _pixels[i] = r;
            _pixels[i + 1] = g;
            _pixels[i + 2] = b;
            _pixels[i + 3] = 255;
        }
    }

    public void SetPixel(int x, int y, byte r, byte g, byte b, byte a = 255)
    {
        if (x < 0 || x >= Width || y < 0 || y >= Height) return;
        var offset = (y * Width + x) * 4;
        if (a == 255)
        {
            _pixels[offset] = r;
            _pixels[offset + 1] = g;
            _pixels[offset + 2] = b;
            _pixels[offset + 3] = 255;
        }
        else if (a > 0)
        {
            // Alpha blending
            float sa = a / 255f;
            float da = 1f - sa;
            _pixels[offset] = (byte)(_pixels[offset] * da + r * sa);
            _pixels[offset + 1] = (byte)(_pixels[offset + 1] * da + g * sa);
            _pixels[offset + 2] = (byte)(_pixels[offset + 2] * da + b * sa);
            _pixels[offset + 3] = 255;
        }
    }

    public void DrawLine(float x0, float y0, float x1, float y1, byte r, byte g, byte b, float thickness = 1f)
    {
        // Bresenham's line with thickness approximation
        var dx = Math.Abs(x1 - x0);
        var dy = Math.Abs(y1 - y0);
        var steps = (int)Math.Max(dx, dy) + 1;

        for (var i = 0; i <= steps; i++)
        {
            var t = steps == 0 ? 0f : (float)i / steps;
            var px = x0 + (x1 - x0) * t;
            var py = y0 + (y1 - y0) * t;

            if (thickness <= 1.5f)
            {
                SetPixel((int)px, (int)py, r, g, b);
            }
            else
            {
                var half = (int)(thickness / 2);
                for (var ox = -half; ox <= half; ox++)
                {
                    for (var oy = -half; oy <= half; oy++)
                    {
                        SetPixel((int)px + ox, (int)py + oy, r, g, b);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Draws a glyph bitmap at a position, scaled, styled and rotated.
    /// </summary>
    public void DrawGlyph(byte[] glyph, int destX, int destY, int scale, byte r, byte g, byte b,
        CaptchaFontStyle fontStyle, float rotationDegrees)
    {
        var scaledW = CaptchaFont.GlyphWidth * scale;
        var scaledH = CaptchaFont.GlyphHeight * scale;
        var cx = scaledW / 2f;
        var cy = scaledH / 2f;

        var rad = rotationDegrees * MathF.PI / 180f;
        var cos = MathF.Cos(rad);
        var sin = MathF.Sin(rad);

        // Italic shear factor
        float shear = fontStyle == CaptchaFontStyle.Italic ? 0.2f : 0f;

        // Bold: draw each pixel slightly wider
        var boldExtra = fontStyle == CaptchaFontStyle.Bold ? 1 : 0;

        // Determine bounding box of rotated character to iterate over
        var boundPadding = (int)(Math.Max(scaledW, scaledH) * 0.5f) + 2;
        var minX = -boundPadding;
        var minY = -boundPadding;
        var maxX = scaledW + boundPadding;
        var maxY = scaledH + boundPadding;

        for (var dy = minY; dy < maxY; dy++)
        {
            for (var dx = minX; dx < maxX; dx++)
            {
                // Inverse rotation to find source pixel
                var rx = dx - cx;
                var ry = dy - cy;
                var srcX = rx * cos + ry * sin + cx;
                var srcY = -rx * sin + ry * cos + cy;

                // Apply inverse italic shear
                srcX -= srcY * shear;

                // Map to glyph coordinates
                var glyphX = (int)(srcX / scale);
                var glyphY = (int)(srcY / scale);

                var isSet = CaptchaFont.IsPixelSet(glyph, glyphX, glyphY);

                // Bold: also check adjacent pixel
                if (!isSet && boldExtra > 0 && glyphX > 0)
                {
                    isSet = CaptchaFont.IsPixelSet(glyph, glyphX - 1, glyphY);
                }

                if (isSet)
                {
                    var finalX = destX + dx;
                    var finalY = destY + dy;
                    SetPixel(finalX, finalY, r, g, b);
                }
            }
        }
    }

    /// <summary>
    /// Measures the horizontal space a glyph occupies at a given scale and style.
    /// </summary>
    public int MeasureCharWidth(int scale, CaptchaFontStyle fontStyle)
    {
        var width = CaptchaFont.GlyphWidth * scale;
        if (fontStyle == CaptchaFontStyle.Bold)
        {
            width += scale;
        }

        // Account for italic shear: horizontal offset proportional to glyph height.
        // This mirrors the shear used in DrawGlyph (srcX -= srcY * shear).
        float shear = 0f;
        if (fontStyle == CaptchaFontStyle.Italic)
        {
            // Use the same shear factor as in DrawGlyph for italic rendering.
            shear = 0.3f;
        }

        if (shear != 0f)
        {
            // Maximum additional width is shear * glyph height * scale.
            var italicExtra = (int)Math.Ceiling(Math.Abs(shear) * CaptchaFont.GlyphHeight * scale);
            width += italicExtra;
        }

        return width;
    }

    public int MeasureCharHeight(int scale)
    {
        return CaptchaFont.GlyphHeight * scale;
    }

    /// <summary>
    /// Encodes the current pixel buffer as a PNG image.
    /// </summary>
    /// <returns>The PNG bytes.</returns>
    public byte[] EncodePng() => PngEncoder.Encode(_pixels, Width, Height);

    public void Dispose()
    {
        // No unmanaged resources, but implements IDisposable for using pattern compatibility
    }
}
