using NUnit.Framework;
using Solar.Captcha.GlyphRenderer;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Solar.Captcha.Tests;

[TestFixture]
public class GlyphSetTests
{
    private const string Letters = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";

    private static byte[] Glyph(byte fill)
    {
        var glyph = new byte[GlyphRenderOptions.GlyphHeight];
        Array.Fill(glyph, fill);
        return glyph;
    }

    #region Static glyph source

    [Test]
    public void StaticGlyphRenderer_WithAuthoredCharacters_RendersEveryGlyph()
    {
        // Act
        var result = new StaticGlyphRenderer().Render(Letters);

        // Assert
        Assert.That(result.Failures, Is.Empty);
        Assert.That(result.Glyphs.Keys, Is.EquivalentTo(Letters.ToCharArray()));
        Assert.That(result.Glyphs.Values, Has.All.Length.EqualTo(GlyphRenderOptions.GlyphHeight));
    }

    [Test]
    public void StaticGlyphRenderer_WithUnauthoredCharacter_ReportsFailureInsteadOfSubstituting()
    {
        // Act - '!' has no authored bitmap
        var result = new StaticGlyphRenderer().Render("A!");

        // Assert
        Assert.That(result.Glyphs.Keys, Is.EqualTo(new[] { 'A' }));
        Assert.That(result.Failures.Keys, Is.EqualTo(new[] { '!' }));
        Assert.That(result.Failures['!'], Is.EqualTo(GlyphRenderFailureReason.CharacterNotInFont));
    }

    [Test]
    public void StaticGlyphRenderer_WithNonBmpCharacter_ReportsNonBmpFailure()
    {
        // Act
        var result = new StaticGlyphRenderer().Render("A\uD83D\uDE00");

        // Assert
        Assert.That(result.Failures.Values, Has.Some.EqualTo(GlyphRenderFailureReason.NonBmpCharacter));
    }

    [Test]
    public void StaticGlyphRenderer_WithEmptyInput_ReturnsEmptyResult()
    {
        // Act
        var result = new StaticGlyphRenderer().Render(string.Empty);

        // Assert
        Assert.That(result.Glyphs, Is.Empty);
        Assert.That(result.Failures, Is.Empty);
    }

    #endregion

    #region Charset normalisation and lookup

    [Test]
    public void Constructor_NormalizesCharsetToUpperCaseWithoutDuplicates()
    {
        // Act
        var set = new GlyphSet("aAbBcC", new Dictionary<char, byte[]>
        {
            ['A'] = Glyph(0xAA),
            ['B'] = Glyph(0xBB),
            ['C'] = Glyph(0xCC)
        });

        // Assert
        Assert.That(set.Charset, Is.EqualTo("ABC"));
    }

    [Test]
    public void TryGetGlyph_WithLowercaseCharacter_IsCaseInsensitive()
    {
        // Arrange
        var set = new GlyphSet("AB", new Dictionary<char, byte[]> { ['A'] = Glyph(0xAA), ['B'] = Glyph(0xBB) });

        // Act & Assert
        Assert.That(set.TryGetGlyph('a', out var lower), Is.True);
        Assert.That(set.TryGetGlyph('A', out var upper), Is.True);
        Assert.That(lower, Is.SameAs(upper));
    }

    [Test]
    public void TryGetGlyph_WithCharacterOutsideCharset_ReturnsFalse()
    {
        // Arrange
        var set = new GlyphSet("AB", new Dictionary<char, byte[]> { ['A'] = Glyph(0xAA), ['B'] = Glyph(0xBB) });

        // Act & Assert
        Assert.That(set.TryGetGlyph('Z', out _), Is.False);
    }

    [Test]
    public void FindMissing_ReturnsDistinctCharactersNotInSet()
    {
        // Arrange
        var set = new GlyphSet("ABC", new Dictionary<char, byte[]>
        {
            ['A'] = Glyph(0xAA),
            ['B'] = Glyph(0xBB),
            ['C'] = Glyph(0xCC)
        });

        // Act
        var missing = set.FindMissing("ABZZ!z!");

        // Assert
        Assert.That(missing, Is.EqualTo(new[] { 'Z', '!' }));
    }

    [Test]
    public void FindMissing_WhenEveryCharacterIsDrawable_ReturnsEmpty()
    {
        // Arrange
        var set = new GlyphSet("ABC", new Dictionary<char, byte[]>
        {
            ['A'] = Glyph(0xAA),
            ['B'] = Glyph(0xBB),
            ['C'] = Glyph(0xCC)
        });

        // Act & Assert
        Assert.That(set.FindMissing("cab"), Is.Empty);
    }

