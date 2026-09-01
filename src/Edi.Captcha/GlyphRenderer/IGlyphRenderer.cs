namespace Solar.Captcha.GlyphRenderer;

/// <summary>
/// Commands the rendering of characters to glyph bitmaps in the
/// Solar.Captcha glyph format (14 rows × 8 bits, MSB = leftmost pixel).
/// </summary>
internal interface IGlyphRenderer
{
    /// <summary>
    /// Renders the requested characters to <see cref="byte"/>[14] glyphs.
    /// </summary>
    /// <param name="characters">The characters to render. Duplicates are de-duplicated.</param>
    /// <returns>
    /// A <see cref="GlyphRenderResult"/> with the successfully rendered glyphs and the
    /// characters that failed (with reasons). An unloadable font throws
    /// <see cref="GlyphRendererException"/>.
    /// </returns>
    GlyphRenderResult Render(string characters);
}