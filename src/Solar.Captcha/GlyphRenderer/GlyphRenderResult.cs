using System.Collections.Generic;

namespace Solar.Captcha.GlyphRenderer;

/// <summary>
/// The outcome of a glyph rendering call. A call may be partially successful:
/// some characters render to glyphs, others fail and are reported with a reason.
/// </summary>
internal sealed class GlyphRenderResult
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GlyphRenderResult"/> class.
    /// </summary>
    /// <param name="glyphs">The successfully rendered glyphs, keyed by character.</param>
    /// <param name="failures">The characters that failed to render, keyed by reason.</param>
    public GlyphRenderResult(
        IReadOnlyDictionary<char, byte[]> glyphs,
        IReadOnlyDictionary<char, GlyphRenderFailureReason> failures)
    {
        Glyphs = glyphs;
        Failures = failures;
    }

    /// <summary>Gets the successfully rendered glyphs, keyed by character.</summary>
    public IReadOnlyDictionary<char, byte[]> Glyphs { get; }

    /// <summary>Gets the characters that failed to render and the reason for each.</summary>
    public IReadOnlyDictionary<char, GlyphRenderFailureReason> Failures { get; }
}