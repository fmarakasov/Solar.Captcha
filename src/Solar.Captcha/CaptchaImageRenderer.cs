using System;
using Solar.Captcha.GlyphRenderer;

namespace Solar.Captcha;

/// <summary>
/// Draws a captcha code using a pre-built <see cref="GlyphSet"/>.
/// </summary>
/// <remarks>
/// The glyphs are supplied up front rather than looked up per character, so this type never
/// touches a font or the static glyph table, and it cannot fail for a character the application
/// declared it would draw (ADR-009). Registered as a singleton, so <see cref="_random"/> must be
/// safe for concurrent use; the default registration passes <see cref="Random.Shared"/>.
/// </remarks>
internal sealed class CaptchaImageRenderer(GlyphSet glyphSet, Random random) : ICaptchaImageRenderer
{
    private const int MinRotationDegrees = -10;
    private const int MaxRotationDegrees = 10;
    private const int TextPadding = 5;

    private readonly GlyphSet _glyphSet = glyphSet ?? throw new ArgumentNullException(nameof(glyphSet));
    private readonly Random _random = random ?? throw new ArgumentNullException(nameof(random));

    /// <inheritdoc />
    public byte[] Render(
        int width,
        int height,
        string captchaCode,
        CaptchaFontStyle fontStyle = CaptchaFontStyle.Regular,
        bool drawLines = true)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        ArgumentException.ThrowIfNullOrWhiteSpace(captchaCode);

        EnsureDrawable(captchaCode);

        using var img = new CaptchaImage(width, height);

        DrawBackground(img);

        if (drawLines)
        {
            DrawRandomLines(img, width, height);
        }

        DrawCaptchaText(img, captchaCode, fontStyle, width, height);

        return img.EncodePng();
    }

    private void EnsureDrawable(string captchaCode)
    {
        var missing = _glyphSet.FindMissing(captchaCode);
        if (missing.Count == 0)
        {
            return;
        }

        var names = new string[missing.Count];
        for (var i = 0; i < missing.Count; i++)
        {
            names[i] = GlyphSet.Describe(missing[i]);
        }

        throw new ArgumentException(
            $"The captcha code contains {missing.Count} character(s) that the configured glyph set cannot draw: " +
            $"{string.Join(", ", names)}. The glyph set covers '{_glyphSet.Charset}'.",
            nameof(captchaCode));
    }

    private void DrawCaptchaText(
        CaptchaImage img,
        string captchaCode,
        CaptchaFontStyle fontStyle,
        int width,
        int height)
    {
        var availableHeight = height - (TextPadding * 2);

        // Target: text block occupies ~70% of image width
        var targetTextWidth = width * 0.7f;
        var totalGaps = Math.Max(0, captchaCode.Length - 1);
        var avgGapWidth = 2.5f; // average of _random.Next(1, 4)
        var availableForChars = targetTextWidth - totalGaps * avgGapWidth;
        var scaleByWidth = availableForChars / (captchaCode.Length * CaptchaFont.GlyphWidth);
        var scaleByHeight = (float)availableHeight / CaptchaFont.GlyphHeight;
        var scale = Math.Max(1, (int)Math.Round(Math.Min(scaleByWidth, scaleByHeight)));

        // Center the text block horizontally
        var charWidth = CaptchaFont.GlyphWidth * scale;
        var estimatedTotalWidth = captchaCode.Length * charWidth + (int)(totalGaps * avgGapWidth);
        var currentX = Math.Max(TextPadding, (width - estimatedTotalWidth) / 2);

        for (var i = 0; i < captchaCode.Length; i++)
        {
            var character = captchaCode[i];

            var charW = img.MeasureCharWidth(scale, fontStyle);
            var charH = img.MeasureCharHeight(scale);

            var maxX = Math.Max(TextPadding, width - TextPadding - charW);
            var maxY = Math.Max(TextPadding, height - TextPadding - charH);

            var x = Math.Min(currentX + _random.Next(0, 3), maxX);
            var baseY = (height - charH) / 2;
            var y = Math.Max(TextPadding, Math.Min(baseY + _random.Next(-4, 5), maxY));

            var degrees = _random.Next(MinRotationDegrees, MaxRotationDegrees);

            // The set is total over its charset, so this lookup cannot fail here.
            _glyphSet.TryGetGlyph(character, out var glyph);

            GetRandomDeepColor(out var r, out var g, out var b);
            img.DrawGlyph(glyph, x, y, scale, r, g, b, fontStyle, degrees);

            currentX += charW + _random.Next(1, 4);
        }
    }

    private void DrawBackground(CaptchaImage img)
    {
        img.Fill(
            (byte)_random.Next(220, 256),
            (byte)_random.Next(220, 256),
            (byte)_random.Next(220, 256));
    }

    private void DrawRandomLines(CaptchaImage img, int width, int height)
    {
        var lineCount = _random.Next(2, 5);

        for (var i = 0; i < lineCount; i++)
        {
            GetRandomLightColor(out var r, out var g, out var b);
            var thickness = _random.NextSingle() * 2f + 0.5f;
            img.DrawLine(
                _random.Next(0, width), _random.Next(0, height),
                _random.Next(0, width), _random.Next(0, height),
                r, g, b, thickness);
        }
    }

    private void GetRandomDeepColor(out byte r, out byte g, out byte b)
    {
        const int maxColorValue = 120;
        r = (byte)_random.Next(0, maxColorValue);
        g = (byte)_random.Next(0, maxColorValue);
        b = (byte)_random.Next(0, maxColorValue);
    }

    private void GetRandomLightColor(out byte r, out byte g, out byte b)
    {
        const int minColorValue = 150;
        const int maxColorValue = 200;
        r = (byte)_random.Next(minColorValue, maxColorValue);
        g = (byte)_random.Next(minColorValue, maxColorValue);
        b = (byte)_random.Next(minColorValue, maxColorValue);
    }
}
