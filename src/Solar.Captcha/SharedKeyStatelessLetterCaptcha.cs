namespace Solar.Captcha;

public class SharedKeyStatelessLetterCaptchaOptions : SharedKeyStatelessCaptchaOptions
{
    public string Letters { get; set; } = "2346789ABCDGHKMNPRUVWXYZ";
    public int CodeLength { get; set; } = 4;
}

public class SharedKeyStatelessLetterCaptcha(ICaptchaImageRenderer imageRenderer, SharedKeyStatelessLetterCaptchaOptions options)
    : SharedKeyStatelessCaptcha(imageRenderer, options)
{
    public override string GenerateCaptchaCode() =>
        SecureCaptchaGenerator.GenerateSecureCaptchaCode(options.Letters, options.CodeLength);
}