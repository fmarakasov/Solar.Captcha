using System;
using System.IO;
using NUnit.Framework;
using Solar.Captcha.Raster;

namespace Solar.Captcha.Tests.Raster;

[TestFixture]
public class RasterFontTests
{
    private string _testFontPath = null!;

    [SetUp]
    public void SetUp()
    {
        _testFontPath = Path.Combine(TestContext.CurrentContext.TestDirectory, "Fonts", "arial.ttf");
        Assert.That(File.Exists(_testFontPath), Is.True, $"Test font not found at '{_testFontPath}'.");
    }

    [Test]
    public void FromFile_ReadsFamilyName()
    {
        var font = RasterFont.FromFile(_testFontPath);

        Assert.That(font.FamilyName, Is.EqualTo("Arial"));
    }

    [Test]
    public void FromStream_ReadsIdenticalFont()
    {
        var fromFile = RasterFont.FromFile(_testFontPath);
        var fromStream = RasterFont.FromStream(() => File.OpenRead(_testFontPath));

        Assert.Multiple(() =>
        {
            Assert.That(fromStream.FamilyName, Is.EqualTo(fromFile.FamilyName));
            Assert.That(fromStream.UnitsPerEm, Is.EqualTo(fromFile.UnitsPerEm));
        });
    }

    [Test]
    public void MeasureText_GrowsWithEmSize()
    {
        var font = RasterFont.FromFile(_testFontPath);

        var small = font.MeasureText("12", 10f);
        var large = font.MeasureText("12", 20f);

        Assert.That(large, Is.GreaterThan(small * 1.8f));
    }

    [Test]
    public void MeasureText_SumsAdvances()
    {
        var font = RasterFont.FromFile(_testFontPath);

        var advance1 = font.GetAdvance('1', 15f);
        var advance2 = font.GetAdvance('2', 15f);
        var combined = font.MeasureText("12", 15f);

        Assert.That(combined, Is.EqualTo(advance1 + advance2).Within(0.01f));
    }

    [TestCase('3')]
    [TestCase('6')]
    [TestCase('9')]
    [TestCase('1')]
    [TestCase('2')]
    public void TryGetPlacement_ForDialDigits_ProducesValidNonEmptyMask(char digit)
    {
        var font = RasterFont.FromFile(_testFontPath);

        var ok = font.TryGetPlacement(digit, 15f, out var placement);

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.True);
            Assert.That(placement.Width, Is.GreaterThan(0));
            Assert.That(placement.Height, Is.GreaterThan(0));
            Assert.That(placement.Advance, Is.GreaterThan(0));
            Assert.That(placement.Coverage, Does.Contain((byte)1), "The mask must contain set pixels.");
        });
    }

    [Test]
    public void TryGetPlacement_ForUnmappedCharacter_ReturnsFalse()
    {
        var font = RasterFont.FromFile(_testFontPath);

        // U+E000 is Private Use Area, unmapped in standard Arial.
        var ok = font.TryGetPlacement('\uE000', 15f, out _);

        Assert.That(ok, Is.False);
    }

    [Test]
    public void DrawText_OntoCanvas_PaintsExpectedPixels()
    {
        var font = RasterFont.FromFile(_testFontPath);
        var canvas = new RasterCanvas(40, 20);
        canvas.Clear(new RasterColor(0, 0, 0));

        canvas.DrawText("12", font, 15f, new RasterColor(255, 255, 255), 2f, 15f);

        var setPixels = 0;
        for (var y = 0; y < canvas.Height; y++)
        {
            for (var x = 0; x < canvas.Width; x++)
            {
                if (canvas.GetPixel(x, y).R == 255)
                {
                    setPixels++;
                }
            }
        }

        Assert.That(setPixels, Is.GreaterThan(10), "Drawing '12' must paint at least some white pixels.");
    }
}
