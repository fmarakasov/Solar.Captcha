using System.Collections.Generic;
using System.IO;

namespace Solar.Captcha.Tests.TestSupport;

/// <summary>
/// Unwraps GIF data sub-blocks.
/// </summary>
/// <remarks>
/// A GIF image block stores its LZW payload as a chain of length-prefixed sub-blocks. The encoder
/// writes that framing, and the decoder needs the payload without it, so tests that exercise the
/// encoder and decoder directly have to bridge the two.
/// </remarks>
internal static class GifSubBlocks
{
    /// <summary>
    /// Concatenates the payloads of a GIF sub-block chain.
    /// </summary>
    /// <param name="framed">The framed bytes: length byte, payload, repeating, then a zero byte.</param>
    /// <returns>The concatenated payload.</returns>
    /// <exception cref="InvalidDataException">When the chain is truncated.</exception>
    public static byte[] Unwrap(byte[] framed)
    {
        var payload = new List<byte>();
        var position = 0;

        while (true)
        {
            if (position >= framed.Length)
            {
                throw new InvalidDataException("The sub-block chain ended without a terminator.");
            }

            var length = framed[position++];
            if (length == 0)
            {
                break;
            }

            if (position + length > framed.Length)
            {
                throw new InvalidDataException("A sub-block claimed more bytes than remain.");
            }

            for (var i = 0; i < length; i++)
            {
                payload.Add(framed[position + i]);
            }

            position += length;
        }

        return [.. payload];
    }
}
