using Microsoft.AspNetCore.DataProtection;
using System;
using System.Linq;
using System.Text.Json;

namespace Solar.Captcha;

public class StatelessCaptchaOptions
{
    public CaptchaFontStyle FontStyle { get; set; } = CaptchaFontStyle.Regular;
    public bool DrawLines { get; set; } = true;
    public string[] BlockedCodes { get; set; } = [];
    public TimeSpan TokenExpiration { get; set; } = TimeSpan.FromMinutes(5);
}

public abstract class StatelessCaptcha(
    IDataProtectionProvider dataProtectionProvider,
    ICaptchaImageRenderer imageRenderer,
    StatelessCaptchaOptions options) : IStatelessCaptcha
{
    private const int MaxBlockedCodeRetries = 100;

    private readonly IDataProtector _dataProtector = dataProtectionProvider.CreateProtector("Solar.Captcha.Stateless");
    private readonly ICaptchaImageRenderer _imageRenderer =
        imageRenderer ?? throw new ArgumentNullException(nameof(imageRenderer));

    public abstract string GenerateCaptchaCode();

    public StatelessCaptchaResult GenerateCaptcha(int width = 100, int height = 36)
    {
        var captchaCode = GenerateAllowedCaptchaCode();

        var imageBytes = _imageRenderer.Render(width, height, captchaCode, options.FontStyle, options.DrawLines);

        var tokenData = new CaptchaTokenData
        {
            Code = captchaCode,
            ExpirationTime = DateTimeOffset.UtcNow.Add(options.TokenExpiration)
        };

        var serializedData = JsonSerializer.Serialize(tokenData);
        var encryptedToken = _dataProtector.Protect(serializedData);

        return new StatelessCaptchaResult
        {
            ImageBytes = imageBytes,
            Token = encryptedToken
        };
    }

    /// <summary>
    /// Generates a code that is not in <see cref="StatelessCaptchaOptions.BlockedCodes"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when an allowed code could not be produced within the retry budget, which means the
    /// configured letters cannot produce a code outside <see cref="StatelessCaptchaOptions.BlockedCodes"/>.
    /// </exception>
    private string GenerateAllowedCaptchaCode()
    {
        var captchaCode = GenerateCaptchaCode();
        var retries = 0;
        while (options.BlockedCodes.Contains(captchaCode))
        {
            if (++retries > MaxBlockedCodeRetries)
            {
                throw new InvalidOperationException($"Unable to generate a captcha code not in BlockedCodes after {MaxBlockedCodeRetries} attempts.");
            }

            captchaCode = GenerateCaptchaCode();
        }

        return captchaCode;
    }

    public bool Validate(string userInputCaptcha, string captchaToken, bool ignoreCase = true)
    {
        if (string.IsNullOrWhiteSpace(userInputCaptcha) || string.IsNullOrWhiteSpace(captchaToken))
        {
            return false;
        }

        try
        {
            var decryptedData = _dataProtector.Unprotect(captchaToken);
            var tokenData = JsonSerializer.Deserialize<CaptchaTokenData>(decryptedData);

            if (DateTimeOffset.UtcNow > tokenData.ExpirationTime)
            {
                return false; // Token expired
            }

            var comparison = ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            return string.Equals(userInputCaptcha, tokenData.Code, comparison);
        }
        catch
        {
            return false; // Invalid or corrupted token
        }
    }
}

public class CaptchaTokenData
{
    public string Code { get; set; }
    public DateTimeOffset ExpirationTime { get; set; }
}