using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Solar.Captcha;

/// <summary>
/// A session-based captcha flow: the generated code is stored in the user's session and validated
/// against a later submission.
/// </summary>
public interface ISessionCaptcha
{
    /// <summary>Gets the MIME content type of the image this flow produces.</summary>
    string ContentType { get; }

    /// <summary>Generates an image for a fresh code and stores that code in the session.</summary>
    byte[] GenerateCaptchaImageBytes(ISession httpSession, int? width = null, int? height = null, string? sessionKeyName = null);

    /// <summary>Generates an image as a <see cref="FileStreamResult"/> for a fresh code.</summary>
    FileStreamResult GenerateCaptchaImageFileStream(ISession httpSession, int? width = null, int? height = null, string? sessionKeyName = null);

    /// <summary>Validates a submitted answer against the code stored in the session.</summary>
    bool Validate(string? userInputCaptcha, ISession httpSession, bool dropSession = true, string? sessionKeyName = null);
}

/// <summary>A session-based captcha flow that exposes the options it was registered with.</summary>
public interface ISessionCaptcha<out TOptions> : ISessionCaptcha
    where TOptions : class
{
    /// <summary>Gets the options this flow was registered with.</summary>
    TOptions Options { get; }
}