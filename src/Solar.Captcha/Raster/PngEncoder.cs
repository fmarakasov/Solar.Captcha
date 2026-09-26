using System;
using System.IO;
using System.IO.Compression;

namespace Solar.Captcha.Raster;

/// <summary>
/// Encodes an RGBA pixel buffer as a PNG image.
/// </summary>
/// <remarks>
/// The encoder is hand-written so the library needs no imaging dependency: it emits the PNG
/// signature, an <c>IHDR</c> chunk, a single <c>IDAT</c> chunk whose payload is a zlib stream built
/// around <see cref="DeflateStream"/>, and an <c>IEND</c> chunk. Rows use filter type 0 (None) and
/// colour type 6 (RGBA), eight bits per channel.
/// </remarks>
public static class PngEncoder
{
    private const int BytesPerPixel = 4;

    /// <summary>
    /// Encodes a canvas as a PNG image.
    /// </summary>
    /// <param name="canvas">The canvas to encode.</param>
    /// <returns>The PNG bytes.</returns>
    /// <exception cref="ArgumentNullException">When <paramref name="canvas"/> is <see langword="null"/>.</exception>
    public static byte[] Encode(RasterCanvas canvas)
    {
        ArgumentNullException.ThrowIfNull(canvas);
        return Encode(canvas.Pixels, canvas.Width, canvas.Height);
    }

    /// <summary>
    /// Encodes a raw RGBA buffer as a PNG image.
    /// </summary>
    /// <param name="rgba">The pixel buffer, row-major, four bytes per pixel.</param>
    /// <param name="width">The image width in pixels.</param>
    /// <param name="height">The image height in pixels.</param>
    /// <returns>The PNG bytes.</returns>
    /// <exception cref="ArgumentOutOfRangeException">When either dimension is not positive.</exception>
    /// <exception cref="ArgumentException">
    /// When <paramref name="rgba"/> does not hold exactly <c>width * height * 4</c> bytes.
    /// </exception>
    public static byte[] Encode(ReadOnlySpan<byte> rgba, int width, int height)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);

        var expected = width * height * BytesPerPixel;
        if (rgba.Length != expected)
        {
            throw new ArgumentException(
                $"A {width}x{height} image needs {expected} RGBA bytes, but the buffer holds {rgba.Length}.",
                nameof(rgba));
        }

        using var output = new MemoryStream();

        // PNG signature
        output.Write([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);

        WriteChunk(output, "IHDR"u8, writer =>
        {
            WriteBigEndianInt32(writer, width);
            WriteBigEndianInt32(writer, height);
            writer.WriteByte(8);  // bit depth
            writer.WriteByte(6);  // colour type: RGBA
            writer.WriteByte(0);  // compression method
            writer.WriteByte(0);  // filter method
            writer.WriteByte(0);  // interlace method
        });

        // The raw scanlines are built before the chunk writer captures anything, because the
        // source buffer is a span and cannot be captured by a lambda.
        var compressedData = DeflateCompress(BuildRawImageData(rgba, width, height));
        WriteChunk(output, "IDAT"u8, writer => writer.Write(compressedData));

        WriteChunk(output, "IEND"u8, _ => { });

        return output.ToArray();
    }

    private static byte[] BuildRawImageData(ReadOnlySpan<byte> rgba, int width, int height)
    {
        // Each row: 1 filter byte (0 = None) + Width * 4 RGBA bytes
        var rowSize = 1 + (width * BytesPerPixel);
        var data = new byte[rowSize * height];

        for (var y = 0; y < height; y++)
        {
            var rowOffset = y * rowSize;
            data[rowOffset] = 0; // filter: None

            rgba.Slice(y * width * BytesPerPixel, width * BytesPerPixel)
                .CopyTo(data.AsSpan(rowOffset + 1));
        }

        return data;
    }

    private static byte[] DeflateCompress(byte[] data)
    {
        using var output = new MemoryStream();

        // zlib header (RFC 1950): CMF=0x78 (deflate, window=32768), FLG=0x01
        output.WriteByte(0x78);
        output.WriteByte(0x01);

        using (var deflate = new DeflateStream(output, CompressionLevel.Fastest, leaveOpen: true))
        {
            deflate.Write(data, 0, data.Length);
        }

        // Adler-32 checksum (required by zlib)
        var adler = ComputeAdler32(data);
        output.WriteByte((byte)(adler >> 24));
        output.WriteByte((byte)(adler >> 16));
        output.WriteByte((byte)(adler >> 8));
        output.WriteByte((byte)adler);

        return output.ToArray();
    }

    private static uint ComputeAdler32(byte[] data)
    {
        uint a = 1, b = 0;
        const uint mod = 65521;
        foreach (var value in data)
        {
            a = (a + value) % mod;
            b = (b + a) % mod;
        }

        return (b << 16) | a;
    }

    private delegate void ChunkWriter(MemoryStream stream);

    private static void WriteChunk(MemoryStream output, ReadOnlySpan<byte> chunkType, ChunkWriter writeData)
    {
        using var dataStream = new MemoryStream();
        writeData(dataStream);
        var data = dataStream.ToArray();

        // Length (4 bytes, big-endian)
        WriteBigEndianInt32(output, data.Length);

        // Type (4 bytes)
        output.Write(chunkType);

        // Data
        if (data.Length > 0)
        {
            output.Write(data, 0, data.Length);
        }

        // CRC32 over type + data
        WriteBigEndianUInt32(output, ComputeCrc32(chunkType, data));
    }

    private static void WriteBigEndianInt32(MemoryStream stream, int value)
    {
        stream.WriteByte((byte)(value >> 24));
        stream.WriteByte((byte)(value >> 16));
        stream.WriteByte((byte)(value >> 8));
        stream.WriteByte((byte)value);
    }

    private static void WriteBigEndianUInt32(MemoryStream stream, uint value)
    {
        stream.WriteByte((byte)(value >> 24));
        stream.WriteByte((byte)(value >> 16));
        stream.WriteByte((byte)(value >> 8));
        stream.WriteByte((byte)value);
    }

    private static readonly uint[] Crc32Table = GenerateCrc32Table();

    private static uint[] GenerateCrc32Table()
    {
        var table = new uint[256];
        for (uint i = 0; i < 256; i++)
        {
            var crc = i;
            for (var j = 0; j < 8; j++)
            {
                crc = (crc & 1) != 0 ? 0xEDB88320 ^ (crc >> 1) : crc >> 1;
            }

            table[i] = crc;
        }

        return table;
    }

    private static uint ComputeCrc32(ReadOnlySpan<byte> type, byte[] data)
    {
        var crc = 0xFFFFFFFF;
        foreach (var value in type)
        {
            crc = Crc32Table[(crc ^ value) & 0xFF] ^ (crc >> 8);
        }

        foreach (var value in data)
        {
            crc = Crc32Table[(crc ^ value) & 0xFF] ^ (crc >> 8);
        }

        return crc ^ 0xFFFFFFFF;
    }
}
