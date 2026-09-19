using System;
using System.Collections.Generic;

namespace Solar.Captcha.GlyphRenderer;

/// <summary>
/// Rasterizes a TrueType glyph outline into a 1-bit glyph bitmap
/// (a row-major array of bytes; MSB of each byte = leftmost pixel).
/// </summary>
internal static class GlyphRasterizer
{
    /// <summary>
    /// Rasterizes the outline at the given pixel size using uniform scaling that
    /// preserves aspect ratio, horizontal centering, baseline alignment, and
    /// even-odd fill of the flattened outline (see ADR-002/003).
    /// </summary>
    public static byte[] Rasterize(GlyphOutline outline, TrueTypeFont font, int width, int height)
    {
        var result = new byte[height];
        if (outline.PointCount == 0 || width <= 0 || height <= 0)
        {
            return result;
        }

        int gxMin = int.MaxValue, gyMin = int.MaxValue, gxMax = int.MinValue, gyMax = int.MinValue;
        for (int i = 0; i < outline.PointCount; i++)
        {
            gxMin = Math.Min(gxMin, outline.Xs[i]);
            gyMin = Math.Min(gyMin, outline.Ys[i]);
            gxMax = Math.Max(gxMax, outline.Xs[i]);
            gyMax = Math.Max(gyMax, outline.Ys[i]);
        }

        if (gxMax <= gxMin || gyMax <= gyMin)
        {
            return result;
        }

        int ascender = font.Ascender;
        int descender = font.Descender;
        int emHeight = ascender - descender;
        if (emHeight <= 0)
        {
            emHeight = gyMax - gyMin;
            ascender = gyMax;
        }

        float scale = Math.Min((float)height / emHeight, (float)width / (gxMax - gxMin));
        if (scale <= 0f)
        {
            return result;
        }

        int baselineY = (int)MathF.Round(scale * ascender);
        float glyphPixelWidth = scale * (gxMax - gxMin);
        int xOffset = (int)MathF.Round((width - glyphPixelWidth) / 2f);

        var segments = FlattenContours(outline);

        for (int py = 0; py < height; py++)
        {
            byte row = 0;
            for (int px = 0; px < width; px++)
            {
                float fx = gxMin + (px + 0.5f - xOffset) / scale;
                float fy = (baselineY - (py + 0.5f)) / scale;

                if (IsInside(fx, fy, segments))
                {
                    row |= (byte)(0x80 >> px);
                }
            }

            result[py] = row;
        }

        return result;
    }

    private static List<(float X1, float Y1, float X2, float Y2)> FlattenContours(GlyphOutline outline)
    {
        var segments = new List<(float, float, float, float)>();
        int contourStart = 0;
        foreach (var endPt in outline.EndPoints)
        {
            FlattenContour(outline, contourStart, endPt, segments);
            contourStart = endPt + 1;
        }

        return segments;
    }

    private static void FlattenContour(GlyphOutline outline, int start, int end, List<(float, float, float, float)> segments)
    {
        var xs = outline.Xs;
        var ys = outline.Ys;
        var onCurve = outline.OnCurve;

        float startX = xs[start];
        float startY = ys[start];
        if (!onCurve[start] && onCurve[end])
        {
            startX = xs[end];
            startY = ys[end];
        }
        else if (!onCurve[start] && !onCurve[end])
        {
            startX = (xs[start] + xs[end]) / 2f;
            startY = (ys[start] + ys[end]) / 2f;
        }

        float prevX = startX;
        float prevY = startY;

        for (int idx = start; idx <= end; idx++)
        {
            if (onCurve[idx])
            {
                if (idx > start || onCurve[start])
                {
                    EmitLine(segments, prevX, prevY, xs[idx], ys[idx]);
                }
                prevX = xs[idx];
                prevY = ys[idx];
            }
            else
            {
                int nextIdx = (idx == end) ? start : idx + 1;
                float endX, endY;

                if (onCurve[nextIdx])
                {
                    endX = xs[nextIdx];
                    endY = ys[nextIdx];
                    EmitQuadratic(segments, prevX, prevY, xs[idx], ys[idx], endX, endY);
                    prevX = endX;
                    prevY = endY;
                }
                else
                {
                    endX = (xs[idx] + xs[nextIdx]) / 2f;
                    endY = (ys[idx] + ys[nextIdx]) / 2f;
                    EmitQuadratic(segments, prevX, prevY, xs[idx], ys[idx], endX, endY);
                    prevX = endX;
                    prevY = endY;
                }
            }
        }

        EmitLine(segments, prevX, prevY, startX, startY);
    }

    private static void EmitLine(List<(float, float, float, float)> segments, float x1, float y1, float x2, float y2)
    {
        if (MathF.Abs(x2 - x1) < 1e-4f && MathF.Abs(y2 - y1) < 1e-4f)
        {
            return;
        }

        segments.Add((x1, y1, x2, y2));
    }

    private static void EmitQuadratic(List<(float, float, float, float)> segments, float x0, float y0, float cx, float cy, float x1, float y1)
    {
        const int subdivisions = 8;
        for (int s = 0; s < subdivisions; s++)
        {
            float t0 = s / (float)subdivisions;
            float t1 = (s + 1) / (float)subdivisions;
            float ax = SampleQuadratic(x0, cx, x1, t0);
            float ay = SampleQuadratic(y0, cy, y1, t0);
            float bx = SampleQuadratic(x0, cx, x1, t1);
            float by = SampleQuadratic(y0, cy, y1, t1);
            EmitLine(segments, ax, ay, bx, by);
        }
    }

    private static float SampleQuadratic(float p0, float c, float p1, float t)
    {
        float u = 1f - t;
        return u * u * p0 + 2f * u * t * c + t * t * p1;
    }

    private static bool IsInside(float x, float y, List<(float X1, float Y1, float X2, float Y2)> segments)
    {
        bool inside = false;
        foreach (var (x1, y1, x2, y2) in segments)
        {
            if ((y1 > y) != (y2 > y))
            {
                float xIntersect = x1 + (y - y1) * (x2 - x1) / (y2 - y1);
                if (xIntersect > x)
                {
                    inside = !inside;
                }
            }
        }

        return inside;
    }
}