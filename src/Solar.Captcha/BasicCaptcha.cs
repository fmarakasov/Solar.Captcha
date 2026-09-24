namespace Solar.Captcha;

public class BasicLetterCaptchaOptions : SessionBasedCaptchaOptions
{
    public string Letters { get; set; } = string.Empty;

    public int CodeLength { get; set; }
}

public class BasicLetterCaptcha(ICaptchaImageRenderer imageRenderer, BasicLetterCaptchaOptions options)
    : SessionBasedCaptcha(imageRenderer, options)
{
    public override string GenerateCaptchaCode() =>
        SecureCaptchaGenerator.GenerateSecureCaptchaCode(options.Letters, options.CodeLength);
}