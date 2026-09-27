namespace Solar.Captcha;

/// <summary>
/// The common contract shared by every captcha image renderer, whatever challenge it draws.
/// </summary>
/// <remarks>
/// Each challenge format contributes its own renderer contract behind this base, so a captcha flow
/// depends on the renderer for its own format and never branches on challenge type at render time.
/// </remarks>
public interface ICaptchaImageRenderer
{
    /// <summary>Gets the MIME content type of the image this renderer produces.</summary>
    string ContentType { get; }
}

/// <summary>
/// Renders a letter captcha code to a PNG image.
/// </summary>
/// <remarks>
/// Resolved from dependency injection. The characters the renderer can draw are fixed by the
/// <c>GlyphSet</c> it was constructed with, and that set is materialised once at application
/// start-up, so rendering never resolves glyphs on the fly (ADR-009).
/// </remarks>
public interface ILetterCaptchaImageRenderer : ICaptchaImageRenderer
{
    /// <summary>
    /// Renders a captcha code to a PNG image.
    /// </summary>
    /// <param name="width">The image width in pixels. Must be positive.</param>
    /// <param name="height">The image height in pixels. Must be positive.</param>
    /// <param name="captchaCode">
    /// The code to draw. Every character must be in the configured glyph set charset
    /// (comparison ignores case); otherwise the call throws.
    /// </param>
    /// <param name="fontStyle">The glyph style to draw with.</param>
    /// <param name="drawLines">Whether to draw the background noise lines.</param>
    /// <returns>The PNG image bytes.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="captchaCode"/> is empty or contains a character that is not in
    /// the configured charset.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="width"/> or <paramref name="height"/> is not positive.
    /// </exception>
    byte[] Render(
        int width,
        int height,
        string captchaCode,
        CaptchaFontStyle fontStyle = CaptchaFontStyle.Regular,
        bool drawLines = true);
}

/// <summary>
/// Renders an analog clock captcha to an animated GIF.
/// </summary>
public interface IClockCaptchaImageRenderer : ICaptchaImageRenderer
{
    /// <summary>
    /// Renders an animated clock whose hour and minute hands point at the given time.
    /// </summary>
    /// <param name="width">The frame width in pixels. Must be positive.</param>
    /// <param name="height">The frame height in pixels. Must be positive.</param>
    /// <param name="hours">The hour the hour hand points at, from 1 to 12.</param>
    /// <param name="minutes">The minute the minute hand points at, from 0 to 59.</param>
    /// <returns>The animated GIF bytes.</returns>
    byte[] Render(int width, int height, int hours, int minutes);
}
