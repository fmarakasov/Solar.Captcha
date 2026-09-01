using NUnit.Framework;
using Solar.Captcha.GlyphRenderer;
using System;
using System.IO;

namespace Edi.Captcha.Tests.GlyphRenderer;

[TestFixture]
public class GlyphRendererTests
{
    private string _testFontPath = null!;
    private GlyphRenderOptions _options = null!;

    [SetUp]
    public void SetUp()
    {
        _testFontPath = @"C:\Windows\Fonts\arial.ttf";
        _options = new GlyphRenderOptions
        {
            FontPath = _testFontPath,
            GlyphWidth = 8,
            GlyphHeight = 14
        };
    }

    [Test]
    public void GlyphRenderOptions_Validate_WithValidOptions_DoesNotThrow()
    {
        // Arrange & Act & Assert
        Assert.DoesNotThrow(() => _options.Validate());
    }

    [Test]
    public void GlyphRenderOptions_Validate_WithEmptyFontPath_ThrowsArgumentException()
    {
        // Arrange
        _options.FontPath = string.Empty;

        // Act & Assert
        Assert.Throws<ArgumentException>(() => _options.Validate());
    }

    [Test]
    public void GlyphRenderOptions_Validate_WithNonExistentFont_ThrowsArgumentException()
    {
        // Arrange
        _options.FontPath = "nonexistent.ttf";

        // Act & Assert
        Assert.Throws<ArgumentException>(() => _options.Validate());
    }

    [Test]
    public void GlyphRenderer_Constructor_WithValidFont_CreatesInstance()
    {
        // Arrange
        _options.Validate();

        // Act & Assert
        Assert.DoesNotThrow(() => new Solar.Captcha.GlyphRenderer.GlyphRenderer(_options));
    }

    [Test]
    public void GlyphRenderer_Render_WithEmptyString_ReturnsEmptyResult()
    {
        // Arrange
        var renderer = new Solar.Captcha.GlyphRenderer.GlyphRenderer(_options);

        // Act
        var result = renderer.Render(string.Empty);

        // Assert
        Assert.That(result.Glyphs.Count, Is.EqualTo(0));
        Assert.That(result.Failures.Count, Is.EqualTo(0));
    }

    [Test]
    public void GlyphRenderer_Render_WithAsciiCharacters_ReturnsGlyphs()
    {
        // Arrange
        var renderer = new Solar.Captcha.GlyphRenderer.GlyphRenderer(_options);

        // Act
        var result = renderer.Render("ABC123");

        // Assert
        Assert.That(result.Glyphs.Count, Is.GreaterThan(0));
        Assert.That(result.Glyphs.Count, Is.EqualTo(6));
        foreach (var kvp in result.Glyphs)
        {
            Assert.That(kvp.Value.Length, Is.EqualTo(14), $"Glyph for '{kvp.Key}' must be 14 bytes");
        }
    }

    [Test]
    public void GlyphRenderer_Render_WithDuplicateCharacters_DeduplicatesInput()
    {
        // Arrange
        var renderer = new Solar.Captcha.GlyphRenderer.GlyphRenderer(_options);

        // Act
        var result = renderer.Render("AABBCC");

        // Assert
        Assert.That(result.Glyphs.Count, Is.EqualTo(3));
        Assert.That(result.Glyphs.ContainsKey('A'), Is.True);
        Assert.That(result.Glyphs.ContainsKey('B'), Is.True);
        Assert.That(result.Glyphs.ContainsKey('C'), Is.True);
    }

    [Test]
    public void GlyphRenderer_Render_WithCyrillicCharacters_ReturnsGlyphs()
    {
        // Arrange
        var renderer = new Solar.Captcha.GlyphRenderer.GlyphRenderer(_options);

        // Act
        var result = renderer.Render("АБВ");

        // Assert
        Assert.That(result.Glyphs.Count, Is.GreaterThan(0), "Arial should contain Cyrillic glyphs");
        foreach (var kvp in result.Glyphs)
        {
            Assert.That(kvp.Value.Length, Is.EqualTo(14), $"Glyph for '{kvp.Key}' must be 14 bytes");
        }
    }

    [Test]
    public void GlyphRenderer_Render_VerifiesGlyphFormat()
    {
        // Arrange
        var renderer = new Solar.Captcha.GlyphRenderer.GlyphRenderer(_options);

        // Act
        var result = renderer.Render("A");

        // Assert
        Assert.That(result.Glyphs.ContainsKey('A'), Is.True);
        var glyph = result.Glyphs['A'];
        Assert.That(glyph.Length, Is.EqualTo(14));
        
        // Verify MSB format: at least one row should have pixels set
        bool hasPixels = false;
        foreach (byte row in glyph)
        {
            if (row != 0x00)
            {
                hasPixels = true;
                break;
            }
        }
        Assert.That(hasPixels, Is.True, "Glyph should have at least some pixels set");
    }

    [Test]
    public void GlyphRenderResult_PartialSuccess_ReportsFailures()
    {
        // Arrange
        var renderer = new Solar.Captcha.GlyphRenderer.GlyphRenderer(_options);
        // U+1F600 (😀) is non-BMP; appears as surrogate pair
        string input = "A😀B";

        // Act
        var result = renderer.Render(input);

        // Assert
        Assert.That(result.Glyphs.Count, Is.EqualTo(2), "A and B should render");
        Assert.That(result.Failures.Count, Is.EqualTo(2), "Both surrogate code units should fail");
        Assert.That(result.Glyphs.ContainsKey('A'), Is.True);
        Assert.That(result.Glyphs.ContainsKey('B'), Is.True);
    }

    [Test]
    public void TrueTypeFont_Load_WithValidFont_ReturnsFont()
    {
        // Arrange & Act
        var font = TrueTypeFont.Load(_testFontPath);

        // Assert
        Assert.That(font, Is.Not.Null);
        Assert.That(font.UnitsPerEm, Is.GreaterThan(0));
        Assert.That(font.NumGlyphs, Is.GreaterThan(0));
    }

    [Test]
    public void TrueTypeFont_GetGlyphIndex_WithValidChar_ReturnsIndex()
    {
        // Arrange
        var font = TrueTypeFont.Load(_testFontPath);

        // Act
        int glyphIndex = font.GetGlyphIndex('A');

        // Assert
        Assert.That(glyphIndex, Is.GreaterThanOrEqualTo(0));
    }

    [Test]
    public void TrueTypeFont_GetOutline_WithValidIndex_ReturnsOutline()
    {
        // Arrange
        var font = TrueTypeFont.Load(_testFontPath);
        int glyphIndex = font.GetGlyphIndex('A');

        // Act
        var outline = font.GetOutline(glyphIndex);

        // Assert
        Assert.That(outline, Is.Not.Null);
        Assert.That(outline.PointCount, Is.GreaterThan(0));
    }
}