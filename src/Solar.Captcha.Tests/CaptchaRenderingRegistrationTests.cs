using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NUnit.Framework;
using Solar.Captcha.GlyphRenderer;
using System;
using System.IO;
using System.Threading.Tasks;

namespace Solar.Captcha.Tests;

/// <summary>
/// Covers the wiring contract: an application declares one glyph source and one glyph set, the set
/// is built while the host starts, and a mismatch between what a captcha flow generates and what the
/// application can draw stops the host rather than the first request.
/// </summary>
[TestFixture]
public class CaptchaRenderingRegistrationTests
{
    private const string DrawableCharset = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";

    /// <summary>The font used by the font-source tests; copied to the test output directory.</summary>
    private static string TestFontPath =>
        Path.Combine(TestContext.CurrentContext.TestDirectory, "Fonts", "arial.ttf");

    private static IHost BuildHost(Action<IServiceCollection> configure)
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Logging.ClearProviders();
        configure(builder.Services);
        return builder.Build();
    }

    /// <summary>Starts the host and returns the exception that stopped it, or null.</summary>
    private static async Task<Exception?> StartAndCaptureAsync(IHost host)
    {
        try
        {
            await host.StartAsync();
            return null;
        }
        catch (Exception exception)
        {
            return exception;
        }
    }

    [Test]
    public async Task AddGlyphSet_WithStaticSourceAndCoveringCharset_StartsSuccessfully()
    {
        // Arrange
        using var host = BuildHost(services =>
        {
            services.AddStaticGlyphSource();
            services.AddGlyphSet(options => options.Charset = DrawableCharset);
            services.AddSessionBasedCaptcha(options => options.Letters = "ABC123");
        });

        // Act
        var failure = await StartAndCaptureAsync(host);

        // Assert
        Assert.That(failure, Is.Null, failure?.Message);
    }

    [Test]
    public async Task AddGlyphSet_RegistersARendererThatDrawsThroughDependencyInjection()
    {
        // Arrange
        using var host = BuildHost(services =>
        {
            services.AddStaticGlyphSource();
            services.AddGlyphSet(options => options.Charset = DrawableCharset);
            services.AddSessionBasedCaptcha(options => options.Letters = "ABC123");
        });

        await host.StartAsync();

        // Act
        var renderer = host.Services.GetRequiredService<ICaptchaImageRenderer>();
        var image = renderer.Render(200, 100, "ABC123");

        // Assert
        Assert.That(image, Is.Not.Null);
        Assert.That(image.Length, Is.GreaterThan(0));
        Assert.That(image[0], Is.EqualTo(0x89)); // PNG signature
    }

    [Test]
    public async Task AddGlyphSet_WithUnresolvableCharset_StopsTheHostFromStarting()
    {
        // Arrange - '!' has no authored glyph and no fallback is configured
        using var host = BuildHost(services =>
        {
            services.AddStaticGlyphSource();
            services.AddGlyphSet(options => options.Charset = "ABC!");
        });

        // Act
        var failure = await StartAndCaptureAsync(host);

        // Assert
        Assert.That(failure, Is.Not.Null, "start-up should have failed");
        Assert.That(failure, Is.InstanceOf<OptionsValidationException>());
        Assert.That(failure!.Message, Does.Contain("glyph set"));
        Assert.That(failure.Message, Does.Contain("'!'"));
    }

    [Test]
    public async Task AddGlyphSet_WithFallbackGlyphForUnresolvableCharacter_StartsSuccessfully()
    {
        // Arrange
        using var host = BuildHost(services =>
        {
            services.AddStaticGlyphSource();
            services.AddGlyphSet(options =>
            {
                options.Charset = "ABC!";
                options.FallbackGlyph = new byte[GlyphRenderOptions.GlyphHeight];
            });
        });

        // Act
        var failure = await StartAndCaptureAsync(host);

        // Assert
        Assert.That(failure, Is.Null, failure?.Message);
    }

    [Test]
    public async Task AddSessionBasedCaptcha_WithoutAGlyphSet_StopsTheHostFromStarting()
    {
        // Arrange - a flow but no glyph set: there is no implicit default to fall back on
        using var host = BuildHost(services =>
        {
            services.AddSessionBasedCaptcha(options => options.Letters = "ABC123");
        });

        // Act
        var failure = await StartAndCaptureAsync(host);

        // Assert
        Assert.That(failure, Is.Not.Null, "start-up should have failed");
        Assert.That(failure, Is.InstanceOf<OptionsValidationException>());
        Assert.That(failure!.Message, Does.Contain("no glyph set is registered"));
        Assert.That(failure.Message, Does.Contain(nameof(BasicLetterCaptchaOptions)));
    }

    [Test]
    public async Task AddGlyphSet_WhenAFlowGeneratesCharactersTheSetCannotDraw_StopsTheHostFromStarting()
    {
        // Arrange - the flow adds 'Z', which the declared charset does not cover
        using var host = BuildHost(services =>
        {
            services.AddStaticGlyphSource();
            services.AddGlyphSet(options => options.Charset = "ABC123");
            services.AddSessionBasedCaptcha(options => options.Letters = "ABZ");
        });

        // Act
        var failure = await StartAndCaptureAsync(host);

        // Assert
        Assert.That(failure, Is.Not.Null, "start-up should have failed");
        Assert.That(failure, Is.InstanceOf<OptionsValidationException>());
        Assert.That(failure!.Message, Does.Contain("cannot draw"));
        Assert.That(failure.Message, Does.Contain("'Z'"));
        Assert.That(failure.Message, Does.Contain(nameof(BasicLetterCaptchaOptions)));
    }

    [Test]
    public async Task AddGlyphSet_WithEveryFlowCovered_StartsSuccessfully()
    {
        // Arrange - the sample app shape: the session flow needs a wider charset than the
        // stateless one, and the declared union must cover both.
        using var host = BuildHost(services =>
        {
            services.AddStaticGlyphSource();
            services.AddGlyphSet(options => options.Charset = "2346789ABCDEFGHJKLMNPRTUVWXYZ");
            services.AddSessionBasedCaptcha(options => options.Letters = "2346789ABCDEFGHJKLMNPRTUVWXYZ");
            services.AddStatelessCaptcha(options => options.Letters = "2346789ABCDGHKMNPRUVWXYZ");
        });

        // Act
        var failure = await StartAndCaptureAsync(host);

        // Assert
        Assert.That(failure, Is.Null, failure?.Message);
    }

    [Test]
    public async Task AddGlyphSet_RegisteredBeforeTheGlyphSource_StillBuildsTheSet()
    {
        // Arrange - registration order must not decide whether the set can be built
        using var host = BuildHost(services =>
        {
            services.AddGlyphSet(options => options.Charset = DrawableCharset);
            services.AddStaticGlyphSource();
            services.AddSessionBasedCaptcha(options => options.Letters = "ABC123");
        });

        // Act
        var failure = await StartAndCaptureAsync(host);

        // Assert
        Assert.That(failure, Is.Null, failure?.Message);
    }

    [Test]
    public async Task AddGlyphSet_WithoutAGlyphSource_StopsTheHostFromStarting()
    {
        // Arrange - a declared charset but nothing to draw it with
        using var host = BuildHost(services =>
        {
            services.AddGlyphSet(options => options.Charset = DrawableCharset);
        });

        // Act
        var failure = await StartAndCaptureAsync(host);

        // Assert
        Assert.That(failure, Is.Not.Null, "start-up should have failed");
    }

    [Test]
    public async Task Renderer_IsRegisteredAsASingleton()
    {
        // Arrange
        using var host = BuildHost(services =>
        {
            services.AddStaticGlyphSource();
            services.AddGlyphSet(options => options.Charset = DrawableCharset);
        });

        await host.StartAsync();

        // Act
        var first = host.Services.GetRequiredService<ICaptchaImageRenderer>();
        var second = host.Services.GetRequiredService<ICaptchaImageRenderer>();

        // Assert
        Assert.That(second, Is.SameAs(first));
    }

    #region Registration guards

    [Test]
    public void AddStaticGlyphSource_AfterAFontSource_Throws()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddStaticGlyphSource();

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => services.AddStaticGlyphSource());
    }

    [Test]
    public void AddGlyphSet_WithEmptyCharset_ThrowsAtRegistration()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act & Assert
        Assert.Throws<ArgumentException>(() => services.AddGlyphSet(options => options.Charset = ""));
    }

    [Test]
    public void AddFontGlyphSource_WithMissingFontFile_ThrowsAtRegistration()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            services.AddFontGlyphSource(options => options.FontPath = "definitely-not-a-font.ttf"));
    }

    [Test]
    public void AddFontGlyphSource_WithBothFontPathAndStreamFactory_ThrowsAtRegistration()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act & Assert
        Assert.Throws<ArgumentException>(() => services.AddFontGlyphSource(options =>
        {
            options.FontPath = TestFontPath;
            options.FontStreamFactory = () => File.OpenRead(TestFontPath);
        }));
    }

    [Test]
    public async Task AddFontGlyphSource_WithStreamFactoryAndCoveringCharset_StartsSuccessfully()
    {
        // Arrange
        using var host = BuildHost(services =>
        {
            services.AddFontGlyphSource(options => options.FontStreamFactory = () => File.OpenRead(TestFontPath));
            services.AddGlyphSet(options => options.Charset = "ABC123");
            services.AddSessionBasedCaptcha(options => options.Letters = "ABC123");
        });

        // Act
        var failure = await StartAndCaptureAsync(host);

        // Assert
        Assert.That(failure, Is.Null, failure?.Message);
    }

    [Test]
    public async Task AddFontGlyphSource_WithStreamFactoryReturningGarbage_StopsTheHostFromStarting()
    {
        // Arrange: the stream source must fail closed at start-up, exactly like a missing font file.
        using var host = BuildHost(services =>
        {
            services.AddFontGlyphSource(options => options.FontStreamFactory = () => new MemoryStream([0x00, 0x01]));
            services.AddGlyphSet(options => options.Charset = "ABC123");
        });

        // Act
        var failure = await StartAndCaptureAsync(host);

        // Assert
        Assert.That(failure, Is.Not.Null, "a font that cannot be parsed must stop the host");
    }

    #endregion
}
