using System;

namespace Solar.Captcha.Raster;

/// <summary>
/// Describes how a stroked shape is drawn: a colour and a stroke width in pixels.
/// </summary>
/// <remarks>
/// Strokes are drawn by stamping a filled disc of the pen diameter along the path, which gives
/// round caps and round joins without needing explicit cap or join options. Thickness is
/// expressed in whole pixels because the rasterizer works on a binary (non-anti-aliased) grid.
/// </remarks>
public sealed class RasterPen
{
    /// <summary>Initializes a pen.</summary>
    /// <param name="color">The stroke colour.</param>
    /// <param name="thickness">The stroke width in pixels. Must be at least 1.</param>
    /// <exception cref="ArgumentOutOfRangeException">When <paramref name="thickness"/> is less than 1.</exception>
    public RasterPen(RasterColor color, int thickness = 1)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(thickness, 1);
        Color = color;
        Thickness = thickness;
    }

    /// <summary>Gets the stroke colour.</summary>
    public RasterColor Color { get; }

    /// <summary>Gets the stroke width in pixels.</summary>
    public int Thickness { get; }
}
