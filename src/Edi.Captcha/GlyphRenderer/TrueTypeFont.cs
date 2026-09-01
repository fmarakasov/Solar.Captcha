using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Solar.Captcha.GlyphRenderer;

/// <summary>
/// A minimal TrueType/OpenType font reader. It parses the tables required to
/// rasterize a glyph outline to a bitmap: <c>cmap</c> (character → glyph id),
/// <c>head</c>/<c>maxp</c> (global metrics), <c>hhea</c>/<c>hmtx</c> (horizontal
/// metrics), <c>loca</c>/<c>glyf</c> (glyph outlines), and <c>name</c> (font name).
///
/// It does not attempt to be a complete font engine: only the TrueType
/// <c>glyf</c> outlines (Simple and Composite) are supported.
/// </summary>
internal sealed class TrueTypeFont
{
    private readonly byte[] _data;
    private readonly IReadOnlyDictionary<string, TableRecord> _tables;

    private int _unitsPerEm;
    private short _ascender;
    private short _descender;
    private int _numGlyphs;
    private long _locaOffset;
    private bool _locaShortFormat;
    private long _glyfOffset;
    private long _hmtxOffset;
    private int _numberOfHMetrics;
    private long _cmapOffset;

    /// <summary>Gets the font family name as recorded in the <c>name</c> table.</summary>
    public string? FamilyName { get; private set; }

    private TrueTypeFont(byte[] data, IReadOnlyDictionary<string, TableRecord> tables)
    {
        _data = data;
        _tables = tables;
    }

