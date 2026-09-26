using System;

namespace Solar.Captcha.Raster;

/// <summary>
/// How a clock captcha is drawn: geometry, colours, the dial font, and the animation timing.
/// </summary>
/// <remarks>
/// This is the renderer's own view of a clock, deliberately free of configuration concerns. A host
/// application maps its own configuration onto it — hex colour strings become
/// <see cref="RasterColor"/>, a font family name becomes a <see cref="RasterFont"/>, and a
/// font-to-radius ratio becomes <see cref="FontPixelSize"/>.
/// </remarks>
public sealed class ClockRenderOptions
{
    /// <summary>Gets or sets the frame width in pixels.</summary>
    public int Width { get; set; } = 282;

    /// <summary>Gets or sets the frame height in pixels.</summary>
    public int Height { get; set; } = 240;

    /// <summary>Gets or sets the dial radius in pixels.</summary>
    public int ClockRadius { get; set; } = 81;

    /// <summary>Gets or sets the thickness of the dial outline.</summary>
    public int ClockThickness { get; set; } = 6;

    /// <summary>Gets or sets the thickness of the hour marks.</summary>
    public int ClockDashThickness { get; set; } = 4;

    /// <summary>Gets or sets the gap between the dial outline and the hour marks.</summary>
    public int ClockDashMargin { get; set; } = 8;

    /// <summary>Gets or sets the length of the hour marks.</summary>
    public int ClockDashLength { get; set; } = 13;

    /// <summary>Gets or sets the thickness of the hands.</summary>
    public int HandsThickness { get; set; } = 2;

    /// <summary>Gets or sets the length of the hour hand.</summary>
    public int HourHandLength { get; set; } = 32;

    /// <summary>Gets or sets the length of the minute and second hands.</summary>
    public int MinuteHandLength { get; set; } = 45;

    /// <summary>Gets or sets the delay between frames, in hundredths of a second.</summary>
    public int GifFrameDelay { get; set; } = 100;

    /// <summary>Gets or sets the em size the dial numbers are drawn at, in pixels.</summary>
    public float FontPixelSize { get; set; } = 15f;

    /// <summary>Gets or sets the colour of the hands and the dial numbers.</summary>
    public required RasterColor HandColor { get; set; }

    /// <summary>Gets or sets the colour of the dial outline and the hour marks.</summary>
    public required RasterColor ClockColor { get; set; }

    /// <summary>Gets or sets the base colour painted before the gradient.</summary>
    public required RasterColor BackgroundColor { get; set; }

    /// <summary>Gets or sets the colour the background gradient starts from.</summary>
    public required RasterColor GradientColor1 { get; set; }

    /// <summary>Gets or sets the colour the background gradient ends at.</summary>
    public required RasterColor GradientColor2 { get; set; }

    /// <summary>Gets or sets the font the dial numbers are drawn with.</summary>
    public required RasterFont Font { get; set; }

    /// <summary>
    /// Gets the colours that must survive GIF colour-table reduction: every flat colour the renderer
    /// paints, as opposed to the gradient, which the encoder is free to sample.
    /// </summary>
    internal RasterColor[] SeedColors =>
        [HandColor, ClockColor, BackgroundColor, GradientColor1, GradientColor2];

    /// <summary>
    /// Validates the option values.
    /// </summary>
    /// <exception cref="ArgumentNullException">When <see cref="Font"/> has not been set.</exception>
    /// <exception cref="ArgumentOutOfRangeException">When a dimension, length, or delay is not positive.</exception>
    public void Validate()
    {
        ArgumentNullException.ThrowIfNull(Font, nameof(Font));

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(Width, nameof(Width));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(Height, nameof(Height));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(ClockRadius, nameof(ClockRadius));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(ClockThickness, nameof(ClockThickness));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(ClockDashThickness, nameof(ClockDashThickness));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(ClockDashMargin, nameof(ClockDashMargin));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(ClockDashLength, nameof(ClockDashLength));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(HandsThickness, nameof(HandsThickness));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(HourHandLength, nameof(HourHandLength));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(MinuteHandLength, nameof(MinuteHandLength));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(GifFrameDelay, nameof(GifFrameDelay));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(FontPixelSize, nameof(FontPixelSize));
    }
}
