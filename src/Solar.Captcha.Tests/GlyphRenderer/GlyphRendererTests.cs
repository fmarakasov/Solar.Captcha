using NUnit.Framework;
using Solar.Captcha.GlyphRenderer;
using System;
using System.Collections.Generic;
using System.IO;

namespace Solar.Captcha.Tests.GlyphRenderer;

[TestFixture]
public class GlyphRendererTests
{
    private string _testFontPath = null!;
    private GlyphRenderOptions _options = null!;

    [SetUp]
    public void SetUp()
    {
        _testFontPath = Path.Combine(TestContext.CurrentContext.TestDirectory, "Fonts", "arial.ttf");
        _options = new GlyphRenderOptions
        {
            FontPath = _testFontPath
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

    [Test]
    public void GlyphAsciiArt_ToAsciiArt_WithKnownBitmap_RendersOneCharacterPerPixel()
    {
        // Arrange: empty row, row with only the leftmost pixel, row with only the rightmost pixel.
        byte[] glyph = [0x00, 0x80, 0x01];

        // Act
        var art = GlyphAsciiArt.ToAsciiArt(glyph);

        // Assert
        Assert.That(art, Is.EqualTo("........\n#.......\n.......#"));
    }

    [Test]
    public void GlyphAsciiArt_ToAsciiArt_WithEmptyGlyph_RendersOneLinePerRow()
    {
        // Arrange
        var glyph = new byte[14];

        // Act
        var lines = GlyphAsciiArt.ToAsciiArt(glyph).Split('\n');

        // Assert
        Assert.That(lines.Length, Is.EqualTo(14));
        Assert.That(lines, Is.All.EqualTo("........"));
    }

    [Test]
    public void GlyphAsciiArt_ToAsciiArt_WithMultipleGlyphs_HeadersEachGlyphInCharacterOrder()
    {
        // Arrange
        var glyphs = new Dictionary<char, byte[]>
        {
            ['Y'] = [0x80],
            ['X'] = [0x00]
        };

        // Act
        var art = GlyphAsciiArt.ToAsciiArt(glyphs);

        // Assert
        Assert.That(art, Is.EqualTo("'X':\n........\n\n'Y':\n#......."));
    }

    [Test]
    public void GlyphAsciiArt_ToAsciiArt_WithStaticGlyph_MatchesCaptchaFontPixelAccessor()
    {
        // Arrange
        const char character = 'A';
        Assert.That(CaptchaFont.TryGetGlyph(character, out var glyph), Is.True,
            "'A' should have a hand-authored glyph");

        // Act
        var lines = GlyphAsciiArt.ToAsciiArt(glyph).Split('\n');

        // Assert: the pseudographics agree with the pixel accessor used by the image pipeline,
        // so what a reviewer sees is what CaptchaImage draws.
        for (int y = 0; y < glyph.Length; y++)
        {
            for (int x = 0; x < GlyphAsciiArt.Columns; x++)
            {
                bool isSet = CaptchaFont.IsPixelSet(glyph, x, y);
                Assert.That(
                    lines[y][x],
                    Is.EqualTo(isSet ? GlyphAsciiArt.PixelOn : GlyphAsciiArt.PixelOff),
                    $"Pixel mismatch at ({x},{y}) for '{character}'");
            }
        }
    }

    [Test]
    public void GlyphRenderer_Render_WithCaptchaAlphabet_WritesPseudographicsToTestOutput()
    {
        // Arrange
        var renderer = new Solar.Captcha.GlyphRenderer.GlyphRenderer(_options);
        const string captchaAlphabet = "2346789ABCDGHKMNPRUVWXYZ";

        // Act
        var result = renderer.Render(captchaAlphabet);

        // Assert: every character renders, and the shapes are written to the test output
        // so a reviewer can confirm the glyphs are legible.
        Assert.That(result.Glyphs.Count, Is.EqualTo(captchaAlphabet.Length));
        Assert.That(result.Failures, Is.Empty);
        GlyphAsciiArt.WriteToTestOutput(result.Glyphs);
    }

    [Test]
    public void GlyphRenderer_Render_GlyphA_MatchesExpectedPseudographics()
    {
        AssertPseudographics('A',
        [
            "........",
            "........",
            "...##...",
            "...##...",
            "..###...",
            "..#..#..",
            "..#..#..",
            ".######.",
            ".#....#.",
            ".#....#.",
            "#......#",
            "........",
            "........",
            "........",
        ]);
    }

    [Test]
    public void GlyphRenderer_Render_Glyph9_MatchesExpectedPseudographics()
    {
        AssertPseudographics('9',
        [
            "........",
            "........",
            "..###...",
            ".##..#..",
            ".#....#.",
            ".#....#.",
            ".##..##.",
            "..###.#.",
            "......#.",
            ".#...#..",
            "..####..",
            "........",
            "........",
            "........",
        ]);
    }

    // These Cyrillic characters are composite glyphs whose component carries a signed 2.14
    // fixed-point (F2Dot14) transform with xScale = -1.0 (a horizontal mirror). Treating the raw
    // 16384 as an integer scaled the component ~16384x outside the glyph's own bounding box.
    [TestCase('Э')] // U+042D
    [TestCase('Я')] // U+042F
    [TestCase('э')] // U+044D
    public void TrueTypeFont_GetOutline_WithTransformedCompositeGlyph_StaysWithinDeclaredBounds(char character)
    {
        // Arrange
        var font = TrueTypeFont.Load(_testFontPath);
        int glyphIndex = font.GetGlyphIndex(character);

        // Act
        var outline = font.GetOutline(glyphIndex);

        // Assert
        Assert.That(outline, Is.Not.Null, $"'{character}' should have an outline");
        Assert.That(outline.PointCount, Is.GreaterThan(0), $"'{character}' should have points");

        int minX = int.MaxValue, maxX = int.MinValue, minY = int.MaxValue, maxY = int.MinValue;
        for (int i = 0; i < outline.PointCount; i++)
        {
            minX = Math.Min(minX, outline.Xs[i]);
            maxX = Math.Max(maxX, outline.Xs[i]);
            minY = Math.Min(minY, outline.Ys[i]);
            maxY = Math.Max(maxY, outline.Ys[i]);
        }

        // The glyph header carries the true bounding box in font units, so decoded component
        // coordinates must land inside it (one unit of slack for fixed-point rounding).
        Assert.That(minX, Is.GreaterThanOrEqualTo(outline.XMin - 1), $"'{character}' minX");
        Assert.That(maxX, Is.LessThanOrEqualTo(outline.XMax + 1), $"'{character}' maxX");
        Assert.That(minY, Is.GreaterThanOrEqualTo(outline.YMin - 1), $"'{character}' minY");
        Assert.That(maxY, Is.LessThanOrEqualTo(outline.YMax + 1), $"'{character}' maxY");
    }

    [Test]
    public void GlyphRenderOptions_GlyphGrid_MatchesStaticGlyphFormat()
    {
        // Assert: the dynamic renderer targets the same grid the image pipeline consumes
        // (ADR-001), so a dynamic glyph is interchangeable with a static one.
        Assert.That(GlyphRenderOptions.GlyphWidth, Is.EqualTo(CaptchaFont.GlyphWidth));
        Assert.That(GlyphRenderOptions.GlyphHeight, Is.EqualTo(CaptchaFont.GlyphHeight));
    }

    [TestCase('W')]
    [TestCase('M')]
    [TestCase('@')]
    public void GlyphRenderer_Render_WithWideGlyph_ProducesNonEmptyGlyphAtGridSize(char character)
    {
        // Arrange
        var renderer = new Solar.Captcha.GlyphRenderer.GlyphRenderer(_options);

        // Act
        var result = renderer.Render(character.ToString());

        // Assert: guards the regression where a glyph was centered across a grid wider than
        // one byte and the columns past the first byte were dropped (0x80 >> px == 0 for
        // px >= 8), silently returning a clipped — or entirely blank — glyph as a success.
        Assert.That(result.Glyphs.ContainsKey(character), Is.True, $"'{character}' should render");

        var glyph = result.Glyphs[character];
        Assert.That(glyph.Length, Is.EqualTo(GlyphRenderOptions.GlyphHeight));

        bool hasPixels = false;
        foreach (byte row in glyph)
        {
            if (row != 0x00)
            {
                hasPixels = true;
                break;
            }
        }

        Assert.That(hasPixels, Is.True, $"'{character}' should have at least one pixel set");
    }

    // Code points are passed as integers rather than char literals: NUnit names parameterized tests
    // from the argument text and silently collapses cases whose names are identical or unprintable,
    // which would drop the U+FFFF case that exercises the '.notdef' (index 0) path.
    [TestCase(0xFFFF)]
    [TestCase(0xFFFE)]
    [TestCase(0xE000)]
    [TestCase(0x0378)]
    public void GlyphRenderer_Render_WithCharacterMissingFromFont_ReportsCharacterNotInFont(int codePoint)
    {
        // Arrange
        char character = (char)codePoint;
        var renderer = new Solar.Captcha.GlyphRenderer.GlyphRenderer(_options);

        // Act
        var result = renderer.Render(character.ToString());

        // Assert: a character with no cmap entry resolves to glyph index 0 ('.notdef') in some
        // cmap layouts (Arial maps U+FFFF that way), so index 0 must be reported as a failure
        // rather than rasterized as a success that returns .notdef's outline.
        Assert.That(result.Glyphs.ContainsKey(character), Is.False, $"'{character}' must not render");
        Assert.That(result.Failures.ContainsKey(character), Is.True, $"'{character}' should be reported as failed");
        Assert.That(
            result.Failures[character],
            Is.EqualTo(GlyphRenderFailureReason.CharacterNotInFont),
            $"'{character}' should be reported as {nameof(GlyphRenderFailureReason.CharacterNotInFont)}");
    }

    /// <summary>
    /// Renders <paramref name="character"/> and asserts that its pseudographics
    /// match <paramref name="expectedRows"/> (one string per glyph row). Pinning
    /// the shape as text means a rasterizer or font change shows up as a readable
    /// diff of the glyph, not just a numeric mismatch.
    /// </summary>
    private void AssertPseudographics(char character, string[] expectedRows)
    {
        // Arrange
        var renderer = new Solar.Captcha.GlyphRenderer.GlyphRenderer(_options);

        // Act
        var result = renderer.Render(character.ToString());

        // Assert
        Assert.That(result.Glyphs.ContainsKey(character), Is.True, $"'{character}' should render");

        var actual = GlyphAsciiArt.ToAsciiArt(result.Glyphs[character]);
        var expected = string.Join('\n', expectedRows);

        Assert.That(
            actual,
            Is.EqualTo(expected),
            $"\nActual ('{character}'):\n{actual}\n\nExpected ('{character}'):\n{expected}");
    }
}