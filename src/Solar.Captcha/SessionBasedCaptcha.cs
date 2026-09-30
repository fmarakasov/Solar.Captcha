using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.IO;
using System.Linq;

namespace Solar.Captcha;

/// <summary>Options for the session-based letter captcha flow.</summary>
public class SessionBasedLetterCaptchaOptions : SessionBasedImageCaptchaOptions
{
    /// <summary>Gets or sets the alphabet the code is drawn from.</summary>
    public string Letters { get; set; } = CaptchaServiceCollectionExtensions.DefaultLetters;

    /// <summary>Gets or sets the number of characters in the generated code.</summary>
    public int CodeLength { get; set; } = 4;

    /// <summary>Gets or sets the glyph style the code is drawn with.</summary>
    public CaptchaFontStyle FontStyle { get; set; } = CaptchaFontStyle.Regular;

    /// <summary>Gets or sets whether to draw the background noise lines.</summary>
    public bool DrawLines { get; set; } = true;
}

/// <summary>
/// Base class for session-based captcha flows. It stores the generated code in the user's session and
/// validates a later submission, leaving code generation, comparison and rendering to subclasses.
/// </summary>
public abstract class SessionBasedCaptcha<TOptions>(TOptions options) : ISessionCaptcha<TOptions>
    where TOptions : SessionBasedImageCaptchaOptions
{
    private const int MaxBlockedCodeRetries = 100;

    /// <inheritdoc />
    public TOptions Options { get; } = options ?? throw new ArgumentNullException(nameof(options));

    /// <inheritdoc />
    public abstract string ContentType { get; }

    /// <summary>Generates a raw captcha code for the challenge.</summary>
    public abstract string GenerateCaptchaCode();

    /// <summary>Renders the image for a code at the given size.</summary>
    protected abstract byte[] RenderImage(string captchaCode, int width, int height);

    /// <summary>Compares a submitted answer with the stored code.</summary>
    protected abstract bool CodesMatch(string? userInputCaptcha, string captchaCode);

    /// <inheritdoc />
    public byte[] GenerateCaptchaImageBytes(ISession httpSession, int? width = null, int? height = null, string? sessionKeyName = null)
    {
        EnsureHttpSession(httpSession);

        var captchaCode = GenerateAllowedCaptchaCode();
        var imageBytes = RenderImage(captchaCode, width ?? Options.Width, height ?? Options.Height);
        httpSession.SetString(sessionKeyName ?? Options.SessionName, captchaCode);
        return imageBytes;
    }

    /// <inheritdoc />
    public FileStreamResult GenerateCaptchaImageFileStream(ISession httpSession, int? width = null, int? height = null, string? sessionKeyName = null)
    {
        var imageBytes = GenerateCaptchaImageBytes(httpSession, width, height, sessionKeyName);
        Stream s = new MemoryStream(imageBytes);
        return new(s, ContentType);
    }

    /// <inheritdoc />
    public bool Validate(string? userInputCaptcha, ISession httpSession, bool dropSession = true, string? sessionKeyName = null)
    {
        if (string.IsNullOrWhiteSpace(userInputCaptcha))
        {
            return false;
        }

        var codeInSession = httpSession.GetString(sessionKeyName ?? Options.SessionName);
        if (codeInSession is null)
        {
            return false;
        }

        var isValid = CodesMatch(userInputCaptcha, codeInSession);

        if (dropSession)
        {
            httpSession.Remove(sessionKeyName ?? Options.SessionName);
        }

        return isValid;
    }

    private string GenerateAllowedCaptchaCode()
    {
        var captchaCode = GenerateCaptchaCode();
        var retries = 0;
        while (Options.BlockedCodes.Contains(captchaCode))
        {
            if (++retries > MaxBlockedCodeRetries)
            {
                throw new InvalidOperationException(
                    $"Unable to generate a captcha code not in BlockedCodes after {MaxBlockedCodeRetries} attempts.");
            }

            captchaCode = GenerateCaptchaCode();
        }

        return captchaCode;
    }

    private static void EnsureHttpSession(ISession httpSession)
    {
        if (null == httpSession)
        {
            throw new ArgumentNullException(nameof(httpSession),
                "Session can not be null, please check if Session is enabled in ASP.NET Core via services.AddSession() and app.UseSession().");
        }
    }
}

/// <summary>Session-based letter captcha: draws glyphs from the glyph set into a PNG.</summary>
public class SessionBasedLetterCaptcha(ILetterCaptchaImageRenderer imageRenderer, SessionBasedLetterCaptchaOptions options)
    : SessionBasedCaptcha<SessionBasedLetterCaptchaOptions>(options)
{
    private readonly ILetterCaptchaImageRenderer _imageRenderer =
        imageRenderer ?? throw new ArgumentNullException(nameof(imageRenderer));

    /// <inheritdoc />
    public override string ContentType => _imageRenderer.ContentType;

    /// <inheritdoc />
    public override string GenerateCaptchaCode() =>
        SecureCaptchaGenerator.GenerateSecureCaptchaCode(Options.Letters, Options.CodeLength);

    /// <inheritdoc />
    protected override byte[] RenderImage(string captchaCode, int width, int height) =>
        _imageRenderer.Render(width, height, captchaCode, Options.FontStyle, Options.DrawLines);

    /// <inheritdoc />
    protected override bool CodesMatch(string? userInputCaptcha, string captchaCode)
    {
        var comparison = Options.IgnoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        return string.Equals(userInputCaptcha, captchaCode, comparison);
    }
}

/// <summary>Session-based clock captcha: renders an animated analog clock as a GIF.</summary>
public class SessionBasedClockCaptcha(IClockCaptchaImageRenderer imageRenderer, SessionBasedClockCaptchaOptions options)
    : SessionBasedCaptcha<SessionBasedClockCaptchaOptions>(options)
{
    private readonly IClockCaptchaImageRenderer _imageRenderer =
        imageRenderer ?? throw new ArgumentNullException(nameof(imageRenderer));

    /// <inheritdoc />
    public override string ContentType => _imageRenderer.ContentType;

    /// <inheritdoc />
    public override string GenerateCaptchaCode() =>
        ClockCaptchaCodeGenerator.Generate(Options.Clock.MinuteStep);

    /// <inheritdoc />
    protected override byte[] RenderImage(string captchaCode, int width, int height)
    {
        var (hours, minutes) = ClockCaptchaCode.Parse(captchaCode);
        return _imageRenderer.Render(width, height, hours, minutes);
    }

    /// <inheritdoc />
    protected override bool CodesMatch(string? userInputCaptcha, string captchaCode) =>
        ClockCaptchaCode.Matches(userInputCaptcha, captchaCode);
}