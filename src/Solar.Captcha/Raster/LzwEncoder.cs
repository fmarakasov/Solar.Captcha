using System;
using System.Collections.Generic;
using System.IO;

namespace Solar.Captcha.Raster;

/// <summary>
/// Compresses palette indices with the LZW variant GIF uses: variable code width, LSB-first bit
/// packing, and a Clear code emitted at the start and again whenever the dictionary fills.
/// </summary>
/// <remarks>
/// <para>
/// The dictionary maps a (prefix code, next index) pair to a code, growing from
/// <c>Clear + 2</c> codes. The code width grows in step with the decoder's, which adds its first
/// dictionary entry on the <em>second</em> code it reads rather than the first — so this encoder
/// grows the width one step later than a naive entry count would suggest. Growing too early
/// produces bytes that look plausible but decode to garbage.
/// </para>
/// <para>
/// Output is written as GIF data sub-blocks: a length byte, up to 255 bytes of payload, repeating,
/// terminated by a zero-length block.
/// </para>
/// </remarks>
internal static class LzwEncoder
{
    /// <summary>Codes are 12 bits wide at most; the dictionary holds 4096 entries.</summary>
    private const int MaximumCodes = 4096;

    private const int MaximumCodeWidth = 12;

    /// <summary>The largest payload a GIF data sub-block can carry.</summary>
    private const int MaximumSubBlockLength = 255;

    /// <summary>
    /// Compresses indices and writes them as GIF data sub-blocks.
    /// </summary>
    /// <param name="output">The destination stream.</param>
    /// <param name="indices">The palette indices to compress, one byte per pixel.</param>
    /// <param name="minimumCodeSize">
    /// The LZW minimum code size, matching the palette: at least 2 and at most 8.
    /// </param>
    /// <exception cref="ArgumentNullException">When <paramref name="output"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// When <paramref name="minimumCodeSize"/> is outside 2..8.
    /// </exception>
    public static void Encode(Stream output, ReadOnlySpan<byte> indices, int minimumCodeSize)
    {
        ArgumentNullException.ThrowIfNull(output);
        ArgumentOutOfRangeException.ThrowIfLessThan(minimumCodeSize, 2);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(minimumCodeSize, 8);

        var blocks = new SubBlockWriter(output);

        if (indices.IsEmpty)
        {
            blocks.Flush();
            return;
        }

        var clearCode = 1 << minimumCodeSize;
        var endCode = clearCode + 1;

        var codeWidth = minimumCodeSize + 1;
        var nextCode = endCode + 1;

        var dictionary = new Dictionary<int, int>();
        var bitBuffer = 0;
        var bitCount = 0;

        void WriteCode(int code)
        {
            bitBuffer |= code << bitCount;
            bitCount += codeWidth;

            while (bitCount >= 8)
            {
                blocks.Add((byte)(bitBuffer & 0xFF));
                bitBuffer >>= 8;
                bitCount -= 8;
            }
        }

        WriteCode(clearCode);

        var prefix = (int)indices[0];

        for (var i = 1; i < indices.Length; i++)
        {
            var next = indices[i];
            var key = (prefix << 8) | next;

            if (dictionary.TryGetValue(key, out var existing))
            {
                prefix = existing;
                continue;
            }

            WriteCode(prefix);

            if (nextCode < MaximumCodes)
            {
                dictionary[key] = nextCode;
                nextCode++;

                if (nextCode > (1 << codeWidth) && codeWidth < MaximumCodeWidth)
                {
                    codeWidth++;
                }
            }
            else
            {
                WriteCode(clearCode);
                dictionary.Clear();
                codeWidth = minimumCodeSize + 1;
                nextCode = endCode + 1;
            }

            prefix = next;
        }

        WriteCode(prefix);
        WriteCode(endCode);

        if (bitCount > 0)
        {
            blocks.Add((byte)(bitBuffer & 0xFF));
        }

        blocks.Flush();
    }

    /// <summary>
    /// Buffers compressed bytes and emits them as GIF data sub-blocks.
    /// </summary>
    private sealed class SubBlockWriter(Stream output)
    {
        private readonly byte[] _buffer = new byte[MaximumSubBlockLength];
        private int _length;

        public void Add(byte value)
        {
            _buffer[_length++] = value;

            if (_length == MaximumSubBlockLength)
            {
                FlushBlock();
            }
        }

        public void Flush()
        {
            FlushBlock();

            // A zero-length sub-block ends the data stream.
            output.WriteByte(0);
        }

        private void FlushBlock()
        {
            if (_length == 0)
            {
                return;
            }

            output.WriteByte((byte)_length);
            output.Write(_buffer, 0, _length);
            _length = 0;
        }
    }
}
