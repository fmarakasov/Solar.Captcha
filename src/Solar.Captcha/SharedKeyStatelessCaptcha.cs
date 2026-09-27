using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;

namespace Solar.Captcha;

/// <summary>Options for the shared-key stateless letter captcha flow.</summary>
public class SharedKeyStatelessLetterCaptchaOptions : SharedKeyStatelessImageCaptchaOptions
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
/// Base class for stateless captcha flows whose token is protected by a shared 256-bit key. It seals
/// the generated code into an opaque token and validates a later submission, leaving code generation,
/// comparison and rendering to subclasses.
/// </summary>
public abstract class SharedKeyStatelessCaptcha<TOptions> : IStatelessCaptcha<TOptions>
    where TOptions : SharedKeyStatelessImageCaptchaOptions
{
    private const int MaxBlockedCodeRetries = 100;

    private readonly byte[] _sharedKey;

    /// <summary>Creates the flow, validating the shared key up front.</summary>
    protected SharedKeyStatelessCaptcha(TOptions options)
    {
        Options = options ?? throw new ArgumentNullException(nameof(options));

        if (string.IsNullOrWhiteSpace(options.SharedKey))
        {
            throw new ArgumentException("SharedKey is required for cluster deployments", nameof(options));
        }

        try
        {
            _sharedKey = Convert.FromBase64String(options.SharedKey);
            if (_sharedKey.Length != 32) // 256 bits
            {
                throw new ArgumentException("SharedKey must be a 256-bit (32-byte) key encoded as Base64");
            }
        }
        catch (FormatException)
        {
            throw new ArgumentException("SharedKey must be a valid Base64 encoded string");
        }
    }

    /// <inheritdoc />
    public TOptions Options { get; }

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
        var encryptedToken = EncryptData(serializedData);

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
            var decryptedData = DecryptData(captchaToken);
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

    private string EncryptData(string plainText)
    {
        using var aes = Aes.Create();
        aes.Key = _sharedKey;
        aes.GenerateIV();

        using var encryptor = aes.CreateEncryptor();
        using var ms = new MemoryStream();

        // Write IV first
        ms.Write(aes.IV, 0, aes.IV.Length);

        using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
        using (var writer = new StreamWriter(cs))
        {
            writer.Write(plainText);
        }

        return Convert.ToBase64String(ms.ToArray());
    }

    private string DecryptData(string cipherText)
    {
        var cipherBytes = Convert.FromBase64String(cipherText);

        using var aes = Aes.Create();
        aes.Key = _sharedKey;

        var iv = new byte[16];
        Array.Copy(cipherBytes, 0, iv, 0, 16);
        aes.IV = iv;

        using var decryptor = aes.CreateDecryptor();
        using var ms = new MemoryStream(cipherBytes, 16, cipherBytes.Length - 16);
        using var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read);
        using var reader = new StreamReader(cs);
        return reader.ReadToEnd();
    }
}

/// <summary>Shared-key stateless letter captcha.</summary>
public class SharedKeyStatelessLetterCaptcha(
    ILetterCaptchaImageRenderer imageRenderer,
    SharedKeyStatelessLetterCaptchaOptions options)
    : SharedKeyStatelessCaptcha<SharedKeyStatelessLetterCaptchaOptions>(options)
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

/// <summary>Shared-key stateless clock captcha.</summary>
public class SharedKeyStatelessClockCaptcha(
    IClockCaptchaImageRenderer imageRenderer,
    SharedKeyStatelessClockCaptchaOptions options)
    : SharedKeyStatelessCaptcha<SharedKeyStatelessClockCaptchaOptions>(options)
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