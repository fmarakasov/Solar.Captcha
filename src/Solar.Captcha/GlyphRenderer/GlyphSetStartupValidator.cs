using System;
using Microsoft.Extensions.Options;

namespace Solar.Captcha.GlyphRenderer;

/// <summary>
/// Forces the glyph set to be built while the host starts, so that an unresolvable charset
/// prevents the application from starting instead of failing on the first captcha request
/// (ADR-009).
/// </summary>
/// <remarks>
/// This runs as options validation rather than as service construction because the DI container
/// does not run factory delegates when it is built — <c>ValidateOnBuild</c> only checks that the
/// dependency graph is well formed. Registered by <c>AddGlyphSet</c> together with
/// <c>ValidateOnStart()</c>, and therefore throws at <c>IHost.StartAsync()</c>, which is the
/// earliest moment at which the failure can be reported.
/// </remarks>
internal sealed class GlyphSetStartupValidator(GlyphSetFactory factory, GlyphSetOptions options)
    : IValidateOptions<GlyphSetOptions>
{
    private readonly GlyphSetFactory _factory = factory;
    private readonly GlyphSetOptions _options = options;

    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, GlyphSetOptions configuredOptions)
    {
        try
        {
            _ = _factory.Get(_options);
            return ValidateOptionsResult.Success;
        }
        catch (Exception exception)
        {
            return ValidateOptionsResult.Fail(
                $"The glyph set for charset '{_options.Charset}' could not be built: {exception.Message}");
        }
    }
}
