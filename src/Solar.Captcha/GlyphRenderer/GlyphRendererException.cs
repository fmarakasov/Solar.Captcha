using System;

namespace Solar.Captcha.GlyphRenderer;

/// <summary>
/// Thrown when a whole glyph rendering call fails; for example, when the configured
/// font file cannot be loaded or parsed. Per-character failures are not thrown —
/// they are captured in <see cref="GlyphRenderResult.Failures"/>.
/// </summary>
internal sealed class GlyphRendererException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GlyphRendererException"/> class.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="innerException">The exception that is the cause of this exception, if any.</param>
    public GlyphRendererException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}