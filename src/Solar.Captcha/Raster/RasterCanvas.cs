using System;

namespace Solar.Captcha.Raster;

/// <summary>
/// An in-memory RGBA drawing surface with the 2D primitives the captcha renderers need.
/// </summary>
/// <remarks>
/// <para>
/// The buffer is row-major: four bytes (R, G, B, A) per pixel, left to right, top to bottom.
/// A pixel is addressed by its whole-number coordinate, so pixel <c>(x, y)</c> has its centre at
/// <c>(x, y)</c> — drawing at a fractional coordinate simply rounds at stamp time.
/// </para>
/// <para>
/// Drawing is not anti-aliased (ADR-0010): a pixel is either covered or not. Shapes are rasterized
/// from their outlines, and strokes are drawn by stamping a disc along the path, which yields round
/// caps and round joins without cap or join options.
/// </para>
/// <para>
/// The canvas itself performs no encoding. Convert it with <see cref="PngEncoder"/> or feed it to
/// the streaming GIF encoder, which lets each caller pick a format without the canvas knowing one.
/// </para>
/// </remarks>
public sealed class RasterCanvas
{
    private const int BytesPerPixel = 4;

    private readonly byte[] _pixels;

    /// <summary>
    /// Creates a canvas filled with transparent black.
    /// </summary>
    /// <param name="width">The width in pixels.</param>
    /// <param name="height">The height in pixels.</param>
    /// <exception cref="ArgumentOutOfRangeException">When either dimension is not positive.</exception>
    public RasterCanvas(int width, int height)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);

        Width = width;
        Height = height;
        _pixels = new byte[width * height * BytesPerPixel];
    }

    /// <summary>Gets the width in pixels.</summary>
    public int Width { get; }

    /// <summary>Gets the height in pixels.</summary>
    public int Height { get; }

    /// <summary>Gets the raw RGBA buffer, for the encoders.</summary>
    internal ReadOnlySpan<byte> Pixels => _pixels;

    /// <summary>
    /// Fills the whole canvas with a colour, replacing whatever was there.
    /// </summary>
    /// <param name="color">The fill colour. Its alpha is written through unchanged.</param>
    public void Clear(RasterColor color)
    {
        for (var i = 0; i < _pixels.Length; i += BytesPerPixel)
        {
            _pixels[i] = color.R;
            _pixels[i + 1] = color.G;
            _pixels[i + 2] = color.B;
            _pixels[i + 3] = color.A;
        }
    }

    /// <summary>
    /// Creates an independent copy of this canvas.
    /// </summary>
    /// <returns>A new canvas with the same dimensions and the same pixels.</returns>
    /// <remarks>
    /// Used by frame-based renderers to keep a static layer and reuse it for every frame, rather
    /// than redrawing the layer each time.
    /// </remarks>
    public RasterCanvas Clone()
    {
        var clone = new RasterCanvas(Width, Height);
        _pixels.CopyTo(clone._pixels, 0);
        return clone;
    }

    /// <summary>
    /// Reads one pixel.
    /// </summary>
    /// <param name="x">The horizontal coordinate.</param>
    /// <param name="y">The vertical coordinate.</param>
    /// <returns>The pixel colour, or <see cref="RasterColor.Transparent"/> outside the canvas.</returns>
    public RasterColor GetPixel(int x, int y)
    {
        if (!Contains(x, y))
        {
            return RasterColor.Transparent;
        }

        var offset = OffsetOf(x, y);
        return new RasterColor(_pixels[offset], _pixels[offset + 1], _pixels[offset + 2], _pixels[offset + 3]);
    }

    /// <summary>
    /// Writes one pixel, compositing semi-transparent colours over what is already there.
    /// </summary>
    /// <param name="x">The horizontal coordinate.</param>
    /// <param name="y">The vertical coordinate.</param>
    /// <param name="color">The colour to write.</param>
    /// <remarks>
    /// Compositing flattens the result to opaque, because the canvas has no separate transparency
    /// channel to accumulate into. Coordinates outside the canvas are ignored.
    /// </remarks>
    public void SetPixel(int x, int y, RasterColor color)
    {
        if (!Contains(x, y))
        {
            return;
        }

        var offset = OffsetOf(x, y);

        if (color.A == 255)
        {
            _pixels[offset] = color.R;
            _pixels[offset + 1] = color.G;
            _pixels[offset + 2] = color.B;
            _pixels[offset + 3] = 255;
            return;
        }

        if (color.A == 0)
        {
            return;
        }

        var sourceAlpha = color.A / 255f;
        var destinationAlpha = 1f - sourceAlpha;

        _pixels[offset] = Blend(_pixels[offset], color.R, sourceAlpha, destinationAlpha);
        _pixels[offset + 1] = Blend(_pixels[offset + 1], color.G, sourceAlpha, destinationAlpha);
        _pixels[offset + 2] = Blend(_pixels[offset + 2], color.B, sourceAlpha, destinationAlpha);
        _pixels[offset + 3] = 255;
    }

    /// <summary>
    /// Fills the canvas with a linear gradient.
    /// </summary>
    /// <param name="brush">The gradient to paint.</param>
    /// <remarks>
    /// Each pixel is projected onto the gradient axis, and the resulting fraction selects the
    /// colour. Pixels outside the two stops are folded according to the brush's repetition mode.
    /// </remarks>
    /// <exception cref="ArgumentNullException">When <paramref name="brush"/> is <see langword="null"/>.</exception>
    public void FillLinearGradient(LinearGradientBrush brush)
    {
        ArgumentNullException.ThrowIfNull(brush);

        var axisX = brush.End.X - brush.Start.X;
        var axisY = brush.End.Y - brush.Start.Y;
        var axisLengthSquared = (axisX * axisX) + (axisY * axisY);

        for (var y = 0; y < Height; y++)
        {
            var relativeY = y - brush.Start.Y;

            for (var x = 0; x < Width; x++)
            {
                var relativeX = x - brush.Start.X;
                var t = ((relativeX * axisX) + (relativeY * axisY)) / axisLengthSquared;

                SetPixel(x, y, brush.ColorAt(t));
            }
        }
    }

    /// <summary>
    /// Fills a circle.
    /// </summary>
    /// <param name="center">The centre point.</param>
    /// <param name="radius">The radius in pixels. Must be greater than zero.</param>
    /// <param name="color">The fill colour.</param>
    /// <exception cref="ArgumentOutOfRangeException">When <paramref name="radius"/> is not positive.</exception>
    public void FillCircle(PointF center, float radius, RasterColor color)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(radius);

        var radiusSquared = radius * radius;
        var minX = (int)MathF.Floor(center.X - radius);
        var maxX = (int)MathF.Ceiling(center.X + radius);
        var minY = (int)MathF.Floor(center.Y - radius);
        var maxY = (int)MathF.Ceiling(center.Y + radius);

        for (var y = minY; y <= maxY; y++)
        {
            var dy = y - center.Y;

            for (var x = minX; x <= maxX; x++)
            {
                var dx = x - center.X;
                if ((dx * dx) + (dy * dy) <= radiusSquared)
                {
                    SetPixel(x, y, color);
                }
            }
        }
    }

    /// <summary>
    /// Draws a straight line with the given pen.
    /// </summary>
    /// <param name="from">The start point.</param>
    /// <param name="to">The end point.</param>
    /// <param name="pen">The stroke colour and width.</param>
    /// <remarks>
    /// The line is drawn by stamping a disc of the pen's radius along the path at sub-radius
    /// intervals, so consecutive stamps overlap and the stroke is continuous on diagonals.
    /// </remarks>
    /// <exception cref="ArgumentNullException">When <paramref name="pen"/> is <see langword="null"/>.</exception>
    public void DrawLine(PointF from, PointF to, RasterPen pen)
    {
        ArgumentNullException.ThrowIfNull(pen);

        var radius = pen.Thickness / 2f;
        var deltaX = to.X - from.X;
        var deltaY = to.Y - from.Y;
        var length = MathF.Sqrt((deltaX * deltaX) + (deltaY * deltaY));

        if (length <= 0f)
        {
            FillCircle(from, radius, pen.Color);
            return;
        }

        var spacing = MathF.Max(0.5f, radius);
        var steps = (int)MathF.Ceiling(length / spacing);

        for (var i = 0; i <= steps; i++)
        {
            var t = (float)i / steps;
            FillCircle(new PointF(from.X + (deltaX * t), from.Y + (deltaY * t)), radius, pen.Color);
        }
    }

    /// <summary>
    /// Strokes a closed polygon: every edge, plus the edge that closes the last point back to the first.
    /// </summary>
    /// <param name="points">The polygon vertices, in order.</param>
    /// <param name="pen">The stroke colour and width.</param>
    /// <remarks>
    /// Fewer than two points draw nothing; two points draw a single line out and back.
    /// </remarks>
    /// <exception cref="ArgumentNullException">When <paramref name="pen"/> is <see langword="null"/>.</exception>
    public void StrokePolygon(ReadOnlySpan<PointF> points, RasterPen pen)
    {
        ArgumentNullException.ThrowIfNull(pen);

        if (points.Length < 2)
        {
            return;
        }

        for (var i = 0; i < points.Length; i++)
        {
            DrawLine(points[i], points[(i + 1) % points.Length], pen);
        }
    }

    /// <summary>
    /// Draws text on its baseline, advancing left to right.
    /// </summary>
    /// <param name="text">The text to draw.</param>
    /// <param name="font">The font to draw with.</param>
    /// <param name="pixelSize">The em size in pixels.</param>
    /// <param name="color">The text colour.</param>
    /// <param name="x">The pen's horizontal position, i.e. where the run starts.</param>
    /// <param name="baselineY">The pen's vertical position: the row the baseline sits on.</param>
    /// <remarks>
    /// Characters the font cannot map are skipped and contribute no advance, so a partially
    /// supported string draws what it can rather than failing.
    /// </remarks>
    /// <exception cref="ArgumentNullException">
    /// When <paramref name="font"/> is <see langword="null"/> or <paramref name="text"/> is <see langword="null"/>.
    /// </exception>
    public void DrawText(string text, RasterFont font, float pixelSize, RasterColor color, float x, float baselineY)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(font);

        var penX = x;

        foreach (var character in text)
        {
            if (!font.TryGetPlacement(character, pixelSize, out var placement))
            {
                penX += font.GetAdvance(character, pixelSize);
                continue;
            }

            BlitMask(
                placement.Coverage,
                placement.Width,
                placement.Height,
                (int)MathF.Round(penX) + placement.OffsetX,
                (int)MathF.Round(baselineY) + placement.OffsetY,
                color);

            penX += placement.Advance;
        }
    }

    /// <summary>
    /// Composites a binary coverage mask onto the canvas.
    /// </summary>
    /// <param name="coverage">The mask, row-major, <paramref name="width"/> × <paramref name="height"/> bytes.</param>
    /// <param name="width">The mask width in pixels.</param>
    /// <param name="height">The mask height in pixels.</param>
    /// <param name="destinationX">The canvas column the mask's left edge maps to.</param>
    /// <param name="destinationY">The canvas row the mask's top edge maps to.</param>
    /// <param name="color">The colour to write where the mask is set.</param>
    /// <remarks>
    /// Any non-zero coverage value writes the colour, so the mask may be binary (as the rasterizer
    /// produces) or a future coverage map without changing this method.
    /// </remarks>
    public void BlitMask(byte[] coverage, int width, int height, int destinationX, int destinationY, RasterColor color)
    {
        ArgumentNullException.ThrowIfNull(coverage);
        ArgumentOutOfRangeException.ThrowIfNegative(width);
        ArgumentOutOfRangeException.ThrowIfNegative(height);

        for (var y = 0; y < height; y++)
        {
            var maskOffset = y * width;

            for (var x = 0; x < width; x++)
            {
                if (coverage[maskOffset + x] != 0)
                {
                    SetPixel(destinationX + x, destinationY + y, color);
                }
            }
        }
    }

    private bool Contains(int x, int y) => x >= 0 && x < Width && y >= 0 && y < Height;

    private int OffsetOf(int x, int y) => ((y * Width) + x) * BytesPerPixel;

    private static byte Blend(byte destination, byte source, float sourceAlpha, float destinationAlpha) =>
        (byte)((destination * destinationAlpha) + (source * sourceAlpha));
}
