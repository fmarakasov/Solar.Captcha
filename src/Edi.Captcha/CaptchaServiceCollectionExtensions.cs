using Microsoft.Extensions.DependencyInjection;
using System;
using Solar.Captcha.GlyphRenderer;

namespace Edi.Captcha;

public static class CaptchaServiceCollectionExtensions
{
    public static void AddSessionBasedCaptcha(this IServiceCollection services, Action<BasicLetterCaptchaOptions> options = null)
    {
        var option = new BasicLetterCaptchaOptions
        {
            Letters = "2346789ABCDGHKMNPRUVWXYZ",
            SessionName = "CaptchaCode",
            CodeLength = 4
        };

        options?.Invoke(option);

        services.AddTransient<ISessionBasedCaptcha>(sb => new BasicLetterCaptcha(option));
    }

    public static void AddStatelessCaptcha(this IServiceCollection services, Action<StatelessLetterCaptchaOptions> options = null)
    {
        services.AddDataProtection();

        var option = new StatelessLetterCaptchaOptions
        {
            Letters = "2346789ABCDGHKMNPRUVWXYZ",
            CodeLength = 4,
            TokenExpiration = TimeSpan.FromMinutes(5)
        };

        options?.Invoke(option);

        services.AddSingleton(option);
        services.AddTransient<IStatelessCaptcha, StatelessLetterCaptcha>();
    }

    public static IServiceCollection AddSharedKeyStatelessCaptcha(this IServiceCollection services, Action<SharedKeyStatelessLetterCaptchaOptions> options = null)
    {
        var option = new SharedKeyStatelessLetterCaptchaOptions
        {
            Letters = "2346789ABCDGHKMNPRUVWXYZ",
            CodeLength = 4,
            TokenExpiration = TimeSpan.FromMinutes(5)
        };

        options?.Invoke(option);

        services.AddSingleton(option);
        services.AddTransient<IStatelessCaptcha, SharedKeyStatelessLetterCaptcha>();

        return services;
    }

    /// <summary>
    /// Registers the font-based glyph renderer with the provided options.
    /// The options will be validated at registration time (fail fast).
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configureOptions">Optional action to configure the renderer options (font path, glyph size).</param>
    /// <returns>The service collection for chaining.</returns>
    /// <exception cref="ArgumentException">Thrown when options are invalid (font file not found, etc.).</exception>
    public static IServiceCollection AddGlyphRenderer(
        this IServiceCollection services,
        Action<GlyphRenderOptions>? configureOptions = null)
    {
        var options = new GlyphRenderOptions();
        configureOptions?.Invoke(options);
        options.Validate();

        services.AddSingleton(options);
        services.AddSingleton<IGlyphRenderer>(provider =>
            new GlyphRenderer(options));

        return services;
    }
}
