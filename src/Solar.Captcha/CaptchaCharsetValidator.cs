using System;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Solar.Captcha.GlyphRenderer;

namespace Solar.Captcha;

/// <summary>
/// Fails start-up when a captcha flow will generate codes from characters the declared glyph set
/// cannot draw, or when no glyph set has been declared at all.
/// </summary>
/// <remarks>
/// A flow's charset is declared where the flow is configured, while the drawing capability is
/// declared at <c>AddGlyphSet</c>. Those two are independent, so this is the check that keeps them
/// consistent — and it runs at host start-up, so a mismatch is a start-up error rather than a
/// request that fails in front of a user.
/// </remarks>
internal sealed class CaptchaCharsetValidator(
    IServiceProvider serviceProvider,
    string letters,
    string captchaOptionsTypeName) : IValidateOptions<GlyphSetOptions>
{
    private readonly IServiceProvider _serviceProvider = serviceProvider;
    private readonly string _letters = letters;
    private readonly string _captchaOptionsTypeName = captchaOptionsTypeName;

    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, GlyphSetOptions configuredOptions)
    {
        if (_serviceProvider.GetService<GlyphSetFactory>() is not { } factory ||
            _serviceProvider.GetService<GlyphSetOptions>() is not { } glyphSetOptions)
        {
            return ValidateOptionsResult.Fail(
                $"{_captchaOptionsTypeName} generates captcha codes from '{_letters}', but no glyph set is " +
                "registered. Call AddGlyphSet(...) to declare the characters the application draws.");
        }

        GlyphSet glyphSet;
        try
        {
            glyphSet = factory.Get(glyphSetOptions);
        }
        catch (Exception)
        {
            // The dedicated glyph set validator reports why the set could not be built; reporting it
            // again here for every flow would only add noise.
            return ValidateOptionsResult.Success;
        }

        var missing = glyphSet.FindMissing(_letters);
        if (missing.Count == 0)
        {
            return ValidateOptionsResult.Success;
        }

        var names = new List<string>(missing.Count);
        foreach (var character in missing)
        {
            names.Add(GlyphSet.Describe(character));
        }

        return ValidateOptionsResult.Fail(
            $"{_captchaOptionsTypeName} generates captcha codes from {missing.Count} character(s) that the " +
            $"glyph set cannot draw: {string.Join(", ", names)}. " +
            $"The glyph set covers '{glyphSet.Charset}'. Either add these characters to the glyph set charset, " +
            "or generate codes from characters it can draw.");
    }
}
