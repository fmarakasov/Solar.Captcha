using System;
using System.Globalization;
using System.IO;

namespace Solar.Captcha.Raster;

/// <summary>
/// Draws an animated clock challenge: a dial with hour numbers, an hour hand, a minute hand, and a
/// second hand that advances one step per frame, encoded as an animated GIF.
/// </summary>
/// <remarks>
/// <para>
/// The dial is painted once and reused for every frame; each frame copies it and draws the second
/// hand at that frame's angle. Only one frame is alive at a time, so the encoder streams rather than
/// holding the whole animation.
/// </para>
/// <para>
/// Angles are measured clockwise from twelve o'clock. The hour hand moves in whole-hour steps and
/// the minute hand in whole-minute steps, so the hands always point exactly at the values being
/// asked about, which is what makes the challenge answerable.
/// </para>
/// </remarks>
public sealed class ClockGifRenderer
{
    /// <summary>The number of frames in one sweep of the second hand.</summary>
    public const int FrameCount = 60;

    /// <summary>The number of degrees in a full circle.</summary>
    public const int CircleDegrees = 360;

    /// <summary>The number of hours a dial shows.</summary>
    public const int HoursCount = 12;

    /// <summary>The number of minutes in an hour.</summary>
    public const int MinutesCount = 60;

    /// <summary>The number of degrees the second hand advances per frame.</summary>
    public const int SecondStepDegrees = 6;

    /// <summary>The number of degrees between hour marks.</summary>
    public const int DashStepDegrees = 30;

    /// <summary>The number of degrees between the numbered hour marks.</summary>
    public const int NumbersStepDegrees = 90;

    /// <summary>The number of points used to approximate the dial outline.</summary>
    public const int ClockPointsCount = 361;

    private readonly Random _random;

    /// <summary>
    /// Creates a renderer.
    /// </summary>
    /// <param name="random">
    /// The source of the dial's random gradient axis. Pass a seeded instance for reproducible output.
    /// Defaults to <see cref="Random.Shared"/>.
    /// </param>
    public ClockGifRenderer(Random? random = null) => _random = random ?? Random.Shared;

    /// <summary>
    /// Renders a clock challenge as GIF bytes.
    /// </summary>
    /// <param name="options">The dial geometry, colours, font, and timing.</param>
    /// <param name="hours">The hour the hour hand points at, from 0 to 12.</param>
    /// <param name="minutes">The minute the minute hand points at, from 0 to 59.</param>
    /// <returns>The animated GIF bytes.</returns>
    /// <exception cref="ArgumentNullException">When <paramref name="options"/> is <see langword="null"/>.</exception>
    public byte[] Render(ClockRenderOptions options, int hours, int minutes)
    {
        using var buffer = new MemoryStream();
        Render(buffer, options, hours, minutes);
        return buffer.ToArray();
    }

    /// <summary>
    /// Renders a clock challenge to a stream.
    /// </summary>
    /// <param name="output">The stream to write the GIF to. Not disposed by this method.</param>
    /// <param name="options">The dial geometry, colours, font, and timing.</param>
    /// <param name="hours">The hour the hour hand points at, from 0 to 12.</param>
    /// <param name="minutes">The minute the minute hand points at, from 0 to 59.</param>
    /// <exception cref="ArgumentNullException">
    /// When <paramref name="output"/> or <paramref name="options"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// When a value in <paramref name="options"/> is unusable, or the time is out of range.
    /// </exception>
    public void Render(Stream output, ClockRenderOptions options, int hours, int minutes)
    {
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();

        ArgumentOutOfRangeException.ThrowIfNegative(hours, nameof(hours));
        ArgumentOutOfRangeException.ThrowIfGreaterThan(hours, HoursCount, nameof(hours));
        ArgumentOutOfRangeException.ThrowIfNegative(minutes, nameof(minutes));
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(minutes, MinutesCount, nameof(minutes));

        var dial = CreateDial(options);
        DrawHand(dial, options, CircleDegrees / (float)HoursCount * hours, options.HourHandLength);
        DrawHand(dial, options, CircleDegrees / (float)MinutesCount * minutes, options.MinuteHandLength);

        var gif = GifEncoder.Begin(
            output,
            options.Width,
            options.Height,
            options.GifFrameDelay,
            repeatCount: 0,
            options.SeedColors);

        for (var frame = 0; frame < FrameCount; frame++)
        {
            var canvas = dial.Clone();
            DrawHand(canvas, options, frame * SecondStepDegrees, options.MinuteHandLength);
            gif.WriteFrame(canvas);
        }

        gif.End();
    }