    /// <summary>
    /// Loads and parses a TrueType/OpenType font from a file.
    /// </summary>
    /// <param name="fontPath">Path to the .ttf/.otf font file.</param>
    /// <returns>The parsed font.</returns>
    /// <exception cref="GlyphRendererException">When the file cannot be read or is not a supported font.</exception>
    public static TrueTypeFont Load(string fontPath)
    {
        byte[] data;
        try
        {
            data = File.ReadAllBytes(fontPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            throw new GlyphRendererException($"Unable to read font file '{fontPath}'.", ex);
        }

        return Parse(data, fontPath);
    }

    private static TrueTypeFont Parse(byte[] data, string fontPath)
    {
        if (data.Length < 12)
        {
            throw new GlyphRendererException($"Font file '{fontPath}' is too small to be a valid font.");
        }

        uint sfntVersion = BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(0, 4));
        bool isTrueType = sfntVersion == 0x00010000 || sfntVersion == 0x74727565; // 0x00010000 or 'true'
        bool isCff = sfntVersion == 0x4F54544F; // 'OTTO' (CFF outlines — unsupported)
        if (!isTrueType && !isCff)
        {
            throw new GlyphRendererException($"Font file '{fontPath}' is not a supported TrueType/OpenType font (unrecognized sfnt version 0x{sfntVersion:X8}).");
        }

        ushort numTables = BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(4, 2));
        var tables = new Dictionary<string, TableRecord>(StringComparer.Ordinal);
        int offset = 12;
        for (int i = 0; i < numTables; i++)
        {
            if (offset + 16 > data.Length)
            {
                break;
            }

            var tag = Encoding.ASCII.GetString(data, offset, 4);
            uint checksum = BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(offset + 4, 4));
            uint tableOffset = BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(offset + 8, 4));
            uint tableLength = BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(offset + 12, 4));
            tables[tag] = new TableRecord(tag, checksum, tableOffset, tableLength);
            offset += 16;
        }

        var font = new TrueTypeFont(data, tables);

        if (isCff)
        {
            // CFF-based fonts (OTTO) are not supported by the glyf rasterizer.
            font.ReadNameTable(fontPath);
            throw new GlyphRendererException(
                $"Font file '{fontPath}' uses CFF outlines ('OTTO'), which are not supported by the rasterizer. " +
                "Convert to a TrueType-outline font (.ttf) or use a different font.");
        }

        font.ParseHead(fontPath);
        font.ParseMaxp(fontPath);
        font.ParseHhea(fontPath);
        font.ParseHmtx(fontPath);
        font.ParseLoca(fontPath);
        font.ParseCmap(fontPath);
        font.ReadNameTable(fontPath);
        return font;
    }

    private void ParseHead(string fontPath)
    {
        if (!_tables.TryGetValue("head", out var head))
        {
            throw new GlyphRendererException($"Font '{fontPath}' is missing the 'head' table.");
        }

        if (head.Offset + 54 > _data.Length)
        {
            throw new GlyphRendererException($"Font '{fontPath}' has a truncated 'head' table.");
        }

        _unitsPerEm = BinaryPrimitives.ReadUInt16BigEndian(_data.AsSpan((int)head.Offset + 18, 2));
        short xMin = BinaryPrimitives.ReadInt16BigEndian(_data.AsSpan((int)head.Offset + 36, 2));
        short yMin = BinaryPrimitives.ReadInt16BigEndian(_data.AsSpan((int)head.Offset + 38, 2));
        short xMax = BinaryPrimitives.ReadInt16BigEndian(_data.AsSpan((int)head.Offset + 40, 2));
        short yMax = BinaryPrimitives.ReadInt16BigEndian(_data.AsSpan((int)head.Offset + 42, 2));
        _ = xMin;
        _ = yMin;
        _ = xMax;
        _ = yMax;

        if (_unitsPerEm <= 0)
        {
            throw new GlyphRendererException($"Font '{fontPath}' has an invalid unitsPerEm ({_unitsPerEm}).");
        }
    }

    private void ParseMaxp(string fontPath)
    {
        if (!_tables.TryGetValue("maxp", out var maxp))
        {
            throw new GlyphRendererException($"Font '{fontPath}' is missing the 'maxp' table.");
        }

        // version 0x00005000 has no numGlyphs; use 0 in that case.
        uint version = BinaryPrimitives.ReadUInt32BigEndian(_data.AsSpan((int)maxp.Offset, 4));
        _numGlyphs = version == 0x00005000
            ? 0
            : BinaryPrimitives.ReadUInt16BigEndian(_data.AsSpan((int)maxp.Offset + 4, 2));
    }

    private void ParseHhea(string fontPath)
    {
        if (!_tables.TryGetValue("hhea", out var hhea))
        {
            throw new GlyphRendererException($"Font '{fontPath}' is missing the 'hhea' table.");
        }

        _ascender = BinaryPrimitives.ReadInt16BigEndian(_data.AsSpan((int)hhea.Offset + 4, 2));
        _descender = BinaryPrimitives.ReadInt16BigEndian(_data.AsSpan((int)hhea.Offset + 6, 2));
        _numberOfHMetrics = BinaryPrimitives.ReadUInt16BigEndian(_data.AsSpan((int)hhea.Offset + 34, 2));
    }

    private void ParseHmtx(string fontPath)
    {
        if (!_tables.TryGetValue("hmtx", out var hmtx))
        {
            throw new GlyphRendererException($"Font '{fontPath}' is missing the 'hmtx' table.");
        }

        _hmtxOffset = hmtx.Offset;
    }

    private void ParseLoca(string fontPath)
    {
        if (!_tables.TryGetValue("loca", out var loca) || !_tables.TryGetValue("glyf", out var glyf))
        {
            throw new GlyphRendererException($"Font '{fontPath}' is missing the 'loca' or 'glyf' table (required for TrueType outlines).");
        }

        // head.indexToLocFormat: 0 = short (offsets/2), 1 = long.
        var head = _tables["head"];
        short indexToLocFormat = BinaryPrimitives.ReadInt16BigEndian(_data.AsSpan((int)head.Offset + 50, 2));
        _locaShortFormat = indexToLocFormat == 0;
        _locaOffset = loca.Offset;
        _glyfOffset = glyf.Offset;
    }

    private void ParseCmap(string fontPath)
    {
        if (!_tables.TryGetValue("cmap", out var cmap))
        {
            throw new GlyphRendererException($"Font '{fontPath}' is missing the 'cmap' table.");
        }

        _cmapOffset = cmap.Offset;
    }

    private void ReadNameTable(string fontPath)
    {
        if (!_tables.TryGetValue("name", out var name))
        {
            return;
        }

        try
        {
            int offset = (int)name.Offset;
            if (offset + 6 > _data.Length)
            {
                return;
            }

            ushort count = BinaryPrimitives.ReadUInt16BigEndian(_data.AsSpan(offset + 2, 2));
            ushort stringOffset = BinaryPrimitives.ReadUInt16BigEndian(_data.AsSpan(offset + 4, 2));
            for (int i = 0; i < count; i++)
            {
                int record = offset + 6 + i * 12;
                if (record + 12 > _data.Length)
                {
                    break;
                }

                ushort nameId = BinaryPrimitives.ReadUInt16BigEndian(_data.AsSpan(record + 6, 2));
                ushort length = BinaryPrimitives.ReadUInt16BigEndian(_data.AsSpan(record + 8, 2));
                ushort strOff = BinaryPrimitives.ReadUInt16BigEndian(_data.AsSpan(record + 10, 2));
                // Family name = nameId 1 (typographic family) or 16 (preferred family).
                if (nameId is 1 or 16 && FamilyName is null)
                {
                    int start = offset + stringOffset + strOff;
                    if (start >= 0 && start + length <= _data.Length)
                    {
                        FamilyName = Encoding.UTF8.GetString(_data, start, length);
                    }
                }
            }
        }
        catch (Exception ex) when (ex is ArgumentOutOfRangeException or IndexOutOfRangeException)
        {
            // Bad name table; ignore — family name is informational only.
            FamilyName = null;
        }
    }

    /// <summary>
    /// Maps a character to a glyph index using the best available <c>cmap</c> subtable.
    /// </summary>
    /// <param name="c">The character (UTF-16 code unit).</param>
    /// <returns>The glyph index, or -1 if no glyph is mapped.</returns>
    public int GetGlyphIndex(char c)
    {
        if (_cmapOffset == 0)
        {
            return -1;
        }

        int cmap = (int)_cmapOffset;
        if (cmap + 4 > _data.Length)
        {
            return -1;
        }

        ushort version = BinaryPrimitives.ReadUInt16BigEndian(_data.AsSpan(cmap, 2));
        ushort numTables = BinaryPrimitives.ReadUInt16BigEndian(_data.AsSpan(cmap + 2, 2));
        int bestFormat = 0;
        int bestSubtableOffset = -1;

        for (int i = 0; i < numTables; i++)
        {
            int record = cmap + 4 + i * 8;
            if (record + 8 > _data.Length)
            {
                break;
            }

            ushort platformId = BinaryPrimitives.ReadUInt16BigEndian(_data.AsSpan(record, 2));
            ushort encodingId = BinaryPrimitives.ReadUInt16BigEndian(_data.AsSpan(record + 2, 2));
            uint subtableOffset = BinaryPrimitives.ReadUInt32BigEndian(_data.AsSpan(record + 4, 4));
            int subtable = cmap + (int)subtableOffset;
            if (subtable + 2 > _data.Length)
            {
                continue;
            }

            ushort format = BinaryPrimitives.ReadUInt16BigEndian(_data.AsSpan(subtable, 2));
            int score = ScoreCmap(platformId, encodingId, format);
            if (score > bestFormat)
            {
                bestFormat = score;
                bestSubtableOffset = subtable;
            }
        }

        if (bestSubtableOffset < 0)
        {
            return -1;
        }

        return MapCmapChar(bestSubtableOffset, c);
    }

    private static int ScoreCmap(ushort platformId, ushort encodingId, ushort format)
    {
        // Prefer (3,1) Unicode BMP, then (0,x) Unicode, then (3,10) UCS-4 (surrogate handling below).
        if (platformId == 3 && encodingId == 1 && format is 4 or 12)
        {
            return 5;
        }

        if (platformId == 0 && format is 4 or 12)
        {
            return 4;
        }

        if (platformId == 3 && encodingId == 10 && format == 12)
        {
            return 3;
        }

        if (format is 4 or 12)
        {
            return 1;
        }

        return 0;
    }

    private int MapCmapChar(int subtable, char c)
    {
        if (subtable + 2 > _data.Length)
        {
            return -1;
        }

        ushort format = BinaryPrimitives.ReadUInt16BigEndian(_data.AsSpan(subtable, 2));
        return format switch
        {
            4 => MapCmapFormat4(subtable, c),
            12 => MapCmapFormat12(subtable, c),
            _ => -1
        };
    }

    private int MapCmapFormat4(int subtable, char c)
    {
        if (subtable + 14 > _data.Length)
        {
            return -1;
        }

        ushort segCountX2 = BinaryPrimitives.ReadUInt16BigEndian(_data.AsSpan(subtable + 6, 2));
        int segCount = segCountX2 / 2;
        int endCodeOffset = subtable + 14;
        int startCodeOffset = endCodeOffset + segCountX2 + 2;  // + reservedPad
        int idDeltaOffset = startCodeOffset + segCountX2;
        int idRangeOffsetOffset = idDeltaOffset + segCountX2;

        ushort c16 = c;
        for (int i = 0; i < segCount; i++)
        {
            ushort endCode = BinaryPrimitives.ReadUInt16BigEndian(_data.AsSpan(endCodeOffset + i * 2, 2));
            ushort startCode = BinaryPrimitives.ReadUInt16BigEndian(_data.AsSpan(startCodeOffset + i * 2, 2));
            if (c16 >= startCode && c16 <= endCode)
            {
                short idDelta = BinaryPrimitives.ReadInt16BigEndian(_data.AsSpan(idDeltaOffset + i * 2, 2));
                ushort idRangeOffset = BinaryPrimitives.ReadUInt16BigEndian(_data.AsSpan(idRangeOffsetOffset + i * 2, 2));
                if (idRangeOffset == 0)
                {
                    return (c16 + idDelta) & 0xFFFF;
                }

                int glyphAddr = idRangeOffsetOffset + i * 2 + idRangeOffset + (c16 - startCode) * 2;
                if (glyphAddr + 2 > _data.Length)
                {
                    return -1;
                }

                ushort glyph = BinaryPrimitives.ReadUInt16BigEndian(_data.AsSpan(glyphAddr, 2));
                if (glyph == 0)
                {
                    return 0; // .notdef
                }

                return (glyph + idDelta) & 0xFFFF;
            }
        }

        return -1;
    }

    private int MapCmapFormat12(int subtable, char c)
    {
        if (subtable + 16 > _data.Length)
        {
            return -1;
        }

        uint numGroups = BinaryPrimitives.ReadUInt32BigEndian(_data.AsSpan(subtable + 12, 4));
        uint cp = c;
        for (uint i = 0; i < numGroups; i++)
        {
            int group = subtable + 16 + (int)(i * 12);
            if (group + 12 > _data.Length)
            {
                return -1;
            }

            uint startCharCode = BinaryPrimitives.ReadUInt32BigEndian(_data.AsSpan(group, 4));
            uint endCharCode = BinaryPrimitives.ReadUInt32BigEndian(_data.AsSpan(group + 4, 4));
            uint startGlyphId = BinaryPrimitives.ReadUInt32BigEndian(_data.AsSpan(group + 8, 4));
            if (cp >= startCharCode && cp <= endCharCode)
            {
                return (int)(startGlyphId + (cp - startCharCode));
            }
        }

        return -1;
    }

    /// <summary>
    /// Gets the horizontal advance width (in font units) for a glyph index.
    /// </summary>
    public int GetAdvanceWidth(int glyphIndex)
    {
        if (_hmtxOffset == 0 || glyphIndex < 0)
        {
            return 0;
        }

        // hmtx: for glyphs beyond numberOfHMetrics, repeat the last advance.
        if (glyphIndex >= _numberOfHMetrics)
        {
            glyphIndex = Math.Max(0, _numberOfHMetrics - 1);
        }

        int offset = (int)_hmtxOffset + glyphIndex * 4;
        if (offset + 2 > _data.Length)
        {
            return 0;
        }

        return BinaryPrimitives.ReadUInt16BigEndian(_data.AsSpan(offset, 2));
    }

    /// <summary>Gets the ascender from the hhea table (font units).</summary>
    public int Ascender => _ascender;

    /// <summary>Gets the descender from the hhea table (font units, typically negative).</summary>
    public int Descender => _descender;

    /// <summary>Gets the units per em from the head table.</summary>
    public int UnitsPerEm => _unitsPerEm;

    /// <summary>Gets the total number of glyphs in the font.</summary>
    public int NumGlyphs => _numGlyphs;

    /// <summary>
    /// Returns the glyph outline (as decoded from the <c>glyf</c> table) or null
    /// when the glyph is empty or the index is out of range.
    /// </summary>
    public GlyphOutline? GetOutline(int glyphIndex)
    {
        if (_glyfOffset == 0 || _locaOffset == 0 || glyphIndex < 0 || (_numGlyphs > 0 && glyphIndex >= _numGlyphs))
        {
            return null;
        }

        int loca0;
        int loca1;
        try
        {
            if (_locaShortFormat)
            {
                int off0 = (int)_locaOffset + glyphIndex * 2;
                int off1 = (int)_locaOffset + (glyphIndex + 1) * 2;
                loca0 = BinaryPrimitives.ReadUInt16BigEndian(_data.AsSpan(off0, 2)) * 2;
                loca1 = BinaryPrimitives.ReadUInt16BigEndian(_data.AsSpan(off1, 2)) * 2;
            }
            else
            {
                int off0 = (int)_locaOffset + glyphIndex * 4;
                int off1 = (int)_locaOffset + (glyphIndex + 1) * 4;
                long l0 = BinaryPrimitives.ReadUInt32BigEndian(_data.AsSpan(off0, 4));
                long l1 = BinaryPrimitives.ReadUInt32BigEndian(_data.AsSpan(off1, 4));
                if (l0 > int.MaxValue || l1 > int.MaxValue)
                {
                    return null;
                }

                loca0 = (int)l0;
                loca1 = (int)l1;
            }
        }
        catch (Exception ex) when (ex is ArgumentOutOfRangeException or IndexOutOfRangeException)
        {
            return null;
        }

        if (loca0 >= loca1)
        {
            return null; // empty glyph
        }

        int glyfStart = (int)_glyfOffset + loca0;
        int glyfEnd = (int)_glyfOffset + loca1;
        if (glyfStart < 0 || glyfEnd > _data.Length || glyfStart + 10 > glyfEnd)
        {
            return null;
        }

        short numberOfContours = BinaryPrimitives.ReadInt16BigEndian(_data.AsSpan(glyfStart, 2));
        short xMin = BinaryPrimitives.ReadInt16BigEndian(_data.AsSpan(glyfStart + 2, 2));
        short yMin = BinaryPrimitives.ReadInt16BigEndian(_data.AsSpan(glyfStart + 4, 2));
        short xMax = BinaryPrimitives.ReadInt16BigEndian(_data.AsSpan(glyfStart + 6, 2));
        short yMax = BinaryPrimitives.ReadInt16BigEndian(_data.AsSpan(glyfStart + 8, 2));

        if (numberOfContours >= 0)
        {
            return DecodeSimpleGlyph(glyfStart, glyfEnd, numberOfContours, xMin, yMin, xMax, yMax);
        }

        return DecodeCompositeGlyph(glyfStart, glyfEnd, xMin, yMin, xMax, yMax);
    }

    private GlyphOutline? DecodeSimpleGlyph(int start, int end, short numberOfContours, short xMin, short yMin, short xMax, short yMax)
    {
        int pos = start + 10;

        // endPtsOfContours
        var endPts = new int[numberOfContours];
        for (int i = 0; i < numberOfContours; i++)
        {
            if (pos + 2 > end)
            {
                return null;
            }

            endPts[i] = BinaryPrimitives.ReadUInt16BigEndian(_data.AsSpan(pos, 2));
            pos += 2;
        }

        if (pos + 2 > end)
        {
            return null;
        }

        int instructionLength = BinaryPrimitives.ReadUInt16BigEndian(_data.AsSpan(pos, 2));
        pos += 2 + instructionLength;
        if (pos > end)
        {
            return null;
        }

        int pointCount = numberOfContours == 0 ? 0 : endPts[^1] + 1;
        if (pointCount <= 0)
        {
            return null;
        }

        // flags
        var flags = new byte[pointCount];
        for (int i = 0; i < pointCount;)
        {
            if (pos >= end)
            {
                return null;
            }

            byte flag = _data[pos++];
            flags[i++] = flag;
            if ((flag & 0x08) != 0) // repeat flag
            {
                if (pos >= end)
                {
                    return null;
                }

                int repeat = _data[pos++];
                for (int r = 0; r < repeat && i < pointCount; r++)
                {
                    flags[i++] = flag;
                }
            }
        }

        // x coordinates (values are deltas, byte or int16 depending on flag)
        var xs = new int[pointCount];
        int x = 0;
        for (int i = 0; i < pointCount; i++)
        {
            if ((flags[i] & 0x02) != 0) // X_SHORT
            {
                if (pos >= end)
                {
                    return null;
                }

                byte v = _data[pos++];
                x += (flags[i] & 0x10) != 0 ? v : -v;
            }
            else if ((flags[i] & 0x10) == 0) // X_SAME (when X_SHORT unset and this bit set → same)
            {
                if (pos + 2 > end)
                {
                    return null;
                }

                x += BinaryPrimitives.ReadInt16BigEndian(_data.AsSpan(pos, 2));
                pos += 2;
            }

            xs[i] = x;
        }

        // y coordinates
        var ys = new int[pointCount];
        int y = 0;
        for (int i = 0; i < pointCount; i++)
        {
            if ((flags[i] & 0x04) != 0) // Y_SHORT
            {
                if (pos >= end)
                {
                    return null;
                }

                byte v = _data[pos++];
                y += (flags[i] & 0x20) != 0 ? v : -v;
            }
            else if ((flags[i] & 0x20) == 0) // Y_SAME
            {
                if (pos + 2 > end)
                {
                    return null;
                }

                y += BinaryPrimitives.ReadInt16BigEndian(_data.AsSpan(pos, 2));
                pos += 2;
            }

            ys[i] = y;
        }

        // on-curve flags come from the low bit of each flag
        var onCurve = new bool[pointCount];
        for (int i = 0; i < pointCount; i++)
        {
            onCurve[i] = (flags[i] & 0x01) != 0;
        }

        return new GlyphOutline(xMin, yMin, xMax, yMax, endPts, xs, ys, onCurve);
    }

    private GlyphOutline? DecodeCompositeGlyph(int start, int end, short xMin, short yMin, short xMax, short yMax)
    {
        // Composite glyphs: recursively decode component glyphs and merge.
        var endPts = new List<int>();
        var xs = new List<int>();
        var ys = new List<int>();
        var onCurve = new List<bool>();

        int pos = start + 10;
        while (pos + 4 <= end)
        {
            ushort flags = BinaryPrimitives.ReadUInt16BigEndian(_data.AsSpan(pos, 2));
            ushort glyphIndex = BinaryPrimitives.ReadUInt16BigEndian(_data.AsSpan(pos + 2, 2));
            pos += 4;

            int arg1 = 0;
            int arg2 = 0;
            if ((flags & 0x0001) != 0) // ARG_1_AND_2_ARE_WORDS
            {
                if (pos + 4 > end)
                {
                    break;
                }

                arg1 = BinaryPrimitives.ReadInt16BigEndian(_data.AsSpan(pos, 2));
                arg2 = BinaryPrimitives.ReadInt16BigEndian(_data.AsSpan(pos + 2, 2));
                pos += 4;
            }
            else
            {
                if (pos + 2 > end)
                {
                    break;
                }

                arg1 = _data[pos];
                arg2 = _data[pos + 1];
                pos += 2;
            }

            // If ARGS_ARE_XY_VALUES, arg1/arg2 are offsets; else they are point indices (rare for captcha fonts).
            int dx = (flags & 0x0002) != 0 ? arg1 : 0;
            int dy = (flags & 0x0002) != 0 ? arg2 : 0;

            short xScale = 1;
            short yScale = 1;
            // WE_HAVE_A_SCALE
            if ((flags & 0x0008) != 0)
            {
                if (pos + 2 > end)
                {
                    break;
                }

                xScale = yScale = BinaryPrimitives.ReadInt16BigEndian(_data.AsSpan(pos, 2));
                pos += 2;
            }
            else if ((flags & 0x0040) != 0) // WE_HAVE_AN_X_AND_Y_SCALE
            {
                if (pos + 4 > end)
                {
                    break;
                }

                xScale = BinaryPrimitives.ReadInt16BigEndian(_data.AsSpan(pos, 2));
                yScale = BinaryPrimitives.ReadInt16BigEndian(_data.AsSpan(pos + 2, 2));
                pos += 4;
            }
            else if ((flags & 0x0080) != 0) // WE_HAVE_A_TWO_BY_TWO
            {
                if (pos + 8 > end)
                {
                    break;
                }

                pos += 8; // xscale, scale01, scale10, yscale — simplified: skip
            }

            var component = GetOutline(glyphIndex);
            if (component is null)
            {
                // skip empty/unsupported component
                continue;
            }

            int baseCount = xs.Count;
            for (int c = 0; c < component.PointCount; c++)
            {
                int cx = component.Xs[c];
                int cy = component.Ys[c];
                // Apply scale then translation (offsets are in font units).
                int sx = (int)(cx * xScale / 1) + dx;
                int sy = (int)(cy * yScale / 1) + dy;
                xs.Add(sx);
                ys.Add(sy);
                onCurve.Add(component.OnCurve[c]);
            }

            // Merge contour end points
            foreach (var endPt in component.EndPoints)
            {
                endPts.Add(endPt + baseCount);
            }

            if ((flags & 0x0020) != 0) // MORE_COMPONENTS
            {
                continue;
            }

            break;
        }

        if (xs.Count == 0)
        {
            return null;
        }

        return new GlyphOutline(xMin, yMin, xMax, yMax, endPts.ToArray(), xs.ToArray(), ys.ToArray(), onCurve.ToArray());
    }

    private readonly record struct TableRecord(string Tag, uint Checksum, uint Offset, uint Length);
}

/// <summary>
/// A decoded (possibly composite) glyph outline in font units.
/// </summary>
internal sealed class GlyphOutline
{
    public GlyphOutline(
        short xMin,
        short yMin,
        short xMax,
        short yMax,
        int[] endPoints,
        int[] xs,
        int[] ys,
        bool[] onCurve)
    {
        XMin = xMin;
        YMin = yMin;
        XMax = xMax;
        YMax = yMax;
        EndPoints = endPoints;
        Xs = xs;
        Ys = ys;
        OnCurve = onCurve;
    }

    public short XMin { get; }
    public short YMin { get; }
    public short XMax { get; }
    public short YMax { get; }
    public int[] EndPoints { get; }
    public int[] Xs { get; }
    public int[] Ys { get; }
    public bool[] OnCurve { get; }

    public int PointCount => Xs.Length;
}