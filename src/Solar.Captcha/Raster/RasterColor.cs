using System;
using System.Globalization;

namespace Solar.Captcha.Raster;

/// <summary>
/// A 32-bit straight-alpha RGBA colour used by the raster drawing primitives.
/// </summary>
/// <remarks>
/// Replaces the imaging-library colour type so the library carries no third-party
/// dependency. The byte order matches the frame buffer layout (R, G, B, A).
/// </remarks>
public readonly struct RasterColor : IEquatable<RasterColor>
{
    /// <summary>Initializes a colour from its channel values.</summary>
    /// <param name="r">Red channel.</param>
    /// <param name="g">Green channel.</param>
    /// <param name="b">Blue channel.</param>
    /// <param name="a">Alpha channel. Defaults to fully opaque.</param>
    public RasterColor(byte r, byte g, byte b, byte a = 255)
    {
        R = r;
        G = g;
        B = b;
        A = a;
    }

    /// <summary>Gets the red channel.</summary>
    public byte R { get; }

    /// <summary>Gets the green channel.</summary>
    public byte G { get; }

    /// <summary>Gets the blue channel.</summary>
    public byte B { get; }

    /// <summary>Gets the alpha channel.</summary>
    public byte A { get; }

    /// <summary>Gets opaque white.</summary>
    public static RasterColor White => new(255, 255, 255);

    /// <summary>Gets opaque black.</summary>
    public static RasterColor Black => new(0, 0, 0);

    /// <summary>Gets a fully transparent colour.</summary>
    public static RasterColor Transparent => new(0, 0, 0, 0);

    /// <summary>
    /// Parses a hexadecimal colour string.
    /// </summary>
    /// <param name="hex">
    /// The colour to parse. A leading <c>#</c> is optional. Accepts 3 digits (<c>RGB</c>,
    /// each digit duplicated), 6 digits (<c>RRGGBB</c>), or 8 digits (<c>RRGGBBAA</c>).
    /// </param>
    /// <returns>The parsed colour.</returns>
    /// <exception cref="ArgumentException">When <paramref name="hex"/> is not a valid colour string.</exception>
    public static RasterColor ParseHex(string hex)
    {
        if (string.IsNullOrWhiteSpace(hex))
        {
            throw new ArgumentException("Colour string must not be empty.", nameof(hex));
        }

        var value = hex.Trim();
        if (value.StartsWith('#'))
        {
            value = value[1..];
        }

        if (value.Length is not (3 or 6 or 8))
        {
            throw new ArgumentException(
                $"Colour '{hex}' must have 3, 6 or 8 hexadecimal digits.",
                nameof(hex));
        }

        foreach (var c in value)
        {
            if (!Uri.IsHexDigit(c))
            {
                throw new ArgumentException(
                    $"Colour '{hex}' contains a non-hexadecimal character '{c}'.",
                    nameof(hex));
            }
        }

        if (value.Length == 3)
        {
            // #RGB => #RRGGBB, each digit duplicated rather than zero-extended.
            return new RasterColor(
                Duplicate(ParseByte(value, 0, 1)),
                Duplicate(ParseByte(value, 1, 1)),
                Duplicate(ParseByte(value, 2, 1)));
        }

        var red = ParseByte(value, 0, 2);
        var green = ParseByte(value, 2, 2);
        var blue = ParseByte(value, 4, 2);
        var alpha = value.Length == 8 ? ParseByte(value, 6, 2) : (byte)255;

        return new RasterColor(red, green, blue, alpha);
    }

    private static byte ParseByte(string value, int offset, int length) =>
        byte.Parse(value.AsSpan(offset, length), NumberStyles.HexNumber, CultureInfo.InvariantCulture);

    private static byte Duplicate(byte nibble) => (byte)((nibble << 4) | nibble);

    /// <inheritdoc />
    public bool Equals(RasterColor other) => R == other.R && G == other.G && B == other.B && A == other.A;

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is RasterColor other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(R, G, B, A);

    /// <summary>Gets the colour as <c>#RRGGBBAA</c>.</summary>
    public override string ToString() => $"#{R:X2}{G:X2}{B:X2}{A:X2}";

    /// <summary>Compares two colours for equality.</summary>
    public static bool operator ==(RasterColor left, RasterColor right) => left.Equals(right);

    /// <summary>Compares two colours for inequality.</summary>
    public static bool operator !=(RasterColor left, RasterColor right) => !left.Equals(right);
}
