using System;
using NUnit.Framework;
using Solar.Captcha.Raster;

namespace Solar.Captcha.Tests.Raster;

[TestFixture]
public class RasterCanvasTests
{
    private static readonly RasterColor Red = new(255, 0, 0);
    private static readonly RasterColor Blue = new(0, 0, 255);

    [TestCase(0, 10)]
    [TestCase(10, 0)]
    [TestCase(-1, 10)]
    public void Constructor_WithNonPositiveDimension_Throws(int width, int height)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new RasterCanvas(width, height));
    }

    [Test]
    public void Clear_SetsEveryPixel()
    {
        var canvas = new RasterCanvas(3, 2);

        canvas.Clear(Red);

        Assert.Multiple(() =>
        {
            Assert.That(canvas.GetPixel(0, 0), Is.EqualTo(Red));
            Assert.That(canvas.GetPixel(2, 1), Is.EqualTo(Red));
        });
    }

    [Test]
    public void Clone_IsIndependentOfTheOriginal()
    {
        var original = new RasterCanvas(2, 2);
        original.Clear(Red);

        var clone = original.Clone();
        clone.SetPixel(0, 0, Blue);

        Assert.Multiple(() =>
        {
            Assert.That(clone.GetPixel(0, 0), Is.EqualTo(Blue));
            Assert.That(original.GetPixel(0, 0), Is.EqualTo(Red));
            Assert.That(clone.Width, Is.EqualTo(original.Width));
            Assert.That(clone.Height, Is.EqualTo(original.Height));
        });
    }

    [Test]
    public void GetPixel_OutsideCanvas_ReturnsTransparent()
    {
        var canvas = new RasterCanvas(2, 2);

        Assert.That(canvas.GetPixel(5, 5), Is.EqualTo(RasterColor.Transparent));
    }

    [Test]
    public void SetPixel_OutsideCanvas_IsIgnored()
    {
        var canvas = new RasterCanvas(2, 2);
        canvas.Clear(Red);

        canvas.SetPixel(-1, 0, Blue);
        canvas.SetPixel(2, 0, Blue);

        Assert.That(canvas.GetPixel(0, 0), Is.EqualTo(Red));
    }

    [Test]
    public void SetPixel_WithHalfAlpha_BlendsTowardsTheDestination()
    {
        var canvas = new RasterCanvas(1, 1);
        canvas.Clear(new RasterColor(0, 0, 0));

        canvas.SetPixel(0, 0, new RasterColor(255, 255, 255, 128));

        var blended = canvas.GetPixel(0, 0);
        Assert.Multiple(() =>
        {
            Assert.That(blended.R, Is.EqualTo(128));
            Assert.That(blended.A, Is.EqualTo(255));
        });
    }

    [Test]
    public void SetPixel_WithZeroAlpha_LeavesPixelUntouched()
    {
        var canvas = new RasterCanvas(1, 1);
        canvas.Clear(Red);

        canvas.SetPixel(0, 0, new RasterColor(0, 255, 0, 0));

        Assert.That(canvas.GetPixel(0, 0), Is.EqualTo(Red));
    }

    [Test]
    public void FillLinearGradient_SetsBothStopColorsAtTheirEnds()
    {
        var canvas = new RasterCanvas(11, 1);

        canvas.FillLinearGradient(new LinearGradientBrush(
            new PointF(0, 0),
            new PointF(10, 0),
            Red,
            Blue));

        Assert.Multiple(() =>
        {
            Assert.That(canvas.GetPixel(0, 0), Is.EqualTo(Red));
            Assert.That(canvas.GetPixel(10, 0), Is.EqualTo(Blue));
        });
    }

    [Test]
    public void FillLinearGradient_InterpolatesBetweenStops()
    {
        var canvas = new RasterCanvas(11, 1);

        canvas.FillLinearGradient(new LinearGradientBrush(
            new PointF(0, 0),
            new PointF(10, 0),
            new RasterColor(0, 0, 0),
            new RasterColor(100, 0, 0)));

        Assert.That(canvas.GetPixel(5, 0).R, Is.EqualTo(50));
    }

    [Test]
    public void FillLinearGradient_WithCoincidentPoints_Throws()
    {
        Assert.Throws<ArgumentException>(() => new LinearGradientBrush(
            new PointF(1, 1),
            new PointF(1, 1),
            Red,
            Blue));
    }

    [Test]
    public void ColorAt_WithoutRepetition_ClampsOutsideTheAxis()
    {
        var brush = new LinearGradientBrush(new PointF(0, 0), new PointF(1, 0), Red, Blue);

        Assert.Multiple(() =>
        {
            Assert.That(brush.ColorAt(-1f), Is.EqualTo(Red));
            Assert.That(brush.ColorAt(2f), Is.EqualTo(Blue));
        });
    }

    [Test]
    public void ColorAt_WithReflectRepetition_MirrorsTheRamp()
    {
        var brush = new LinearGradientBrush(
            new PointF(0, 0),
            new PointF(1, 0),
            Red,
            Blue,
            GradientRepetition.Reflect);

        // t = 1.5 reflects back to 0.5, the midpoint of the ramp.
        var reflected = brush.ColorAt(1.5f);

        Assert.Multiple(() =>
        {
            Assert.That(reflected.R, Is.EqualTo(128));
            Assert.That(reflected.B, Is.EqualTo(128));
        });
    }

    [Test]
    public void DrawLine_WithSinglePixelPen_PaintsOnePixelRow()
    {
        var canvas = new RasterCanvas(5, 5);
        canvas.Clear(new RasterColor(0, 0, 0));

        canvas.DrawLine(new PointF(0, 2), new PointF(4, 2), new RasterPen(Red, 1));

        Assert.Multiple(() =>
        {
            for (var x = 0; x < 5; x++)
            {
                Assert.That(canvas.GetPixel(x, 2), Is.EqualTo(Red), $"pixel {x}");
            }

            Assert.That(canvas.GetPixel(2, 1), Is.EqualTo(new RasterColor(0, 0, 0)));
            Assert.That(canvas.GetPixel(2, 3), Is.EqualTo(new RasterColor(0, 0, 0)));
        });
    }

    [Test]
    public void DrawLine_WithWidePen_PaintsMultipleRows()
    {
        var canvas = new RasterCanvas(9, 9);
        canvas.Clear(new RasterColor(0, 0, 0));

        canvas.DrawLine(new PointF(0, 4), new PointF(8, 4), new RasterPen(Red, 5));

        Assert.Multiple(() =>
        {
            Assert.That(canvas.GetPixel(4, 2), Is.EqualTo(Red));
            Assert.That(canvas.GetPixel(4, 4), Is.EqualTo(Red));
            Assert.That(canvas.GetPixel(4, 6), Is.EqualTo(Red));
        });
    }

    [Test]
    public void DrawLine_WithZeroLength_StampsADot()
    {
        var canvas = new RasterCanvas(3, 3);
        canvas.Clear(new RasterColor(0, 0, 0));

        canvas.DrawLine(new PointF(1, 1), new PointF(1, 1), new RasterPen(Red, 1));

        Assert.That(canvas.GetPixel(1, 1), Is.EqualTo(Red));
    }

    [Test]
    public void DrawLine_WithNullPen_Throws()
    {
        var canvas = new RasterCanvas(2, 2);

        Assert.Throws<ArgumentNullException>(() => canvas.DrawLine(new PointF(0, 0), new PointF(1, 1), null!));
    }

    [Test]
    public void StrokePolygon_ClosesTheShape()
    {
        var canvas = new RasterCanvas(10, 10);
        canvas.Clear(new RasterColor(0, 0, 0));

        // A triangle whose closing edge is the vertical run from (1,1) back to (0,0)'s neighbour.
        PointF[] triangle = [new(1, 1), new(8, 1), new(8, 8)];

        canvas.StrokePolygon(triangle, new RasterPen(Red, 1));

        Assert.Multiple(() =>
        {
            Assert.That(canvas.GetPixel(4, 1), Is.EqualTo(Red), "top edge");
            Assert.That(canvas.GetPixel(8, 4), Is.EqualTo(Red), "right edge");
            Assert.That(canvas.GetPixel(4, 4), Is.EqualTo(Red), "hypotenuse");
        });
    }

    [Test]
    public void StrokePolygon_WithFewerThanTwoPoints_DrawsNothing()
    {
        var canvas = new RasterCanvas(3, 3);
        canvas.Clear(new RasterColor(0, 0, 0));

        canvas.StrokePolygon([new PointF(1, 1)], new RasterPen(Red, 1));

        Assert.That(canvas.GetPixel(1, 1), Is.EqualTo(new RasterColor(0, 0, 0)));
    }

    [Test]
    public void FillCircle_PaintsInsideAndLeavesOutside()
    {
        var canvas = new RasterCanvas(11, 11);
        canvas.Clear(new RasterColor(0, 0, 0));

        canvas.FillCircle(new PointF(5, 5), 3f, Red);

        Assert.Multiple(() =>
        {
            Assert.That(canvas.GetPixel(5, 5), Is.EqualTo(Red));
            Assert.That(canvas.GetPixel(5, 7), Is.EqualTo(Red));
            Assert.That(canvas.GetPixel(0, 0), Is.EqualTo(new RasterColor(0, 0, 0)));
        });
    }

    [Test]
    public void FillCircle_WithNonPositiveRadius_Throws()
    {
        var canvas = new RasterCanvas(4, 4);

        Assert.Throws<ArgumentOutOfRangeException>(() => canvas.FillCircle(new PointF(1, 1), 0f, Red));
    }

    [Test]
    public void BlitMask_WritesOnlySetPixels()
    {
        var canvas = new RasterCanvas(4, 2);
        canvas.Clear(new RasterColor(0, 0, 0));

        // Two rows of two: only the diagonal is set.
        byte[] mask = [1, 0, 0, 1];
        canvas.BlitMask(mask, 2, 2, 1, 0, Red);

        Assert.Multiple(() =>
        {
            Assert.That(canvas.GetPixel(1, 0), Is.EqualTo(Red));
            Assert.That(canvas.GetPixel(2, 0), Is.EqualTo(new RasterColor(0, 0, 0)));
            Assert.That(canvas.GetPixel(1, 1), Is.EqualTo(new RasterColor(0, 0, 0)));
            Assert.That(canvas.GetPixel(2, 1), Is.EqualTo(Red));
        });
    }

    [Test]
    public void BlitMask_ClipsAtTheCanvasEdge()
    {
        var canvas = new RasterCanvas(2, 2);
        canvas.Clear(new RasterColor(0, 0, 0));

        byte[] mask = [1, 1, 1, 1];
        canvas.BlitMask(mask, 2, 2, 1, 1, Red);

        Assert.Multiple(() =>
        {
            Assert.That(canvas.GetPixel(1, 1), Is.EqualTo(Red));
            Assert.That(canvas.GetPixel(0, 0), Is.EqualTo(new RasterColor(0, 0, 0)));
        });
    }
}
