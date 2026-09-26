using System;
using System.Collections.Generic;
using System.IO;

namespace Solar.Captcha.Raster;

/// <summary>
/// Writes an animated GIF89a stream frame by frame, so only the frame being written needs to be
/// in memory at any one time.
/// </summary>
/// <remarks>
/// <para>
/// The colour table is built from the first frame written (plus any seed colours), then reused by
/// every later frame; a pixel whose colour is absent is mapped to the nearest table entry. That
/// makes the table exact when the frames use few colours and a faithful ramp when a gradient pushes
/// past 256.
/// </para>
/// <para>
/// Frames are written whole, with the disposal method set to "do not dispose", a single global
/// colour table, no transparency, and no interlacing. Nothing is written until the first frame
/// arrives, because the table cannot be known before then.
/// </para>
/// </remarks>
public sealed class GifEncoder
{
    private const byte ExtensionIntroducer = 0x21;
    private const byte GraphicControlLabel = 0xF9;
    private const byte ApplicationLabel = 0xFF;
    private const byte ImageSeparator = 0x2C;
    private const byte Trailer = 0x3B;

    /// <summary>The disposal method meaning "leave the previous frame in place".</summary>
    private const byte DisposalDoNotDispose = 1;

    private readonly Stream _output;
    private readonly int _width;
    private readonly int _height;
    private readonly int _delayCentiseconds;
    private readonly int _repeatCount;
    private readonly IReadOnlyCollection<RasterColor>? _seedColors;
    private readonly Dictionary<int, byte> _indexCache = [];

    private GifPalette? _palette;
    private bool _ended;

    private GifEncoder(
        Stream output,
        int width,
        int height,
        int delayCentiseconds,
        int repeatCount,
        IReadOnlyCollection<RasterColor>? seedColors)
    {
        _output = output;
        _width = width;
        _height = height;
        _delayCentiseconds = delayCentiseconds;
        _repeatCount = repeatCount;
        _seedColors = seedColors;
    }

    /// <summary>
    /// Starts an animation on a stream. Nothing is written until the first frame is added, because
    /// the colour table is derived from that frame.
    /// </summary>
    /// <param name="output">The stream to write to. Not disposed by this type; the caller owns it.</param>
    /// <param name="width">The frame width in pixels, shared by every frame.</param>
    /// <param name="height">The frame height in pixels, shared by every frame.</param>
    /// <param name="delayCentiseconds">The per-frame delay, in hundredths of a second.</param>
    /// <param name="repeatCount">
    /// How many times the animation repeats; 0 means forever. Defaults to 0.
    /// </param>
    /// <param name="seedColors">
    /// Colours that must survive colour-table reduction, such as stroke colours a gradient might
    /// otherwise crowd out. May be <see langword="null"/>.
    /// </param>
    /// <returns>The encoder, ready for <see cref="WriteFrame"/>.</returns>
    /// <exception cref="ArgumentNullException">When <paramref name="output"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// When a dimension is not positive, or the delay or repeat count is negative.
    /// </exception>
    public static GifEncoder Begin(
        Stream output,
        int width,
        int height,
        int delayCentiseconds,
        int repeatCount = 0,
        IReadOnlyCollection<RasterColor>? seedColors = null)
    {
        ArgumentNullException.ThrowIfNull(output);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        ArgumentOutOfRangeException.ThrowIfNegative(delayCentiseconds);
        ArgumentOutOfRangeException.ThrowIfNegative(repeatCount);

        return new GifEncoder(output, width, height, delayCentiseconds, repeatCount, seedColors);
    }

