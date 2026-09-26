using System;

namespace Solar.Captcha.Raster;

/// <summary>
/// A location in the pixel grid, in whole pixels.
/// </summary>
public readonly struct Point : IEquatable<Point>
{
    /// <summary>Initializes a point.</summary>
    /// <param name="x">Horizontal coordinate.</param>
    /// <param name="y">Vertical coordinate.</param>
    public Point(int x, int y)
    {
        X = x;
        Y = y;
    }

    /// <summary>Gets the horizontal coordinate.</summary>
    public int X { get; }

    /// <summary>Gets the vertical coordinate.</summary>
    public int Y { get; }

    /// <inheritdoc />
    public bool Equals(Point other) => X == other.X && Y == other.Y;

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is Point other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(X, Y);

    /// <inheritdoc />
    public override string ToString() => $"({X}, {Y})";

    /// <summary>Compares two points for equality.</summary>
    public static bool operator ==(Point left, Point right) => left.Equals(right);

    /// <summary>Compares two points for inequality.</summary>
    public static bool operator !=(Point left, Point right) => !left.Equals(right);
}

/// <summary>
/// A location on the drawing surface with sub-pixel precision.
/// </summary>
public readonly struct PointF : IEquatable<PointF>
{
    /// <summary>Initializes a point.</summary>
    /// <param name="x">Horizontal coordinate.</param>
    /// <param name="y">Vertical coordinate.</param>
    public PointF(float x, float y)
    {
        X = x;
        Y = y;
    }

    /// <summary>Gets the horizontal coordinate.</summary>
    public float X { get; }

    /// <summary>Gets the vertical coordinate.</summary>
    public float Y { get; }

    /// <inheritdoc />
    public bool Equals(PointF other) => X.Equals(other.X) && Y.Equals(other.Y);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is PointF other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(X, Y);

    /// <inheritdoc />
    public override string ToString() => $"({X}, {Y})";

    /// <summary>Compares two points for equality.</summary>
    public static bool operator ==(PointF left, PointF right) => left.Equals(right);

    /// <summary>Compares two points for inequality.</summary>
    public static bool operator !=(PointF left, PointF right) => !left.Equals(right);
}
