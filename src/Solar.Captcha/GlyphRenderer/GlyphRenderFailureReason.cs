namespace Solar.Captcha.GlyphRenderer;

/// <summary>
/// Defines the reason a character could not be rendered to a glyph.
/// </summary>
internal enum GlyphRenderFailureReason
{
    /// <summary>The font does not contain a glyph for the requested character.</summary>
    CharacterNotInFont,

    /// <summary>
    /// The character is outside the Basic Multilingual Plane and cannot be represented
    /// as a single UTF-16 <see cref="char"/> glyph.
    /// </summary>
    NonBmpCharacter
}