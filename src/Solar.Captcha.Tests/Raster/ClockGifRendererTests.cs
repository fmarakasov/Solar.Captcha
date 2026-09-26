using System;
using System.IO;
using NUnit.Framework;
using Solar.Captcha.Raster;
using Solar.Captcha.Tests.TestSupport;

namespace Solar.Captcha.Tests.Raster;

[TestFixture]
public class ClockGifRendererTests
{
    private string _testFontPath = null!;
    private RasterFont _font = null!;

    [SetUp]
    public void SetUp()
    {
        _testFontPath = Path.Combine(TestContext.CurrentContext.TestDirectory, "Fonts", "arial.ttf");
        _font = RasterFont.FromFile(_testFontPath);
    }

    private ClockRenderOptions CreateOptions() => new()
    {
        Width = 282,
        Height = 240,
        ClockRadius = 81,
        ClockThickness = 6,
        ClockDashThickness = 4,
        ClockDashMargin = 8,
        ClockDashLength = 13,
        HandsThickness = 2,
        HourHandLength = 32,
        MinuteHandLength = 45,
        GifFrameDelay = 100,
        FontPixelSize = 15f,
        HandColor = RasterColor.ParseHex("0D2C74"),
        ClockColor = RasterColor.ParseHex("3970F3"),
        BackgroundColor = RasterColor.ParseHex("EEF6FF"),
        GradientColor1 = RasterColor.ParseHex("E0FAF2"),
        GradientColor2 = RasterColor.ParseHex("BFCFF8"),
        Font = _font,
    };

    [Test]
    public void Render_ProducesValidAnimatedGifWithSixtyFrames()
    {
        var options = CreateOptions();
        var renderer = new ClockGifRenderer(new Random(20260926));

        var bytes = renderer.Render(options, hours: 3, minutes: 45);

        Assert.That(bytes, Is.Not.Null);
        Assert.That(bytes.Length, Is.GreaterThan(1000));

        var gif = GifImage.Parse(bytes);

        Assert.Multiple(() =>
        {
            Assert.That(gif.Width, Is.EqualTo(282));
            Assert.That(gif.Height, Is.EqualTo(240));
            Assert.That(gif.Frames, Has.Count.EqualTo(60));
            Assert.That(gif.Frames[0].DelayCentiseconds, Is.EqualTo(100));
            Assert.That(gif.RepeatCount, Is.EqualTo(0), "Must loop infinitely.");
        });
    }

    [Test]
    public void Render_SecondHandMovesBetweenFrames()
    {
        var options = CreateOptions();
        var renderer = new ClockGifRenderer(new Random(42));

        var bytes = renderer.Render(options, hours: 12, minutes: 0);
        var gif = GifImage.Parse(bytes);

        // Frame 0 second hand is at 12 o'clock; Frame 15 is at 3 o'clock.
        var frame0 = gif.Frames[0];
        var frame15 = gif.Frames[15];

        var differences = 0;
        for (var i = 0; i < frame0.Indices.Length; i++)
        {
            if (frame0.Indices[i] != frame15.Indices[i])
            {
                differences++;
            }
        }

        Assert.That(differences, Is.GreaterThan(10), "Second hand must produce visibly different frames.");
    }

    [TestCase(-1, 30)]
    [TestCase(13, 30)]
    [TestCase(6, -1)]
    [TestCase(6, 60)]
    public void Render_WithInvalidTime_Throws(int hours, int minutes)
    {
        var options = CreateOptions();
        var renderer = new ClockGifRenderer();

        Assert.Throws<ArgumentOutOfRangeException>(
            () => renderer.Render(options, hours, minutes));
    }

    [Test]
    [Explicit("Generates a sample GIF on disk for eyeball verification.")]
    public void GenerateSampleClockGif()
    {
        var options = CreateOptions();
        var renderer = new ClockGifRenderer(new Random(1));

        var bytes = renderer.Render(options, hours: 10, minutes: 10);

        var artifacts = Path.Combine(TestContext.CurrentContext.TestDirectory, "artifacts");
        Directory.CreateDirectory(artifacts);
        var path = Path.Combine(artifacts, "clock-sample.gif");
        File.WriteAllBytes(path, bytes);

        TestContext.Out.WriteLine($"Sample GIF written to: {Path.GetFullPath(path)} ({bytes.Length} bytes)");
        Assert.That(File.Exists(path), Is.True);
    }
}
