using System;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Solar.Captcha.GlyphRenderer;

namespace Solar.Captcha;

/// <summary>
/// Registration for the captcha flows and for the glyph rendering they depend on.
/// </summary>
/// <remarks>
/// Rendering is explicit and has no default: register one glyph source
/// (<see cref="AddFontGlyphSource"/> or <see cref="AddStaticGlyphSource"/>), then declare the
/// characters the application will draw with <see cref="AddGlyphSet"/>. The glyph set is built
/// while the host starts, and an application that cannot build it refuses to start rather than
/// failing on the first captcha request (ADR-008, ADR-009).
/// </remarks>
public static class CaptchaServiceCollectionExtensions
{
    /// <summary>
    /// The characters the letter-based captcha flows draw by default, chosen to avoid visually
    /// ambiguous glyphs.
    /// </summary>
    public const string DefaultLetters = "2346789ABCDGHKMNPRUVWXYZ";

    /// <summary>
    /// Registers the session-based captcha flow.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="options">Optional configuration for the flow.</param>
    public static void AddSessionBasedCaptcha(this IServiceCollection services, Action<BasicLetterCaptchaOptions> options = null)
    {
        var option = new BasicLetterCaptchaOptions
        {
            Letters = DefaultLetters,
            SessionName = "CaptchaCode",
            CodeLength = 4
        };

        options?.Invoke(option);

        services.AddSingleton(option);
        services.AddCaptchaCharsetRequirement(option.Letters, nameof(BasicLetterCaptchaOptions));
        services.AddTransient<ISessionBasedCaptcha, BasicLetterCaptcha>();
    }

    /// <summary>
    /// Registers the stateless captcha flow, which protects its token with Data Protection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="options">Optional configuration for the flow.</param>
    public static void AddStatelessCaptcha(this IServiceCollection services, Action<StatelessLetterCaptchaOptions> options = null)
    {
        services.AddDataProtection();

        var option = new StatelessLetterCaptchaOptions
        {
            Letters = DefaultLetters,
            CodeLength = 4,
            TokenExpiration = TimeSpan.FromMinutes(5)
        };

        options?.Invoke(option);

        services.AddSingleton(option);
        services.AddCaptchaCharsetRequirement(option.Letters, nameof(StatelessLetterCaptchaOptions));
        services.AddTransient<IStatelessCaptcha, StatelessLetterCaptcha>();
    }

    /// <summary>
    /// Registers the stateless captcha flow that protects its token with a shared key.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="options">Optional configuration for the flow.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddSharedKeyStatelessCaptcha(this IServiceCollection services, Action<SharedKeyStatelessLetterCaptchaOptions> options = null)
    {
        var option = new SharedKeyStatelessLetterCaptchaOptions
        {
            Letters = DefaultLetters,
            CodeLength = 4,
            TokenExpiration = TimeSpan.FromMinutes(5)
        };

        options?.Invoke(option);

        services.AddSingleton(option);
        services.AddCaptchaCharsetRequirement(option.Letters, nameof(SharedKeyStatelessLetterCaptchaOptions));
        services.AddTransient<IStatelessCaptcha, SharedKeyStatelessLetterCaptcha>();

        return services;
    }

    /// <summary>
    /// Registers the font-based glyph source. The font is loaded when the glyph set is built, so an
    /// unusable font stops start-up rather than the first captcha request.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configureOptions">Configures the font path.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <exception cref="ArgumentException">Thrown when the options are invalid.</exception>
    /// <exception cref="InvalidOperationException">Thrown when a glyph source is already registered.</exception>
    public static IServiceCollection AddFontGlyphSource(
        this IServiceCollection services,
        Action<GlyphRenderOptions> configureOptions = null)
    {
        ThrowIfGlyphSourceRegistered(services);

        var options = new GlyphRenderOptions();
        configureOptions?.Invoke(options);
        options.Validate();

        services.AddSingleton(options);
        services.AddSingleton<IGlyphRenderer>(_ => new Solar.Captcha.GlyphRenderer.GlyphRenderer(options));

        return services;
    }

    /// <summary>
    /// Registers the static glyph source: the hand-authored glyphs embedded in the package.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <exception cref="InvalidOperationException">Thrown when a glyph source is already registered.</exception>
    public static IServiceCollection AddStaticGlyphSource(this IServiceCollection services)
    {
        ThrowIfGlyphSourceRegistered(services);

        services.AddSingleton<IGlyphRenderer, StaticGlyphRenderer>();

        return services;
    }

    /// <summary>
    /// Declares the characters the application must be able to draw, and registers the captcha image
    /// renderer that draws them.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configureOptions">Declares the charset and an optional fallback glyph.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <exception cref="ArgumentException">Thrown when the options are invalid.</exception>
    public static IServiceCollection AddGlyphSet(
        this IServiceCollection services,
        Action<GlyphSetOptions> configureOptions = null)
    {
        var options = new GlyphSetOptions();
        configureOptions?.Invoke(options);
        options.Validate();

        // One instance is shared by the build and by everything that validates against it, so the
        // declared charset cannot drift between the two.
        services.AddSingleton(options);
        services.AddSingleton<GlyphSetFactory>();
        services.AddSingleton(sp => sp.GetRequiredService<GlyphSetFactory>().Get(sp.GetRequiredService<GlyphSetOptions>()));

        // Mirror the declared options into the options system, so a consumer resolving
        // IOptions<GlyphSetOptions> sees the real values rather than an empty instance.
        services.AddOptions<GlyphSetOptions>()
            .Configure(o =>
            {
                o.Charset = options.Charset;
                o.FallbackGlyph = options.FallbackGlyph;
            });

        services.EnsureGlyphSetStartupValidation();
        services.AddSingleton<IValidateOptions<GlyphSetOptions>, GlyphSetStartupValidator>();

        services.AddSingleton(_ => Random.Shared);
        services.AddSingleton<ICaptchaImageRenderer, CaptchaImageRenderer>();

        return services;
    }

    /// <summary>
    /// Records the characters a captcha flow will draw, so that start-up fails if the declared glyph
    /// set cannot draw them.
    /// </summary>
    private static void AddCaptchaCharsetRequirement(this IServiceCollection services, string letters, string optionsTypeName)
    {
        // Registered even when AddGlyphSet is missing, because that case must fail too.
        services.EnsureGlyphSetStartupValidation();
        services.AddSingleton<IValidateOptions<GlyphSetOptions>>(
            sp => new CaptchaCharsetValidator(sp, letters, optionsTypeName));
    }

    /// <summary>
    /// Enables start-up validation of the glyph set options exactly once, however many registrations
    /// ask for it.
    /// </summary>
    private static void EnsureGlyphSetStartupValidation(this IServiceCollection services)
    {
        if (services.Any(descriptor => descriptor.ServiceType == typeof(GlyphSetValidationRegistration)))
        {
            return;
        }

        services.AddSingleton(new GlyphSetValidationRegistration());
        services.AddOptions<GlyphSetOptions>().ValidateOnStart();
    }

    private static void ThrowIfGlyphSourceRegistered(IServiceCollection services)
    {
        if (services.Any(descriptor => descriptor.ServiceType == typeof(IGlyphRenderer)))
        {
            throw new InvalidOperationException(
                "A glyph source is already registered. Two sources would make the glyph set depend on " +
                "registration order; register either AddFontGlyphSource or AddStaticGlyphSource.");
        }
    }

    /// <summary>Marker type used to enable start-up validation only once.</summary>
    private sealed class GlyphSetValidationRegistration;
}
