using System;
using System.IO;
using NUnit.Framework;
using Solar.Captcha.Raster;
using Solar.Captcha.Tests.TestSupport;

namespace Solar.Captcha.Tests.Raster;

[TestFixture]
public class LzwEncoderTests
{
    /// <summary>Encodes indices and decodes them again, proving the byte stream is readable.</summary>
    private static byte[] RoundTrip(byte[] indices, int minimumCodeSize)
    {
        using var buffer = new MemoryStream();
        LzwEncoder.Encode(buffer, indices, minimumCodeSize);

        // The encoder frames its payload as GIF data sub-blocks; the decoder wants the payload.
        return LzwDecoder.Decode(GifSubBlocks.Unwrap(buffer.ToArray()), minimumCodeSize, indices.Length);
    }

    [Test]
    public void Encode_WithSingleIndex_ProducesDecodableStream()
    {
        byte[] indices = [0];

        var decoded = RoundTrip(indices, 2);

        Assert.That(decoded, Is.EqualTo(indices));
    }

    [Test]
    public void Encode_WithEmptyInput_WritesOnlyATerminatingBlock()
    {
        using var buffer = new MemoryStream();

        LzwEncoder.Encode(buffer, ReadOnlySpan<byte>.Empty, 2);

        // A chain of zero sub-blocks: the single terminator, and nothing else.
        Assert.That(buffer.ToArray(), Is.EqualTo(new byte[] { 0 }));
    }

    [TestCase(2)]
    [TestCase(3)]
    [TestCase(4)]
    [TestCase(6)]
    [TestCase(8)]
    public void Encode_WithRandomIndices_RoundTripsExactly(int bitsPerIndex)
    {
        var alphabet = 1 << bitsPerIndex;
        var random = new Random(20260926);
        var indices = new byte[4096];
        for (var i = 0; i < indices.Length; i++)
        {
            indices[i] = (byte)random.Next(alphabet);
        }

        var decoded = RoundTrip(indices, bitsPerIndex);

        Assert.That(decoded, Is.EqualTo(indices));
    }

    [Test]
    public void Encode_WithFlatImage_RoundTripsExactly()
    {
        // A flat run exercises the longest-repeated-string path rather than dictionary churn.
        var indices = new byte[10000];
        Array.Fill(indices, (byte)7);

        var decoded = RoundTrip(indices, 4);

        Assert.That(decoded, Is.EqualTo(indices));
    }

    [Test]
    public void Encode_WithGradientLikeRamp_RoundTripsExactly()
    {
        var indices = new byte[20000];
        for (var i = 0; i < indices.Length; i++)
        {
            indices[i] = (byte)(i % 256);
        }

        var decoded = RoundTrip(indices, 8);

        Assert.That(decoded, Is.EqualTo(indices));
    }

    [Test]
    public void Encode_WhenDictionaryFills_ResetsAndStillRoundTrips()
    {
        // With a four-colour palette the dictionary reaches its 4096-entry ceiling quickly, so this
        // input forces at least one Clear code and a code-width reset. A decoder that mishandles the
        // reset desynchronises and the comparison below fails loudly.
        var random = new Random(42);
        var indices = new byte[60000];
        for (var i = 0; i < indices.Length; i++)
        {
            indices[i] = (byte)random.Next(4);
        }

        var decoded = RoundTrip(indices, 2);

        Assert.That(decoded, Is.EqualTo(indices));
    }

    [Test]
    public void Encode_WithMinimumCodeSizeBelowTwo_Throws()
    {
        using var buffer = new MemoryStream();

        Assert.Throws<ArgumentOutOfRangeException>(
            () => LzwEncoder.Encode(buffer, new byte[] { 0 }, 1));
    }

    [Test]
    public void Encode_WithMinimumCodeSizeAboveEight_Throws()
    {
        using var buffer = new MemoryStream();

        Assert.Throws<ArgumentOutOfRangeException>(
            () => LzwEncoder.Encode(buffer, new byte[] { 0 }, 9));
    }

    [Test]
    public void Encode_WithNullStream_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => LzwEncoder.Encode(null!, new byte[] { 0 }, 2));
    }
}
