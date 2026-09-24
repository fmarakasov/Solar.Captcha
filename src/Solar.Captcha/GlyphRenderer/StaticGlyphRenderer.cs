using System.Collections.Generic;

namespace Solar.Captcha.GlyphRenderer;

/// <summary>
/// The static glyph path: glyphs taken from the hand-authored bitmaps embedded in
/// <see cref="CaptchaFont"/>.
/// </summary>
/// <remarks>
/// This source covers exactly the characters that were authored as bitmaps and no others. It
/// deliberately reports holes through <see cref="GlyphRenderResult.Failures"/> rather than
/// substituting a placeholder, so that it behaves identically to the font-based renderer under
/// start-up validation (ADR-008).
/// </remarks>
internal sealed class StaticGlyphRenderer : IGlyphRenderer
{
    /// <inheritdoc />
    public GlyphRenderResult Render(string characters)
    {
        var glyphs = new Dictionary<char, byte[]>();
        var failures = new Dictionary<char, GlyphRenderFailureReason>();

        if (string.IsNullOrEmpty(characters))
        {
            return new GlyphRenderResult(glyphs, failures);
        }

        foreach (var character in characters)
        {
            if (char.IsSurrogate(character))
            {
                failures[character] = GlyphRenderFailureReason.NonBmpCharacter;
                continue;
            }

            var normalized = char.ToUpperInvariant(character);
            if (CaptchaFont.TryGetGlyph(normalized, out var glyph))
            {
                glyphs[normalized] = glyph;
            }
            else
            {
                failures[normalized] = GlyphRenderFailureReason.CharacterNotInFont;
            }
        }

        return new GlyphRenderResult(glyphs, failures);
    }
}
