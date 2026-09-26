using System;
using System.IO;
using NUnit.Framework;
using Solar.Captcha.Raster;
using Solar.Captcha.Tests.TestSupport;

namespace Solar.Captcha.Tests.Raster;

[TestFixture]
public class GifEncoderTests
{
    private static readonly RasterColor Red = new(255, 0, 0);
    private static readonly RasterColor Blue = new(0, 0, 255);
    private static readonly RasterColor Green = new(0, 255, 0);

    [Test]
    public void Encode_SingleFrame_ProducesValidGif()
    {
        var canvas = new RasterCanvas(10, 8);
        canvas.Clear(Red);

        using var stream = new MemoryStream();
        var encoder = GifEncoder.Begin(stream, 10, 8, delayCentiseconds: 50);
        encoder.WriteFrame(canvas);
        encoder.End();

        var gif = GifImage.Parse(stream.ToArray());

        Assert.Multiple(() =>
        {
            Assert.That(gif.Width, Is.EqualTo(10));
            Assert.That(gif.Height, Is.EqualTo(8));
            Assert.That(gif.Frames, Has.Count.EqualTo(1));
            Assert.That(gif.Frames[0].DelayCentiseconds, Is.EqualTo(50));
            Assert.That(gif.Frames[0].Width, Is.EqualTo(10));
            Assert.That(gif.Frames[0].Height, Is.EqualTo(8));
        });
    }

    [Test]
    public void Encode_MultiFrame_RecordsEachFrameWithCorrectDelay()
    {
        var frame1 = new RasterCanvas(4, 4);
        frame1.Clear(Red);

        var frame2 = new RasterCanvas(4, 4);
        frame2.Clear(Blue);

        using var stream = new MemoryStream();
        var encoder = GifEncoder.Begin(stream, 4, 4, delayCentiseconds: 100);
        encoder.WriteFrame(frame1);
        encoder.WriteFrame(frame2);
        encoder.End();

        var gif = GifImage.Parse(stream.ToArray());

        Assert.Multiple(() =>
        {
            Assert.That(gif.Frames, Has.Count.EqualTo(2));
            Assert.That(gif.Frames[0].DelayCentiseconds, Is.EqualTo(100));
            Assert.That(gif.Frames[1].DelayCentiseconds, Is.EqualTo(100));
        });
    }

    [Test]
    public void Encode_WithSeedColors_GuaranteesSeedColorsSurviveReduction()
    {
        var canvas = new RasterCanvas(300, 1);
        // Paint a gradient with more than 256 colours, which forces the palette to sample.
        canvas.FillLinearGradient(new LinearGradientBrush(
            new PointF(0, 0),
            new PointF(299, 0),
            new RasterColor(0, 0, 0),
            new RasterColor(255, 255, 255)));

        using var stream = new MemoryStream();
        // Seed green, which is absent from the black-to-white gradient.
        var encoder = GifEncoder.Begin(
            stream,
            300,
            1,
            delayCentiseconds: 10,
            seedColors: [Green]);
        encoder.WriteFrame(canvas);
        encoder.End();

        var gif = GifImage.Parse(stream.ToArray());

        var foundGreen = false;
        for (var i = 0; i < gif.GlobalColorTable.Length / 3; i++)
        {
            var (r, g, b) = gif.ColorAt((byte)i);
            if (r == Green.R && g == Green.G && b == Green.B)
            {
                foundGreen = true;
                break;
            }
        }

        Assert.That(foundGreen, Is.True, "The seeded stroke colour must survive colour-table sampling.");
    }

    [Test]
    public void Encode_WithoutFrames_ThrowsOnEnd()
    {
        using var stream = new MemoryStream();
        var encoder = GifEncoder.Begin(stream, 4, 4, 10);

        Assert.Throws<InvalidOperationException>(() => encoder.End());
    }

    [Test]
    public void WriteFrame_AfterEnd_Throws()
    {
        var canvas = new RasterCanvas(2, 2);
        canvas.Clear(Red);

        using var stream = new MemoryStream();
        var encoder = GifEncoder.Begin(stream, 2, 2, 10);
        encoder.WriteFrame(canvas);
        encoder.End();

        Assert.Throws<InvalidOperationException>(() => encoder.WriteFrame(canvas));
    }

    [Test]
    public void WriteFrame_WithMismatchedDimensions_Throws()
    {
        using var stream = new MemoryStream();
        var encoder = GifEncoder.Begin(stream, 10, 10, 10);

        Assert.Throws<ArgumentException>(() => encoder.WriteFrame(new RasterCanvas(5, 5)));
    }
}
