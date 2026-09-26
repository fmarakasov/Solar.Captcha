using System;
using System.Collections.Generic;

namespace Solar.Captcha.Fonts;

/// <summary>
/// Fills a TrueType glyph outline into a binary coverage mask on an arbitrary pixel grid,
/// and reports the outline's bounds. Shared by every raster consumer: the 8×14 glyph path and
/// the arbitrary-size raster font path both flatten and fill through this type, so contour
/// handling exists once (ADR-0010).
/// </summary>
internal static class OutlineRasterizer
{
    /// <summary>
    /// Computes the bounding box of an outline, in font units, by scanning its points.
    /// </summary>
    /// <param name="outline">The outline to measure.</param>
    /// <returns>The inclusive bounds of every outline point.</returns>
    /// <exception cref="ArgumentNullException">When <paramref name="outline"/> is <see langword="null"/>.</exception>
    public static (int MinX, int MinY, int MaxX, int MaxY) Bounds(GlyphOutline outline)
    {
        ArgumentNullException.ThrowIfNull(outline);

        var minX = int.MaxValue;
        var minY = int.MaxValue;
        var maxX = int.MinValue;
        var maxY = int.MinValue;

        for (var i = 0; i < outline.PointCount; i++)
        {
            minX = Math.Min(minX, outline.Xs[i]);
            minY = Math.Min(minY, outline.Ys[i]);
            maxX = Math.Max(maxX, outline.Xs[i]);
            maxY = Math.Max(maxY, outline.Ys[i]);
        }

        return (minX, minY, maxX, maxY);
    }

    /// <summary>
    /// Fills an outline into a binary coverage mask on an arbitrary pixel grid.
    /// </summary>
    /// <param name="coverage">
    /// The destination mask: <paramref name="width"/> × <paramref name="height"/> bytes in
    /// row-major order. A pixel is set to 1 when it falls inside the outline; the caller decides
    /// how to shade it. Existing values are left alone, so overlapping fills accumulate.
    /// </param>
    /// <param name="width">The mask width in pixels.</param>
    /// <param name="height">The mask height in pixels.</param>
    /// <param name="outline">The outline, in font units.</param>
    /// <param name="scale">Font units to pixels: one font unit becomes <paramref name="scale"/> pixels.</param>
    /// <param name="xOffset">
    /// The pixel column the outline's left-most point maps to. A pixel centre sampled at
    /// <c>px + 0.5</c> is walked back into the outline's own coordinate space through this offset.
    /// </param>
    /// <param name="baselineY">
    /// The pixel row the font baseline maps to, measured downwards from the top of the mask.
    /// </param>
    /// <remarks>
    /// The fill is even-odd over the flattened outline and never anti-aliases, so coverage values
    /// are 0 or 1 (ADR-001, ADR-0010). Each pixel centre is mapped back into font space and tested
    /// against the flattened contour segments, which keeps the result independent of grid size.
    /// </remarks>
    /// <exception cref="ArgumentNullException">
    /// When <paramref name="coverage"/> or <paramref name="outline"/> is <see langword="null"/>.
    /// </exception>
    public static void Fill(
        byte[] coverage,
        int width,
        int height,
        GlyphOutline outline,
        float scale,
        int xOffset,
        int baselineY)
    {
        ArgumentNullException.ThrowIfNull(coverage);
        ArgumentNullException.ThrowIfNull(outline);

        if (outline.PointCount == 0 || scale <= 0f || width <= 0 || height <= 0)
        {
            return;
        }

        var (gxMin, _, _, _) = Bounds(outline);
        var segments = FlattenContours(outline);

        for (var py = 0; py < height; py++)
        {
            var rowOffset = py * width;
            var fy = (baselineY - (py + 0.5f)) / scale;

            for (var px = 0; px < width; px++)
            {
                var fx = gxMin + ((px + 0.5f - xOffset) / scale);

                if (IsInside(fx, fy, segments))
                {
                    coverage[rowOffset + px] = 1;
                }
            }
        }
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