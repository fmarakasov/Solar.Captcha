using System;
using System.Collections.Generic;
using System.IO;

namespace Solar.Captcha.Tests.TestSupport;

/// <summary>
/// Inflates the LZW stream a GIF image block carries.
/// </summary>
/// <remarks>
/// This is the decoder half of the round-trip check on the hand-written encoder. It follows the
/// GIF LZW rules exactly: LSB-first codes, the Clear code resetting the dictionary and the code
/// width, and the width growing when the dictionary reaches the next power of two — one step behind
/// the encoder, which is where a naive encoder goes wrong.
/// </remarks>
internal static class LzwDecoder
{
    /// <summary>
    /// Decodes a GIF LZW stream.
    /// </summary>
    /// <param name="compressed">The compressed bytes, already unwrapped from their sub-blocks.</param>
    /// <param name="minimumCodeSize">The LZW minimum code size from the image block.</param>
    /// <param name="expectedCount">The number of indices the frame should contain.</param>
    /// <returns>The decoded palette indices.</returns>
    /// <exception cref="InvalidDataException">When the stream is malformed.</exception>
    public static byte[] Decode(byte[] compressed, int minimumCodeSize, int expectedCount)
    {
        ArgumentNullException.ThrowIfNull(compressed);
        ArgumentOutOfRangeException.ThrowIfLessThan(minimumCodeSize, 2);

        var clearCode = 1 << minimumCodeSize;
        var endCode = clearCode + 1;

        var dictionary = new List<byte[]>(4096);
        ResetDictionary(dictionary, clearCode);

        var codeWidth = minimumCodeSize + 1;
        var output = new List<byte>(expectedCount);

        var bitBuffer = 0;
        var bitCount = 0;
        var position = 0;

        byte[]? previous = null;

        while (true)
        {
            while (bitCount < codeWidth)
            {
                if (position >= compressed.Length)
                {
                    throw new InvalidDataException("The LZW stream ended before an end code.");
                }

                bitBuffer |= compressed[position++] << bitCount;
                bitCount += 8;
            }

            var code = bitBuffer & ((1 << codeWidth) - 1);
            bitBuffer >>= codeWidth;
            bitCount -= codeWidth;

            if (code == clearCode)
            {
                dictionary.Clear();
                ResetDictionary(dictionary, clearCode);
                codeWidth = minimumCodeSize + 1;
                previous = null;
                continue;
            }

            if (code == endCode)
            {
                break;
            }

            byte[] entry;

            if (code < dictionary.Count)
            {
                entry = dictionary[code];
            }
            else if (code == dictionary.Count && previous is not null)
            {
                // The KwKwK case: the encoder used a code it defined with this very output.
                entry = new byte[previous.Length + 1];
                Array.Copy(previous, entry, previous.Length);
                entry[^1] = previous[0];
            }
            else
            {
                throw new InvalidDataException($"Code {code} is not in the dictionary of {dictionary.Count} entries.");
            }

            output.AddRange(entry);

            if (previous is not null && dictionary.Count < 4096)
            {
                var added = new byte[previous.Length + 1];
                Array.Copy(previous, added, previous.Length);
                added[^1] = entry[0];
                dictionary.Add(added);

                if (dictionary.Count == (1 << codeWidth) && codeWidth < 12)
                {
                    codeWidth++;
                }
            }

            previous = entry;
        }

        if (output.Count != expectedCount)
        {
            throw new InvalidDataException($"Decoded {output.Count} indices, expected {expectedCount}.");
        }

        return [.. output];
    }

    private static void ResetDictionary(List<byte[]> dictionary, int clearCode)
    {
        for (var i = 0; i < clearCode; i++)
        {
            dictionary.Add([(byte)i]);
        }

        // Placeholders for the Clear and end codes, which occupy codes but never appear in output.
        dictionary.Add([]);
        dictionary.Add([]);
    }
}
