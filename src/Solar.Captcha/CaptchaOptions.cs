using System;

namespace Solar.Captcha;

/// <summary>
/// Options shared by every captcha flow, whatever challenge it draws or how it stores its code.
/// </summary>
public abstract class CaptchaOptionsBase
{
    /// <summary>Gets or sets the codes the flow refuses to issue.</summary>
    public string[] BlockedCodes { get; set; } = [];

    /// <summary>
    /// Gets or sets whether letter answers are compared case-insensitively. Ignored by clock flows,
    /// whose answers are normalized before comparison.
    /// </summary>
    public bool IgnoreCase { get; set; } = true;
}

/// <summary>Options shared by every captcha flow that renders an image.</summary>
public abstract class ImageCaptchaOptions : CaptchaOptionsBase
{
    /// <summary>Gets or sets the image width in pixels.</summary>
    public int Width { get; set; } = 100;

    /// <summary>Gets or sets the image height in pixels.</summary>
    public int Height { get; set; } = 36;
}

/// <summary>Options shared by every session-based captcha flow.</summary>
public abstract class SessionBasedImageCaptchaOptions : ImageCaptchaOptions
{
    /// <summary>Gets or sets the session key the generated code is stored under.</summary>
    public string SessionName { get; set; } = "CaptchaCode";
}

/// <summary>Options shared by every stateless captcha flow.</summary>
public abstract class StatelessImageCaptchaOptions : ImageCaptchaOptions
{
    /// <summary>Gets or sets how long a sealed token remains valid.</summary>
    public TimeSpan TokenExpiration { get; set; } = TimeSpan.FromMinutes(5);
}

/// <summary>Options shared by every shared-key stateless captcha flow.</summary>
public abstract class SharedKeyStatelessImageCaptchaOptions : StatelessImageCaptchaOptions
{
    /// <summary>Gets or sets the Base64-encoded 256-bit key shared across the cluster.</summary>
    public string SharedKey { get; set; } = string.Empty;
}