    #endregion

    #region Totality

    [Test]
    public void Constructor_WhenCharsetCharacterHasNoGlyph_ThrowsNamingEveryUnresolvedCharacter()
    {
        // Act
        var ex = Assert.Throws<GlyphRendererException>(() => new GlyphSet("ABC", new Dictionary<char, byte[]>
        {
            ['A'] = Glyph(0xAA)
        }));

        // Assert
        Assert.That(ex!.Message, Does.Contain("'B'"));
        Assert.That(ex.Message, Does.Contain("'C'"));
        Assert.That(ex.Message, Does.Contain("FallbackGlyph"));
    }

    [Test]
    public void Constructor_WithEmptyCharset_ThrowsArgumentException()
    {
        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() =>
            new GlyphSet(string.Empty, new Dictionary<char, byte[]>()));

        Assert.That(ex!.ParamName, Is.EqualTo("charset"));
    }

    #endregion

    #region GlyphSetFactory

    [Test]
    public void Factory_WithoutFallback_ThrowsWhenACharacterCannotBeResolved()
    {
        // Arrange
        var factory = new GlyphSetFactory(new StaticGlyphRenderer());

        // Act
        var ex = Assert.Throws<GlyphRendererException>(() =>
            factory.Get(new GlyphSetOptions { Charset = "AB!" }));

        // Assert
        Assert.That(ex!.Message, Does.Contain("'!'"));
    }

    [Test]
    public void Factory_WithFallback_SubstitutesItForEveryUnresolvableCharacter()
    {
        // Arrange
        var fallback = Glyph(0xEE);
        var factory = new GlyphSetFactory(new StaticGlyphRenderer());

        // Act
        var set = factory.Get(new GlyphSetOptions { Charset = "AB!", FallbackGlyph = fallback });

        // Assert
        Assert.That(set.Charset, Is.EqualTo("AB!"));
        Assert.That(set.TryGetGlyph('!', out var substituted), Is.True);
        Assert.That(substituted, Is.SameAs(fallback));
    }

    [Test]
    public void Factory_WithoutFallback_DoesNotSubstituteResolvableCharacters()
    {
        // Arrange
        var fallback = Glyph(0xEE);
        var factory = new GlyphSetFactory(new StaticGlyphRenderer());

        // Act
        var set = factory.Get(new GlyphSetOptions { Charset = "AB", FallbackGlyph = fallback });

        // Assert
        Assert.That(set.TryGetGlyph('A', out var glyph), Is.True);
        Assert.That(glyph, Is.Not.SameAs(fallback));
    }

    [Test]
    public void Factory_CalledRepeatedly_BuildsTheSetOnlyOnce()
    {
        // Arrange
        var factory = new GlyphSetFactory(new StaticGlyphRenderer());
        var options = new GlyphSetOptions { Charset = "AB" };

        // Act
        var first = factory.Get(options);
        var second = factory.Get(options);

        // Assert
        Assert.That(second, Is.SameAs(first));
    }

    [Test]
    public void Factory_WhenTheSourceCannotLoad_PropagatesTheFailure()
    {
        // Arrange
        var factory = new GlyphSetFactory(new ThrowingGlyphRenderer());

        // Act & Assert
        Assert.Throws<GlyphRendererException>(() => factory.Get(new GlyphSetOptions { Charset = "A" }));
    }

    private sealed class ThrowingGlyphRenderer : IGlyphRenderer
    {
        public GlyphRenderResult Render(string characters) =>
            throw new GlyphRendererException("font could not be loaded");
    }

    #endregion

    #region GlyphSetOptions validation

    [Test]
    public void Validate_WithEmptyCharset_ThrowsArgumentException()
    {
        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => new GlyphSetOptions { Charset = "" }.Validate());

        Assert.That(ex!.ParamName, Is.EqualTo("Charset"));
    }

    [Test]
    public void Validate_WithNonBmpCharset_ThrowsArgumentException()
    {
        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() =>
            new GlyphSetOptions { Charset = "A\uD83D\uDE00" }.Validate());

        Assert.That(ex!.ParamName, Is.EqualTo("Charset"));
        Assert.That(ex.Message, Does.Contain("non-BMP"));
    }

    [Test]
    public void Validate_WithFallbackGlyphOfWrongLength_ThrowsArgumentException()
    {
        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() =>
            new GlyphSetOptions { Charset = "A", FallbackGlyph = new byte[3] }.Validate());

        Assert.That(ex!.ParamName, Is.EqualTo("FallbackGlyph"));
    }

    #endregion
}
