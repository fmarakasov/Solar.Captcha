using System;
using NUnit.Framework;
using Solar.Captcha.Raster;

namespace Solar.Captcha.Tests.Raster;

[TestFixture]
public class RasterColorTests
{
    [Test]
    public void ParseHex_WithSixDigits_ReadsOpaqueColor()
    {
        var color = RasterColor.ParseHex("0D2C74");

        Assert.Multiple(() =>
        {
            Assert.That(color.R, Is.EqualTo(0x0D));
            Assert.That(color.G, Is.EqualTo(0x2C));
            Assert.That(color.B, Is.EqualTo(0x74));
            Assert.That(color.A, Is.EqualTo(255));
        });
    }

    [Test]
    public void ParseHex_WithLeadingHash_IsAccepted()
    {
        var color = RasterColor.ParseHex("#EEF6FF");

        Assert.That(color, Is.EqualTo(new RasterColor(0xEE, 0xF6, 0xFF)));
    }

    [Test]
    public void ParseHex_WithThreeDigits_DuplicatesEachDigit()
    {
        var color = RasterColor.ParseHex("#0AF");

        Assert.That(color, Is.EqualTo(new RasterColor(0x00, 0xAA, 0xFF)));
    }

    [Test]
    public void ParseHex_WithEightDigits_ReadsAlpha()
    {
        var color = RasterColor.ParseHex("#11223344");

        Assert.That(color, Is.EqualTo(new RasterColor(0x11, 0x22, 0x33, 0x44)));
    }

    [Test]
    public void ParseHex_WithLowerAndUpperCase_Agree()
    {
        Assert.That(RasterColor.ParseHex("abcdef"), Is.EqualTo(RasterColor.ParseHex("ABCDEF")));
    }

    [TestCase("")]
    [TestCase("   ")]
    [TestCase("12")]
    [TestCase("12345")]
    [TestCase("1234567")]
    [TestCase("123456789")]
    [TestCase("GGGGGG")]
    public void ParseHex_WithInvalidValue_Throws(string value)
    {
        Assert.Throws<ArgumentException>(() => RasterColor.ParseHex(value));
    }

    [Test]
    public void ParseHex_WithNull_Throws()
    {
        Assert.Throws<ArgumentException>(() => RasterColor.ParseHex(null!));
    }

    [Test]
    public void Equality_WithSameChannels_IsEqual()
    {
        var left = new RasterColor(1, 2, 3, 4);
        var right = new RasterColor(1, 2, 3, 4);

        Assert.Multiple(() =>
        {
            Assert.That(left == right, Is.True);
            Assert.That(left != right, Is.False);
            Assert.That(left.GetHashCode(), Is.EqualTo(right.GetHashCode()));
            Assert.That(left.Equals((object)right), Is.True);
        });
    }

    [Test]
    public void Equality_WithDifferentAlpha_IsNotEqual()
    {
        Assert.That(new RasterColor(1, 2, 3, 255) == new RasterColor(1, 2, 3, 254), Is.False);
    }
}
