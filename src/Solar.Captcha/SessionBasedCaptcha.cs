using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.IO;
using System.Linq;

namespace Solar.Captcha;

public class SessionBasedCaptchaOptions
{
    public string SessionName { get; set; } = string.Empty;
    public CaptchaFontStyle FontStyle { get; set; } = CaptchaFontStyle.Regular;
    public bool DrawLines { get; set; } = true;
    public string[] BlockedCodes { get; set; } = [];
}

public abstract class SessionBasedCaptcha(
    ICaptchaImageRenderer imageRenderer,
    SessionBasedCaptchaOptions options) : ISessionBasedCaptcha
{
    private const int MaxBlockedCodeRetries = 100;

    private readonly ICaptchaImageRenderer _imageRenderer =
        imageRenderer ?? throw new ArgumentNullException(nameof(imageRenderer));

    public SessionBasedCaptchaOptions Options { get; } = options ?? throw new ArgumentNullException(nameof(options));

    public abstract string GenerateCaptchaCode();

    public byte[] GenerateCaptchaImageBytes(ISession httpSession, int width = 100, int height = 36, string? sessionKeyName = null)
    {
        EnsureHttpSession(httpSession);

        var captchaCode = GenerateAllowedCaptchaCode();

        var imageBytes = _imageRenderer.Render(width, height, captchaCode, Options.FontStyle, Options.DrawLines);
        httpSession.SetString(sessionKeyName ?? Options.SessionName, captchaCode);
        return imageBytes;
    }

    /// <summary>
    /// Generates a code that is not in <see cref="SessionBasedCaptchaOptions.BlockedCodes"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when an allowed code could not be produced within the retry budget, which means the
    /// configured letters cannot produce a code outside <see cref="SessionBasedCaptchaOptions.BlockedCodes"/>.
    /// </exception>
    private string GenerateAllowedCaptchaCode()
    {
        var captchaCode = GenerateCaptchaCode();
        var retries = 0;
        while (Options.BlockedCodes.Contains(captchaCode))
        {
            if (++retries > MaxBlockedCodeRetries)
            {
                throw new InvalidOperationException($"Unable to generate a captcha code not in BlockedCodes after {MaxBlockedCodeRetries} attempts.");
            }

            captchaCode = GenerateCaptchaCode();
        }

        return captchaCode;
    }

    public FileStreamResult GenerateCaptchaImageFileStream(ISession httpSession, int width = 100, int height = 36, string? sessionKeyName = null)
    {
        EnsureHttpSession(httpSession);

        var captchaCode = GenerateAllowedCaptchaCode();

        var imageBytes = _imageRenderer.Render(width, height, captchaCode, Options.FontStyle, Options.DrawLines);
        httpSession.SetString(sessionKeyName ?? Options.SessionName, captchaCode);
        Stream s = new MemoryStream(imageBytes);
        return new(s, "image/png");
    }

    /// <summary>
    /// Validate Captcha Code
    /// </summary>
    /// <param name="userInputCaptcha">User Input Captcha Code</param>
    /// <param name="httpSession">Current Session</param>
    /// <param name="ignoreCase">Ignore Case (default = true)</param>
    /// <param name="dropSession">Whether to drop session regardless of the validation pass or not (default = true)</param>
    /// <returns>Is Valid Captcha Challenge</returns>
    public bool Validate(string? userInputCaptcha, ISession httpSession, bool ignoreCase = true, bool dropSession = true, string? sessionKeyName = null)
    {
        if (string.IsNullOrWhiteSpace(userInputCaptcha))
        {
            return false;
        }
        var codeInSession = httpSession.GetString(sessionKeyName ?? Options.SessionName);
        var isValid = string.Compare(userInputCaptcha, codeInSession, ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
        if (dropSession)
        {
            httpSession.Remove(sessionKeyName ?? Options.SessionName);
        }
        return isValid == 0;
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