    private RasterCanvas CreateDial(ClockRenderOptions options)
    {
        var canvas = new RasterCanvas(options.Width, options.Height);
        canvas.Clear(options.BackgroundColor);

        // The gradient axis runs from a random point on the top edge to its mirror on the bottom
        // edge, so successive captchas differ without the dial ever looking lopsided.
        var gradientStartX = _random.Next(0, options.Width);
        canvas.FillLinearGradient(new LinearGradientBrush(
            new PointF(gradientStartX, 0),
            new PointF(options.Width - gradientStartX, options.Height),
            options.GradientColor1,
            options.GradientColor2,
            GradientRepetition.Reflect));

        var halfWidth = options.Width / 2f;
        var halfHeight = options.Height / 2f;

        DrawDashes(canvas, options, halfWidth, halfHeight);
        DrawNumbers(canvas, options, halfWidth, halfHeight);
        DrawDialOutline(canvas, options, halfWidth, halfHeight);

        return canvas;
    }

    private static void DrawDashes(RasterCanvas canvas, ClockRenderOptions options, float halfWidth, float halfHeight)
    {
        var pen = new RasterPen(options.ClockColor, options.ClockDashThickness);
        var outerRadius = options.ClockRadius - options.ClockDashMargin;
        var innerRadius = outerRadius - options.ClockDashLength;

        for (var angle = 0; angle < CircleDegrees; angle += DashStepDegrees)
        {
            canvas.DrawLine(
                PolarToCartesian(halfWidth, halfHeight, angle, outerRadius),
                PolarToCartesian(halfWidth, halfHeight, angle, innerRadius),
                pen);
        }
    }

    private static void DrawNumbers(RasterCanvas canvas, ClockRenderOptions options, float halfWidth, float halfHeight)
    {
        var radius = options.ClockRadius - (options.ClockDashMargin * 2.5f) - options.ClockDashLength;

        // The dial numbers are metric-centred rather than nudged by a fraction of the font size, so
        // the same code centres them for any font.
        var verticalOffset = (options.Font.GetAscender(options.FontPixelSize)
            + options.Font.GetDescender(options.FontPixelSize)) / 2f;

        var number = 3;
        for (var angle = NumbersStepDegrees; angle <= CircleDegrees; angle += NumbersStepDegrees)
        {
            var anchor = PolarToCartesian(halfWidth, halfHeight, angle, radius);
            var text = number.ToString(CultureInfo.InvariantCulture);
            var width = options.Font.MeasureText(text, options.FontPixelSize);

            canvas.DrawText(
                text,
                options.Font,
                options.FontPixelSize,
                options.HandColor,
                anchor.X - (width / 2f),
                anchor.Y + verticalOffset);

            number += 3;
        }
    }

    private static void DrawDialOutline(RasterCanvas canvas, ClockRenderOptions options, float halfWidth, float halfHeight)
    {
        var ring = new PointF[ClockPointsCount];
        for (var i = 0; i < ClockPointsCount; i++)
        {
            ring[i] = PolarToCartesian(halfWidth, halfHeight, i, options.ClockRadius);
        }

        canvas.StrokePolygon(ring, new RasterPen(options.ClockColor, options.ClockThickness));
    }

    private static void DrawHand(RasterCanvas canvas, ClockRenderOptions options, float angle, int length)
    {
        var centerX = options.Width / 2f;
        var centerY = options.Height / 2f;

        canvas.DrawLine(
            new PointF(centerX, centerY),
            PolarToCartesian(centerX, centerY, angle, length),
            new RasterPen(options.HandColor, options.HandsThickness));
    }

    private static PointF PolarToCartesian(float startX, float startY, double angle, float length)
    {
        var radians = (angle - 90) * Math.PI / 180.0;

        return new PointF(
            (float)((length * Math.Cos(radians)) + startX),
            (float)((length * Math.Sin(radians)) + startY));
    }
}
