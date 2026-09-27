using Microsoft.AspNetCore.DataProtection;
using System;
using System.Linq;
using System.Text.Json;

namespace Solar.Captcha;

/// <summary>Options for the stateless (Data Protection) letter captcha flow.</summary>
public class StatelessLetterCaptchaOptions : StatelessImageCaptchaOptions
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
/// Base class for stateless captcha flows whose token is protected by ASP.NET Data Protection. It
/// seals the generated code into an opaque token and validates a later submission, leaving code
/// generation, comparison and rendering to subclasses.
/// </summary>
public abstract class StatelessCaptcha<TOptions>(
    IDataProtectionProvider dataProtectionProvider,
    TOptions options) : IStatelessCaptcha<TOptions>
    where TOptions : StatelessImageCaptchaOptions
{
    private const int MaxBlockedCodeRetries = 100;

    private readonly IDataProtector _dataProtector =
        dataProtectionProvider.CreateProtector("Solar.Captcha.Stateless");

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
    public StatelessCaptchaResult GenerateCaptcha(int? width = null, int? height = null)
    {
        var captchaCode = GenerateAllowedCaptchaCode();

        var imageBytes = RenderImage(captchaCode, width ?? Options.Width, height ?? Options.Height);

        var tokenData = new CaptchaTokenData
        {
            Code = captchaCode,
            ExpirationTime = DateTimeOffset.UtcNow.Add(Options.TokenExpiration)
        };

        var serializedData = JsonSerializer.Serialize(tokenData);
        var encryptedToken = _dataProtector.Protect(serializedData);

        return new StatelessCaptchaResult
        {
            ImageBytes = imageBytes,
            Token = encryptedToken
        };
    }

    /// <inheritdoc />
    public bool Validate(string? userInputCaptcha, string? captchaToken)
    {
        if (string.IsNullOrWhiteSpace(userInputCaptcha) || string.IsNullOrWhiteSpace(captchaToken))
        {
            return false;
        }

        try
        {
            var decryptedData = _dataProtector.Unprotect(captchaToken);
            var tokenData = JsonSerializer.Deserialize<CaptchaTokenData>(decryptedData);

            if (tokenData is null || DateTimeOffset.UtcNow > tokenData.ExpirationTime)
            {
                return false; // Token expired
            }

            return CodesMatch(userInputCaptcha, tokenData.Code);
        }
        catch
        {
            return false; // Invalid or corrupted token
        }
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
}

/// <summary>Stateless letter captcha protected by Data Protection.</summary>
public class StatelessLetterCaptcha(
    IDataProtectionProvider dataProtectionProvider,
    ILetterCaptchaImageRenderer imageRenderer,
    StatelessLetterCaptchaOptions options)
    : StatelessCaptcha<StatelessLetterCaptchaOptions>(dataProtectionProvider, options)
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

/// <summary>Stateless clock captcha protected by Data Protection.</summary>
public class StatelessClockCaptcha(
    IDataProtectionProvider dataProtectionProvider,
    IClockCaptchaImageRenderer imageRenderer,
    StatelessClockCaptchaOptions options)
    : StatelessCaptcha<StatelessClockCaptchaOptions>(dataProtectionProvider, options)
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

public class CaptchaTokenData
{
    public string Code { get; set; } = string.Empty;
    public DateTimeOffset ExpirationTime { get; set; }
}