    /// <summary>
    /// Writes one frame. The first frame also writes the header and the global colour table.
    /// </summary>
    /// <param name="canvas">The frame to write. Its dimensions must match the encoder's.</param>
    /// <exception cref="ArgumentNullException">When <paramref name="canvas"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">When the canvas dimensions differ from the encoder's.</exception>
    /// <exception cref="InvalidOperationException">When the animation has already been ended.</exception>
    public void WriteFrame(RasterCanvas canvas)
    {
        ArgumentNullException.ThrowIfNull(canvas);

        if (_ended)
        {
            throw new InvalidOperationException("The animation has already been ended; no more frames can be written.");
        }

        if (canvas.Width != _width || canvas.Height != _height)
        {
            throw new ArgumentException(
                $"The animation is {_width}x{_height}, but the frame is {canvas.Width}x{canvas.Height}.",
                nameof(canvas));
        }

        var pixelCount = _width * _height;

        if (_palette is null)
        {
            _palette = GifPalette.Build(_seedColors, canvas.Pixels, pixelCount);
            WriteHeader(_palette);
        }

        var indices = MapToIndices(canvas.Pixels, pixelCount, _palette);

        WriteGraphicControlExtension();
        WriteImageDescriptor();
        _output.WriteByte((byte)_palette.MinimumCodeSize);
        LzwEncoder.Encode(_output, indices, _palette.MinimumCodeSize);
    }

    /// <summary>
    /// Writes the trailer, completing the stream.
    /// </summary>
    /// <exception cref="InvalidOperationException">When no frame has been written.</exception>
    public void End()
    {
        if (_ended)
        {
            return;
        }

        if (_palette is null)
        {
            throw new InvalidOperationException(
                "An animation needs at least one frame; the colour table is derived from the first frame.");
        }

        _output.WriteByte(Trailer);
        _ended = true;
    }

    private byte[] MapToIndices(ReadOnlySpan<byte> rgba, int pixelCount, GifPalette palette)
    {
        var indices = new byte[pixelCount];

        for (var i = 0; i < pixelCount; i++)
        {
            var offset = i * 4;
            var packed = (rgba[offset] << 16) | (rgba[offset + 1] << 8) | rgba[offset + 2];

            if (!_indexCache.TryGetValue(packed, out var index))
            {
                index = palette.Nearest(packed);
                _indexCache[packed] = index;
            }

            indices[i] = index;
        }

        return indices;
    }

    private void WriteHeader(GifPalette palette)
    {
        // Header
        _output.Write("GIF89a"u8);

        // Logical Screen Descriptor
        WriteUInt16LittleEndian(_width);
        WriteUInt16LittleEndian(_height);

        var sizeField = CalculateSizeField(palette.Size);
        var colourResolution = palette.MinimumCodeSize - 1;

        // Global colour table present (0x80), colour resolution, unsorted, table size.
        _output.WriteByte((byte)(0x80 | (colourResolution << 4) | sizeField));
        _output.WriteByte(0); // background colour index
        _output.WriteByte(0); // pixel aspect ratio: none

        // Global Colour Table
        _output.Write(palette.Table);

        // Netscape application extension: loop forever, or the requested number of times.
        _output.WriteByte(ExtensionIntroducer);
        _output.WriteByte(ApplicationLabel);
        _output.WriteByte(11);
        _output.Write("NETSCAPE2.0"u8);
        _output.WriteByte(3);
        _output.WriteByte(1);
        WriteUInt16LittleEndian(_repeatCount);
        _output.WriteByte(0);
    }

    private void WriteGraphicControlExtension()
    {
        _output.WriteByte(ExtensionIntroducer);
        _output.WriteByte(GraphicControlLabel);
        _output.WriteByte(4);
        _output.WriteByte(DisposalDoNotDispose << 2); // no user input, no transparency
        WriteUInt16LittleEndian(_delayCentiseconds);
        _output.WriteByte(0); // transparent colour index, unused
        _output.WriteByte(0); // block terminator
    }

    private void WriteImageDescriptor()
    {
        _output.WriteByte(ImageSeparator);
        WriteUInt16LittleEndian(0);      // left
        WriteUInt16LittleEndian(0);      // top
        WriteUInt16LittleEndian(_width);
        WriteUInt16LittleEndian(_height);
        _output.WriteByte(0);            // no local colour table, not interlaced, not sorted
    }

    private void WriteUInt16LittleEndian(int value)
    {
        _output.WriteByte((byte)(value & 0xFF));
        _output.WriteByte((byte)((value >> 8) & 0xFF));
    }

    private static int CalculateSizeField(int tableSize)
    {
        var field = 0;
        while ((1 << (field + 1)) < tableSize)
        {
            field++;
        }

        return field;
    }
}
