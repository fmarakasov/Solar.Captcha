using System;
using Solar.Captcha.Raster;

namespace Solar.Captcha;

/// <summary>
/// Adapter that renders clock captchas as animated GIFs using the raster engine's clock renderer.
/// </summary>
internal sealed class ClockCaptchaImageRenderer : IClockCaptchaImageRenderer
{
    private readonly ClockRenderOptions _prototype;
    private readonly ClockGifRenderer _renderer;

    /// <summary>Creates the renderer from a resolved clock prototype and the GIF renderer.</summary>
    public ClockCaptchaImageRenderer(ClockRenderOptions prototype, ClockGifRenderer renderer)
    {
        _prototype = prototype ?? throw new ArgumentNullException(nameof(prototype));
        _renderer = renderer ?? throw new ArgumentNullException(nameof(renderer));
    }

    /// <inheritdoc />
    public string ContentType => "image/gif";

    /// <inheritdoc />
    public byte[] Render(int width, int height, int hours, int minutes)
    {
        var options = WithSize(width, height);
        return _renderer.Render(options, hours, minutes);
    }

    private ClockRenderOptions WithSize(int width, int height) => new()
    {
        Width = width,
        Height = height,
        ClockRadius = _prototype.ClockRadius,
        ClockThickness = _prototype.ClockThickness,
        ClockDashThickness = _prototype.ClockDashThickness,
        ClockDashMargin = _prototype.ClockDashMargin,
        ClockDashLength = _prototype.ClockDashLength,
        HandsThickness = _prototype.HandsThickness,
        HourHandLength = _prototype.HourHandLength,
        MinuteHandLength = _prototype.MinuteHandLength,
        GifFrameDelay = _prototype.GifFrameDelay,
        FontPixelSize = _prototype.FontPixelSize,
        HandColor = _prototype.HandColor,
        ClockColor = _prototype.ClockColor,
        BackgroundColor = _prototype.BackgroundColor,
        GradientColor1 = _prototype.GradientColor1,
        GradientColor2 = _prototype.GradientColor2,
        Font = _prototype.Font,
    };
}