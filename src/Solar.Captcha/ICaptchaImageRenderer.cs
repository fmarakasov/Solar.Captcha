namespace Solar.Captcha;

/// <summary>
/// Renders a captcha code to a PNG image.
/// </summary>
/// <remarks>
/// Resolved from dependency injection. The characters the renderer can draw are fixed by the
/// <c>GlyphSet</c> it was constructed with, and that set is materialised once at application
/// start-up, so rendering never resolves glyphs on the fly (ADR-009).
/// </remarks>
public interface ICaptchaImageRenderer
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
