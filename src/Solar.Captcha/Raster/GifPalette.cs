using System;
using System.Collections.Generic;

namespace Solar.Captcha.Raster;

/// <summary>
/// The colour table of a GIF animation: up to 256 RGB entries, with nearest-colour lookup for
/// pixels that are not exactly in the table.
/// </summary>
/// <remarks>
/// The table is built once, from the first frame plus any seed colours the caller wants guaranteed
/// to survive. GIF indexes pixels into a fixed table, so a table built from a gradient cannot hold
/// every distinct colour; when the distinct colour count exceeds the table size the entries are
/// sampled uniformly from the colour-sorted set, which keeps the gradient's ramp even. Seed colours
/// are placed first and are never sampled away, so a stroke colour cannot be lost to the gradient.
/// </remarks>
internal sealed class GifPalette
{
    /// <summary>The largest colour table a GIF can carry.</summary>
    public const int MaximumColors = 256;

    private readonly byte[] _table;
    private readonly int[] _entries;

    private GifPalette(byte[] table, int[] entries, int size)
    {
        _table = table;
        _entries = entries;
        Size = size;
        MinimumCodeSize = CalculateMinimumCodeSize(size);
    }

    /// <summary>Gets the table size in entries: a power of two between 2 and 256.</summary>
    public int Size { get; }

    /// <summary>Gets the LZW minimum code size that matches <see cref="Size"/>.</summary>
    public int MinimumCodeSize { get; }

    /// <summary>Gets the packed RGB triplets, three bytes per entry, <see cref="Size"/> entries.</summary>
    public ReadOnlySpan<byte> Table => _table;

    /// <summary>
    /// Builds a palette from a frame plus optional seed colours.
    /// </summary>
    /// <param name="seedColors">
    /// Colours that must be present in the table, in preference to sampled gradient colours. May be
    /// <see langword="null"/>.
    /// </param>
    /// <param name="rgba">The first frame's pixels, four bytes per pixel.</param>
    /// <param name="pixelCount">The number of pixels in <paramref name="rgba"/>.</param>
    /// <returns>The palette.</returns>
    public static GifPalette Build(IReadOnlyCollection<RasterColor>? seedColors, ReadOnlySpan<byte> rgba, int pixelCount)
    {
        var ordered = new List<int>();

        if (seedColors is not null)
        {
            foreach (var color in seedColors)
            {
                var packed = Pack(color.R, color.G, color.B);
                if (!ordered.Contains(packed))
                {
                    ordered.Add(packed);
                }
            }
        }

        var seedCount = ordered.Count;

        var seen = new HashSet<int>(ordered);
        for (var i = 0; i < pixelCount; i++)
        {
            var offset = i * 4;
            var packed = Pack(rgba[offset], rgba[offset + 1], rgba[offset + 2]);
            if (seen.Add(packed))
            {
                ordered.Add(packed);
            }
        }

        var colors = SelectEntries(ordered, seedCount);

        var table = new byte[colors.Length * 3];
        for (var i = 0; i < colors.Length; i++)
        {
            table[(i * 3) + 0] = (byte)(colors[i] >> 16);
            table[(i * 3) + 1] = (byte)(colors[i] >> 8);
            table[(i * 3) + 2] = (byte)colors[i];
        }

        return new GifPalette(table, colors, colors.Length);
    }

    /// <summary>
    /// Finds the index of the table entry closest to a packed RGB colour.
    /// </summary>
    /// <param name="packedRgb">The colour, packed as <c>0xRRGGBB</c>.</param>
    /// <returns>The index of the nearest table entry, by squared distance in RGB space.</returns>
    public byte Nearest(int packedRgb)
    {
        var red = (packedRgb >> 16) & 0xFF;
        var green = (packedRgb >> 8) & 0xFF;
        var blue = packedRgb & 0xFF;

        var bestIndex = 0;
        var bestDistance = int.MaxValue;

        for (var i = 0; i < _entries.Length; i++)
        {
            var candidate = _entries[i];
            var deltaRed = ((candidate >> 16) & 0xFF) - red;
            var deltaGreen = ((candidate >> 8) & 0xFF) - green;
            var deltaBlue = (candidate & 0xFF) - blue;
            var distance = (deltaRed * deltaRed) + (deltaGreen * deltaGreen) + (deltaBlue * deltaBlue);

            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestIndex = i;

                if (distance == 0)
                {
                    break;
                }
            }
        }

        return (byte)bestIndex;
    }

    private static int[] SelectEntries(List<int> ordered, int seedCount)
    {
        if (ordered.Count <= MaximumColors)
        {
            // Pad to a power of two so the table size field and the LZW code size agree.
            var padded = new int[PowerOfTwoAtLeast(ordered.Count)];
            ordered.CopyTo(padded);
            return padded;
        }

        // Keep every seed colour, then sample the remaining (colour-sorted) entries uniformly,
        // so the gradient keeps an even ramp rather than losing a contiguous band.
        var result = new int[MaximumColors];
        var seeds = Math.Min(seedCount, MaximumColors);
        for (var i = 0; i < seeds; i++)
        {
            result[i] = ordered[i];
        }

        var remaining = MaximumColors - seeds;
        if (remaining > 0)
        {
            var rest = ordered.GetRange(seeds, ordered.Count - seeds);
            rest.Sort();

            for (var i = 0; i < remaining; i++)
            {
                var index = (int)((long)i * (rest.Count - 1) / Math.Max(1, remaining - 1));
                result[seeds + i] = rest[index];
            }
        }

        return result;
    }

    private static int PowerOfTwoAtLeast(int value)
    {
        var size = 2;
        while (size < value)
        {
            size <<= 1;
        }

        return Math.Min(size, MaximumColors);
    }

    private static int CalculateMinimumCodeSize(int tableSize)
    {
        var bits = 1;
        while ((1 << bits) < tableSize)
        {
            bits++;
        }

        // The GIF spec requires a minimum code size of at least 2.
        return Math.Max(2, bits);
    }

    private static int Pack(byte r, byte g, byte b) => (r << 16) | (g << 8) | b;
}
