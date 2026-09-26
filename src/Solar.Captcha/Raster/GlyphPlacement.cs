namespace Solar.Captcha.Raster;

/// <summary>
/// The result of rasterizing one character at one pixel size: a binary coverage mask plus
/// where it sits relative to the pen position.
/// </summary>
/// <remarks>
/// Coverage values are 0 (outside) or 1 (inside) — the rasterizer does not anti-alias, so there
/// is no intermediate value to blend with (ADR-0010).
/// </remarks>
internal readonly struct GlyphPlacement
{
    /// <summary>Gets the coverage mask, row-major, <see cref="Width"/> × <see cref="Height"/> bytes.</summary>
    public required byte[] Coverage { get; init; }

    /// <summary>Gets the mask width in pixels.</summary>
    public required int Width { get; init; }

    /// <summary>Gets the mask height in pixels.</summary>
    public required int Height { get; init; }

    /// <summary>Gets the pixel offset from the pen position to the mask's left edge.</summary>
    public required int OffsetX { get; init; }

    /// <summary>
    /// Gets the pixel offset from the baseline to the mask's top edge. Negative values mean the
    /// mask sits above the baseline, which is the usual case.
    /// </summary>
    public required int OffsetY { get; init; }

    /// <summary>Gets the horizontal advance to the next pen position, in pixels.</summary>
    public required float Advance { get; init; }
}
