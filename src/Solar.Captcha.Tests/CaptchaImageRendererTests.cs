using NUnit.Framework;
using Solar.Captcha.GlyphRenderer;
using System;
using System.Threading.Tasks;

namespace Solar.Captcha.Tests;

[TestFixture]
public class CaptchaImageRendererTests
{
    private const string TestCaptchaCode = "ABC123";

    /// <summary>Every character the static glyph source has an authored bitmap for.</summary>
    private const string TestCharset = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";

    private ICaptchaImageRenderer _renderer;

    [SetUp]
    public void SetUp() => _renderer = CreateRenderer();

    private static GlyphSet CreateGlyphSet(string charset, byte[] fallbackGlyph = null) =>
        new GlyphSetFactory(new StaticGlyphRenderer())
            .Get(new GlyphSetOptions { Charset = charset, FallbackGlyph = fallbackGlyph });

    private static ICaptchaImageRenderer CreateRenderer(
        Random random = null,
        string charset = TestCharset,
        byte[] fallbackGlyph = null) =>
        new CaptchaImageRenderer(CreateGlyphSet(charset, fallbackGlyph), random ?? Random.Shared);

    [Test]
    public void Render_WithValidParameters_ReturnsValidPngBytes()
    {
        // Arrange
        const int width = 200;
        const int height = 100;
        const string captchaCode = "TEST123";

        // Act
        var result = _renderer.Render(width, height, captchaCode);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Length, Is.GreaterThan(0));
    }

    [Test]
    public void Render_WithMinimumValidSize_ReturnsValidResult()
    {
        // Act
        var result = _renderer.Render(1, 1, TestCaptchaCode);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Length, Is.GreaterThan(0));
    }

    [Test]
    public void Render_WithLargeSize_ReturnsValidResult()
    {
        // Act
        var result = _renderer.Render(1000, 500, TestCaptchaCode);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Length, Is.GreaterThan(0));
    }

    [Test]
    public void Render_WithDifferentFontStyles_ReturnsValidResults()
    {
        // Arrange
        var fontStyles = new[] { CaptchaFontStyle.Regular, CaptchaFontStyle.Bold, CaptchaFontStyle.Italic };

        foreach (var fontStyle in fontStyles)
        {
            // Act
            var result = _renderer.Render(200, 100, TestCaptchaCode, fontStyle);

            // Assert
            Assert.That(result, Is.Not.Null, $"Failed for font style: {fontStyle}");
            Assert.That(result.Length, Is.GreaterThan(0));
        }
    }

    [Test]
    public void Render_WithDrawLinesTrue_ReturnsValidResult()
    {
        // Act
        var result = _renderer.Render(200, 100, TestCaptchaCode, CaptchaFontStyle.Regular, true);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Length, Is.GreaterThan(0));
    }

    [Test]
    public void Render_WithDrawLinesFalse_ReturnsValidResult()
    {
        // Act
        var result = _renderer.Render(200, 100, TestCaptchaCode, CaptchaFontStyle.Regular, false);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Length, Is.GreaterThan(0));
    }

    [Test]
    public void Render_WithSingleCharacter_ReturnsValidResult()
    {
        // Act
        var result = _renderer.Render(100, 50, "A");

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Length, Is.GreaterThan(0));
    }

    [Test]
    public void Render_WithLongCaptchaCode_ReturnsValidResult()
    {
        // Arrange
        const string longCode = "ABCDEFGHIJKLMNOPQRSTUVWXYZ123456789";

        // Act
        var result = _renderer.Render(800, 100, longCode);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Length, Is.GreaterThan(0));
    }

    [Test]
    public void Render_WithNumbers_ReturnsValidResult()
    {
        // Act
        var result = _renderer.Render(300, 100, "1234567890");

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Length, Is.GreaterThan(0));
    }

    [Test]
    public void Render_WithLowercaseCode_ReturnsValidResult()
    {
        // Arrange - the glyph set is declared in upper case, and lookup ignores case, so a
        // lower-case code must draw the upper-case glyphs rather than fail.
        const string lowerCaseCode = "abc123";

        // Act
        var result = _renderer.Render(300, 100, lowerCaseCode);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Length, Is.GreaterThan(0));
    }

    [Test]
    public void Render_GeneratesValidPngImage()
    {
        // Act
        var result = _renderer.Render(200, 100, TestCaptchaCode);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Length, Is.GreaterThan(24));

        // PNG signature: 137 80 78 71 13 10 26 10
        Assert.That(result[0], Is.EqualTo(0x89));
        Assert.That(result[1], Is.EqualTo(0x50)); // 'P'
        Assert.That(result[2], Is.EqualTo(0x4E)); // 'N'
        Assert.That(result[3], Is.EqualTo(0x47)); // 'G'

        // Read width and height from IHDR chunk (bytes 16-23, big-endian)
        var pngWidth = (result[16] << 24) | (result[17] << 16) | (result[18] << 8) | result[19];
        var pngHeight = (result[20] << 24) | (result[21] << 16) | (result[22] << 8) | result[23];
        Assert.That(pngWidth, Is.EqualTo(200));
        Assert.That(pngHeight, Is.EqualTo(100));
    }

    [Test]
    public void Render_DifferentSizes_GenerateCorrectDimensions()
    {
        // Arrange
        var sizes = new[] { (100, 50), (300, 150), (500, 200) };

        foreach (var (width, height) in sizes)
        {
            // Act
            var result = _renderer.Render(width, height, TestCaptchaCode);

            // Assert - Parse PNG IHDR to verify dimensions
            var pngWidth = (result[16] << 24) | (result[17] << 16) | (result[18] << 8) | result[19];
            var pngHeight = (result[20] << 24) | (result[21] << 16) | (result[22] << 8) | result[23];
            Assert.That(pngWidth, Is.EqualTo(width), $"Width mismatch for size {width}x{height}");
            Assert.That(pngHeight, Is.EqualTo(height), $"Height mismatch for size {width}x{height}");
        }
    }

    [Test]
    public void Render_ConsecutiveCalls_GenerateDifferentImages()
    {
        // Act
        var result1 = _renderer.Render(200, 100, TestCaptchaCode);
        var result2 = _renderer.Render(200, 100, TestCaptchaCode);

        // Assert
        Assert.That(result1, Is.Not.EqualTo(result2),
            "Consecutive calls should generate different images due to randomization");
    }

    #region Charset Enforcement

    [Test]
    public void Render_WithCharacterOutsideCharset_ThrowsArgumentException()
    {
        // Arrange - none of these characters were authored as glyphs
        const string specialChars = "!@#$%^&*()";

        // Act
        var ex = Assert.Throws<ArgumentException>(() => _renderer.Render(300, 100, specialChars));

        // Assert
        Assert.That(ex.ParamName, Is.EqualTo("captchaCode"));
        Assert.That(ex.Message, Does.Contain("cannot draw"));
        Assert.That(ex.Message, Does.Contain(TestCharset));
    }

    [Test]
    public void Render_WithNonBmpCharacter_ThrowsArgumentException()
    {
        // Arrange - an emoji is outside the Basic Multilingual Plane and has no single-char glyph
        const string nonBmp = "A\uD83D\uDE00";

        // Act
        var ex = Assert.Throws<ArgumentException>(() => _renderer.Render(300, 100, nonBmp));

        // Assert
        Assert.That(ex.ParamName, Is.EqualTo("captchaCode"));
        Assert.That(ex.Message, Does.Contain("cannot draw"));
    }

    [Test]
    public void Render_WithFallbackGlyphConfigured_DrawsUnresolvableCharacter()
    {
        // Arrange - '!' has no authored glyph, so it can only be drawn via the fallback
        var renderer = CreateRenderer(charset: "ABC!", fallbackGlyph: new byte[GlyphRenderOptions.GlyphHeight]);

        // Act
        var result = renderer.Render(100, 50, "!!!");

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Length, Is.GreaterThan(0));
    }

    #endregion

    #region Parameter Validation Tests

    [Test]
    public void Render_WithZeroWidth_ThrowsArgumentOutOfRangeException()
    {
        // Act & Assert
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => _renderer.Render(0, 100, TestCaptchaCode));

        Assert.That(ex.ParamName, Is.EqualTo("width"));
    }

    [Test]
    public void Render_WithNegativeWidth_ThrowsArgumentOutOfRangeException()
    {
        // Act & Assert
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => _renderer.Render(-1, 100, TestCaptchaCode));

        Assert.That(ex.ParamName, Is.EqualTo("width"));
    }

    [Test]
    public void Render_WithZeroHeight_ThrowsArgumentOutOfRangeException()
    {
        // Act & Assert
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => _renderer.Render(100, 0, TestCaptchaCode));

        Assert.That(ex.ParamName, Is.EqualTo("height"));
    }

    [Test]
    public void Render_WithNegativeHeight_ThrowsArgumentOutOfRangeException()
    {
        // Act & Assert
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => _renderer.Render(100, -1, TestCaptchaCode));

        Assert.That(ex.ParamName, Is.EqualTo("height"));
    }

    [Test]
    public void Render_WithEmptyCaptchaCode_ThrowsArgumentException()
    {
        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => _renderer.Render(100, 100, ""));

        Assert.That(ex.ParamName, Is.EqualTo("captchaCode"));
    }

    [Test]
    public void Render_WithWhitespaceCaptchaCode_ThrowsArgumentException()
    {
        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => _renderer.Render(100, 100, "   "));

        Assert.That(ex.ParamName, Is.EqualTo("captchaCode"));
    }

    #endregion

    #region Edge Cases and Robustness Tests

    [Test]
    public void Render_WithVerySmallSize_HandlesGracefully()
    {
        // Act & Assert - Should not throw, even if image quality is poor
        Assert.DoesNotThrow(() =>
        {
            var result = _renderer.Render(10, 10, "A");
            Assert.That(result, Is.Not.Null);
        });
    }

    [Test]
    public void Render_WithExtremeAspectRatio_HandlesGracefully()
    {
        // Test very wide image
        Assert.DoesNotThrow(() =>
        {
            var result = _renderer.Render(1000, 10, TestCaptchaCode);
            Assert.That(result, Is.Not.Null);
        });

        // Test very tall image
        Assert.DoesNotThrow(() =>
        {
            var result = _renderer.Render(10, 1000, TestCaptchaCode);
            Assert.That(result, Is.Not.Null);
        });
    }

    [Test]
    public void Render_WithSameSeedAndCode_ProducesIdenticalImages()
    {
        // Arrange - the injected random source is the only non-determinism, so a seeded renderer
        // must be reproducible. This is what the injected Random buys over Random.Shared.
        var first = CreateRenderer(new Random(12345));
        var second = CreateRenderer(new Random(12345));

        // Act
        var result1 = first.Render(200, 100, TestCaptchaCode);
        var result2 = second.Render(200, 100, TestCaptchaCode);

        // Assert
        Assert.That(result1, Is.EqualTo(result2));
    }

    [Test]
    public void Render_WithDifferentCodes_ProducesDifferentImages()
    {
        // Arrange - same seed, so any difference must come from the glyphs being drawn
        var renderer = CreateRenderer(new Random(12345));

        // Act
        var result1 = renderer.Render(200, 100, "AAA", CaptchaFontStyle.Regular, false);
        var result2 = renderer.Render(200, 100, "BBB", CaptchaFontStyle.Regular, false);

        // Assert
        Assert.That(result1, Is.Not.EqualTo(result2));
    }

    [Test]
    public void Render_MultipleThreadsSimultaneously_WorksCorrectly()
    {
        // Arrange - a single renderer instance is shared, mirroring its singleton registration
        const int threadCount = 10;
        var results = new byte[threadCount][];
        var tasks = new Task[threadCount];

        // Act
        for (int i = 0; i < threadCount; i++)
        {
            int index = i;
            tasks[i] = Task.Run(() =>
            {
                results[index] = _renderer.Render(200, 100, $"TEST{index}");
            });
        }

        Task.WaitAll(tasks);

        // Assert
        for (int i = 0; i < threadCount; i++)
        {
            Assert.That(results[i], Is.Not.Null, $"Result {i} should not be null");
            Assert.That(results[i].Length, Is.GreaterThan(0), $"Result {i} should have byte data");
        }

        // Verify all results are different (due to randomization)
        for (int i = 0; i < threadCount - 1; i++)
        {
            for (int j = i + 1; j < threadCount; j++)
            {
                Assert.That(results[i], Is.Not.EqualTo(results[j]),
                    $"Results {i} and {j} should be different due to randomization");
            }
        }
    }

    #endregion
}
