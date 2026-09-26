using System;
using System.Collections.Generic;
using System.IO;

namespace Solar.Captcha.Tests.TestSupport;

/// <summary>
/// One decoded GIF frame.
/// </summary>
internal sealed class GifFrame
{
    public int Left { get; init; }

    public int Top { get; init; }

    public int Width { get; init; }

    public int Height { get; init; }

    public int DelayCentiseconds { get; init; }

    public int DisposalMethod { get; init; }

    /// <summary>Gets the palette indices, one per pixel, row-major.</summary>
    public required byte[] Indices { get; init; }

    /// <summary>Gets the index at a position within the frame.</summary>
    public byte this[int x, int y] => Indices[(y * Width) + x];
}

/// <summary>
/// A minimal GIF89a reader used only by the tests.
/// </summary>
/// <remarks>
/// The library encodes GIF by hand, and an encoder that emits plausible-looking bytes that no
/// decoder can read is a real failure mode. This reader is the independent check: it parses the
/// container and inflates the LZW stream the way a browser would, so a round-trip proves the output
/// is genuinely readable rather than merely well-shaped. It lives in the test project and is
/// deliberately not part of the shipped library.
/// </remarks>
internal sealed class GifImage
{
    private const byte ExtensionIntroducer = 0x21;
    private const byte GraphicControlLabel = 0xF9;
    private const byte ApplicationLabel = 0xFF;
    private const byte ImageSeparator = 0x2C;
    private const byte Trailer = 0x3B;

    public int Width { get; init; }

    public int Height { get; init; }

    /// <summary>Gets the global colour table as packed RGB triplets.</summary>
    public required byte[] GlobalColorTable { get; init; }

    /// <summary>Gets the loop count from the Netscape extension, or -1 when absent.</summary>
    public int RepeatCount { get; init; }

    public required List<GifFrame> Frames { get; init; }

    /// <summary>Gets the RGB colour of a palette index.</summary>
    public (byte R, byte G, byte B) ColorAt(byte index) => (
        GlobalColorTable[index * 3],
        GlobalColorTable[(index * 3) + 1],
        GlobalColorTable[(index * 3) + 2]);

    /// <summary>
    /// Parses a GIF stream.
    /// </summary>
    /// <param name="data">The encoded bytes.</param>
    /// <returns>The decoded image.</returns>
    /// <exception cref="InvalidDataException">When the stream is not a readable GIF.</exception>
    public static GifImage Parse(byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);

        var reader = new ByteReader(data);

        var signature = reader.ReadAscii(6);
        if (signature is not ("GIF87a" or "GIF89a"))
        {
            throw new InvalidDataException($"Not a GIF: signature was '{signature}'.");
        }

        var width = reader.ReadUInt16LittleEndian();
        var height = reader.ReadUInt16LittleEndian();
        var packed = reader.ReadByte();
        reader.ReadByte(); // background colour index
        reader.ReadByte(); // pixel aspect ratio

        var colorTable = Array.Empty<byte>();
        if ((packed & 0x80) != 0)
        {
            var tableSize = 2 << (packed & 0x07);
            colorTable = reader.ReadBytes(tableSize * 3);
        }

        var frames = new List<GifFrame>();
        var repeatCount = -1;

        var pendingDelay = 0;
        var pendingDisposal = 0;

        while (true)
        {
            var block = reader.ReadByte();

            if (block == Trailer)
            {
                break;
            }

            if (block == ExtensionIntroducer)
            {
                var label = reader.ReadByte();

                if (label == GraphicControlLabel)
                {
                    var size = reader.ReadByte();
                    if (size != 4)
                    {
                        throw new InvalidDataException($"Graphic control block size was {size}, expected 4.");
                    }

                    var controlPacked = reader.ReadByte();
                    pendingDisposal = (controlPacked >> 2) & 0x07;
                    pendingDelay = reader.ReadUInt16LittleEndian();
                    reader.ReadByte();   // transparent colour index
                    reader.ReadByte();   // block terminator
                }
                else
                {
                    if (label == ApplicationLabel)
                    {
                        var declared = reader.ReadByte();
                        var identifier = reader.ReadAscii(declared);
                        var subBlocks = reader.ReadSubBlocks();
                        if (identifier == "NETSCAPE2.0" && subBlocks.Length >= 3 && subBlocks[0] == 1)
                        {
                            repeatCount = subBlocks[1] | (subBlocks[2] << 8);
                        }

                        continue;
                    }

                    reader.SkipSubBlocks();
                }

                continue;
            }

            if (block != ImageSeparator)
            {
                throw new InvalidDataException($"Unexpected block 0x{block:X2}.");
            }

            var left = reader.ReadUInt16LittleEndian();
            var top = reader.ReadUInt16LittleEndian();
            var frameWidth = reader.ReadUInt16LittleEndian();
            var frameHeight = reader.ReadUInt16LittleEndian();
            var imagePacked = reader.ReadByte();

            if ((imagePacked & 0x80) != 0)
            {
                var localTableSize = 2 << (imagePacked & 0x07);
                reader.ReadBytes(localTableSize * 3);
            }

            var minimumCodeSize = reader.ReadByte();
            var compressed = reader.ReadSubBlocks();

            frames.Add(new GifFrame
            {
                Left = left,
                Top = top,
                Width = frameWidth,
                Height = frameHeight,
                DelayCentiseconds = pendingDelay,
                DisposalMethod = pendingDisposal,
                Indices = LzwDecoder.Decode(compressed, minimumCodeSize, frameWidth * frameHeight),
            });
        }

        return new GifImage
        {
            Width = width,
            Height = height,
            GlobalColorTable = colorTable,
            RepeatCount = repeatCount,
            Frames = frames,
        };
    }

    private sealed class ByteReader(byte[] data)
    {
        private int _position;

        public byte ReadByte()
        {
            if (_position >= data.Length)
            {
                throw new InvalidDataException("Unexpected end of GIF data.");
            }

            return data[_position++];
        }

        public byte[] ReadBytes(int count)
        {
            if (count < 0 || _position + count > data.Length)
            {
                throw new InvalidDataException($"Cannot read {count} bytes at offset {_position}.");
            }

            var result = data.AsSpan(_position, count).ToArray();
            _position += count;
            return result;
        }

        public int ReadUInt16LittleEndian()
        {
            var low = ReadByte();
            var high = ReadByte();
            return low | (high << 8);
        }

        public string ReadAscii(int count) => System.Text.Encoding.ASCII.GetString(ReadBytes(count));

        /// <summary>Reads a chain of length-prefixed sub-blocks and concatenates their payloads.</summary>
        public byte[] ReadSubBlocks()
        {
            using var buffer = new MemoryStream();
            while (true)
            {
                var length = ReadByte();
                if (length == 0)
                {
                    break;
                }

                buffer.Write(ReadBytes(length));
            }

            return buffer.ToArray();
        }

        public void SkipSubBlocks() => _ = ReadSubBlocks();
    }
}
