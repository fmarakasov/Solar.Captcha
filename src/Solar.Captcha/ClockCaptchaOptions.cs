using System;
using Solar.Captcha.Raster;

namespace Solar.Captcha;

/// <summary>
/// Configures how a clock captcha is drawn and answered: the dial geometry, colours, dial font, and
/// the minute resolution at which the answer is set.
/// </summary>
/// <remarks>
/// This is the configuration view of a clock. The host maps it onto the renderer's own
/// <see cref="ClockRenderOptions"/> — hex colour strings become <see cref="RasterColor"/> and a font
/// family or path becomes a <see cref="RasterFont"/> (ADR-0013).
/// </remarks>
public class ClockCaptchaOptions
{
    /// <summary>Gets or sets the minute step between answerable dial positions. Must divide 60.</summary>
    public int MinuteStep { get; set; } = 5;

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

    /// <summary>Gets or sets the dial number font size in pixels.</summary>
    public float FontPixelSize { get; set; } = 15f;

    /// <summary>
    /// Gets or sets the dial font family name, resolved from the platform font directories. Ignored
    /// when <see cref="FontPath"/> is set.
    /// </summary>
    public string FontFamily { get; set; } = "Arial";

    /// <summary>Gets or sets an explicit font file path, bypassing family-name resolution.</summary>
    public string? FontPath { get; set; }

    /// <summary>Gets or sets the hex colour of the hands.</summary>
    public string HandColor { get; set; } = "0D2C74";

    /// <summary>Gets or sets the hex colour of the dial outline and hour marks.</summary>
    public string ClockColor { get; set; } = "3970F3";

    /// <summary>Gets or sets the hex colour of the dial face background.</summary>
    public string BackgroundColor { get; set; } = "EEF6FF";

    /// <summary>Gets or sets the hex colour the background gradient starts from.</summary>
    public string GradientColor1 { get; set; } = "E0FAF2";

    /// <summary>Gets or sets the hex colour the background gradient ends at.</summary>
    public string GradientColor2 { get; set; } = "BFCFF8";

    /// <summary>
    /// Validates every value, resolving the dial font so that a bad configuration fails before the
    /// first request rather than in front of a user.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">When a value is not positive or the step is unusable.</exception>
    /// <exception cref="ArgumentException">When a colour string is invalid.</exception>
    /// <exception cref="InvalidOperationException">When the dial font cannot be resolved.</exception>
    public void Validate()
    {
        if (MinuteStep <= 0 || 60 % MinuteStep != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(MinuteStep), MinuteStep,
                "MinuteStep must be positive and divide 60.");
        }

        if (ClockRadius <= 0) throw new ArgumentOutOfRangeException(nameof(ClockRadius));
        if (ClockThickness <= 0) throw new ArgumentOutOfRangeException(nameof(ClockThickness));
        if (ClockDashThickness <= 0) throw new ArgumentOutOfRangeException(nameof(ClockDashThickness));
        if (ClockDashMargin <= 0) throw new ArgumentOutOfRangeException(nameof(ClockDashMargin));
        if (ClockDashLength <= 0) throw new ArgumentOutOfRangeException(nameof(ClockDashLength));
        if (HandsThickness <= 0) throw new ArgumentOutOfRangeException(nameof(HandsThickness));
        if (HourHandLength <= 0) throw new ArgumentOutOfRangeException(nameof(HourHandLength));
        if (MinuteHandLength <= 0) throw new ArgumentOutOfRangeException(nameof(MinuteHandLength));
        if (GifFrameDelay <= 0) throw new ArgumentOutOfRangeException(nameof(GifFrameDelay));
        if (FontPixelSize <= 0) throw new ArgumentOutOfRangeException(nameof(FontPixelSize));

        // ParseHex validates the colour strings.
        RasterColor.ParseHex(HandColor);
        RasterColor.ParseHex(ClockColor);
        RasterColor.ParseHex(BackgroundColor);
        RasterColor.ParseHex(GradientColor1);
        RasterColor.ParseHex(GradientColor2);

        _ = ResolveFont();
    }

    /// <summary>
    /// Builds the renderer's view of the clock, resolving the dial font and validating every value.
    /// </summary>
    /// <exception cref="ArgumentException">When a colour or a value is invalid.</exception>
    /// <exception cref="InvalidOperationException">When the dial font cannot be resolved.</exception>
    public ClockRenderOptions ToClockRenderOptions(int width, int height)
    {
        var font = ResolveFont();

        var renderOptions = new ClockRenderOptions
        {
            Width = width,
            Height = height,
            ClockRadius = ClockRadius,
            ClockThickness = ClockThickness,
            ClockDashThickness = ClockDashThickness,
            ClockDashMargin = ClockDashMargin,
            ClockDashLength = ClockDashLength,
            HandsThickness = HandsThickness,
            HourHandLength = HourHandLength,
            MinuteHandLength = MinuteHandLength,
            GifFrameDelay = GifFrameDelay,
            FontPixelSize = FontPixelSize,
            HandColor = RasterColor.ParseHex(HandColor),
            ClockColor = RasterColor.ParseHex(ClockColor),
            BackgroundColor = RasterColor.ParseHex(BackgroundColor),
            GradientColor1 = RasterColor.ParseHex(GradientColor1),
            GradientColor2 = RasterColor.ParseHex(GradientColor2),
            Font = font,
        };

        renderOptions.Validate();

        return renderOptions;
    }

    private RasterFont ResolveFont()
    {
        if (!string.IsNullOrWhiteSpace(FontPath))
        {
            return RasterFont.FromFile(FontPath);
        }

        if (string.IsNullOrWhiteSpace(FontFamily))
        {
            throw new InvalidOperationException("A clock captcha requires either FontPath or FontFamily to be set.");
        }

        if (!RasterFontResolver.TryResolveFamilyName(FontFamily, out var fontPath))
        {
            throw new InvalidOperationException(
                $"The clock font family '{FontFamily}' could not be resolved to a font file.");
        }

        return RasterFont.FromFile(fontPath);
    }
}

/// <summary>Options for the session-based clock captcha flow.</summary>
public class SessionBasedClockCaptchaOptions : SessionBasedImageCaptchaOptions
{
    /// <summary>Initializes the clock flow's image size to the clock's default frame size.</summary>
    public SessionBasedClockCaptchaOptions()
    {
        Width = 282;
        Height = 240;
    }

    /// <summary>Gets or sets how the clock is drawn and answered.</summary>
    public ClockCaptchaOptions Clock { get; set; } = new();
}

/// <summary>Options for the stateless (Data Protection) clock captcha flow.</summary>
public class StatelessClockCaptchaOptions : StatelessImageCaptchaOptions
{
    /// <summary>Initializes the clock flow's image size to the clock's default frame size.</summary>
    public StatelessClockCaptchaOptions()
    {
        Width = 282;
        Height = 240;
    }

    /// <summary>Gets or sets how the clock is drawn and answered.</summary>
    public ClockCaptchaOptions Clock { get; set; } = new();
}

/// <summary>Options for the shared-key stateless clock captcha flow.</summary>
public class SharedKeyStatelessClockCaptchaOptions : SharedKeyStatelessImageCaptchaOptions
{
    /// <summary>Initializes the clock flow's image size to the clock's default frame size.</summary>
    public SharedKeyStatelessClockCaptchaOptions()
    {
        Width = 282;
        Height = 240;
    }

    /// <summary>Gets or sets how the clock is drawn and answered.</summary>
    public ClockCaptchaOptions Clock { get; set; } = new();
}