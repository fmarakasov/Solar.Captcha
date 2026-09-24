using System;
using System.Collections.Generic;
using System.Text;

namespace Solar.Captcha.GlyphRenderer;

/// <summary>
/// An immutable, complete set of glyphs for a declared charset.
/// </summary>
/// <remarks>
/// A glyph set is the product of running an <see cref="IGlyphRenderer"/> over a charset once,
/// at application start-up (see ADR-009). It is <i>total</i> over <see cref="Charset"/>:
/// construction fails if any declared character has no glyph, so a constructed set is a
/// guarantee that every character it advertises is drawable. The image pipeline therefore never
/// has to decide what to do about a missing glyph.
/// </remarks>
public sealed class GlyphSet
{
    private readonly Dictionary<char, byte[]> _glyphs;

    /// <summary>
    /// Initializes a new glyph set.
    /// </summary>
    /// <param name="charset">
    /// The characters the set must cover. Lookups and comparison are case-insensitive; the charset
    /// is normalised to upper case with duplicates removed.
    /// </param>
    /// <param name="glyphs">The glyphs that are available, keyed by character.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="charset"/> is empty.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="glyphs"/> is <see langword="null"/>.</exception>
    /// <exception cref="GlyphRendererException">
    /// Thrown when a character in <paramref name="charset"/> has no glyph, in which case the message
    /// names every unresolved character.
    /// </exception>
    public GlyphSet(string charset, IReadOnlyDictionary<char, byte[]> glyphs)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(charset);
        ArgumentNullException.ThrowIfNull(glyphs);

        var normalized = NormalizeCharset(charset);
        _glyphs = new Dictionary<char, byte[]>(normalized.Length);

        List<char>? unresolved = null;
        foreach (var character in normalized)
        {
            if (glyphs.TryGetValue(character, out var glyph))
            {
                _glyphs[character] = glyph;
            }
            else
            {
                (unresolved ??= []).Add(character);
            }
        }

        if (unresolved is not null)
        {
            var names = new string[unresolved.Count];
            for (var i = 0; i < unresolved.Count; i++)
            {
                names[i] = Describe(unresolved[i]);
            }

            throw new GlyphRendererException(
                $"No glyph could be resolved for {unresolved.Count} character(s) in the declared charset: " +
                $"{string.Join(", ", names)}. " +
                "Supply a FallbackGlyph or remove these characters from the charset.");
        }

        Charset = normalized;
    }

    /// <summary>
    /// Gets the charset this set covers, normalised to upper case with duplicates removed.
    /// </summary>
    public string Charset { get; }

    /// <summary>Gets the glyphs in this set, keyed by the normalised character.</summary>
    public IReadOnlyDictionary<char, byte[]> Glyphs => _glyphs;

    /// <summary>
    /// Tries to get the glyph for a character. Lookup ignores case.
    /// </summary>
    /// <param name="character">The character to look up.</param>
    /// <param name="glyph">The glyph, when found.</param>
    /// <returns><see langword="true"/> when the character is in this set.</returns>
    public bool TryGetGlyph(char character, out byte[] glyph) =>
        _glyphs.TryGetValue(char.ToUpperInvariant(character), out glyph);

    /// <summary>
    /// Finds the characters in <paramref name="characters"/> that this set cannot draw.
    /// </summary>
    /// <param name="characters">The characters to check.</param>
    /// <returns>
    /// The distinct characters that are not in <see cref="Charset"/>, or an empty list when all of
    /// them are drawable.
    /// </returns>
    public IReadOnlyList<char> FindMissing(string characters)
    {
        if (string.IsNullOrEmpty(characters))
        {
            return [];
        }

        List<char>? missing = null;
        var seen = new HashSet<char>();
        foreach (var character in characters)
        {
            var normalized = char.ToUpperInvariant(character);
            if (!_glyphs.ContainsKey(normalized) && seen.Add(normalized))
            {
                (missing ??= []).Add(normalized);
            }
        }

        return missing ?? (IReadOnlyList<char>)[];
    }

    /// <summary>
    /// Normalises a charset to upper case with duplicates removed, preserving first-seen order.
    /// </summary>
    internal static string NormalizeCharset(string charset)
    {
        var seen = new HashSet<char>();
        var builder = new StringBuilder(charset.Length);

        foreach (var character in charset)
        {
            var normalized = char.ToUpperInvariant(character);
            if (seen.Add(normalized))
            {
                builder.Append(normalized);
            }
        }

        return builder.ToString();
    }

    /// <summary>Renders a character for a human-readable diagnostic message.</summary>
    internal static string Describe(char character) =>
        char.IsControl(character) || char.IsWhiteSpace(character)
            ? $"U+{(int)character:X4}"
            : $"'{character}' (U+{(int)character:X4})";
}
