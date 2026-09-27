namespace Solar.Captcha;

/// <summary>
/// A stateless captcha flow: the generated code is sealed into an opaque token instead of being
/// stored on the server.
/// </summary>
public interface IStatelessCaptcha
{
    /// <summary>Gets the MIME content type of the image this flow produces.</summary>
    string ContentType { get; }

    /// <summary>Generates an image and a sealed token for a fresh code.</summary>
    StatelessCaptchaResult GenerateCaptcha(int? width = null, int? height = null);

    /// <summary>Validates a submitted answer against a sealed token.</summary>
    bool Validate(string? userInputCaptcha, string? captchaToken);
}

/// <summary>A stateless captcha flow that exposes the options it was registered with.</summary>
public interface IStatelessCaptcha<out TOptions> : IStatelessCaptcha
    where TOptions : class
{
    /// <summary>Gets the options this flow was registered with.</summary>
    TOptions Options { get; }
}

public class StatelessCaptchaResult
{
    public byte[] ImageBytes { get; set; } = [];

    public string Token { get; set; } = string.Empty;
}