# Solar.Captcha

Generate and validate Captcha images in ASP.NET Core. Based on [Edi.Captcha.AspNetCore](https://github.com/EdiWang/Edi.Captcha.AspNetCore) by Edi Wang.

[![.NET](https://github.com/fmarakasov/Solar.Captcha/actions/workflows/dotnet.yml/badge.svg)](https://github.com/fmarakasov/Solar.Captcha/actions/workflows/dotnet.yml)

[![NuGet][main-nuget-badge]][main-nuget]

[main-nuget]: https://www.nuget.org/packages/Solar.Captcha/
[main-nuget-badge]: https://img.shields.io/nuget/v/Solar.Captcha.svg?style=flat-square&label=nuget

## Key Differences from Edi.Captcha.AspNetCore

`Solar.Captcha` exists because the rendering model in `Edi.Captcha.AspNetCore` changed after the SixLabors dependency was removed. That change introduced an internal graphics core and dropped user-provided font support, effectively limiting rendering to primitive Latin glyphs.

`Solar.Captcha` brings font-based rendering back without introducing new image dependencies, so you can use custom glyphs (including non-Latin scripts) from your own TrueType/OpenType font assets.

- **Dynamic Glyph Creation from Font File or Stream**: Register a glyph source from `FontPath` or `FontStreamFactory`, then build a `GlyphSet` for your configured charset. Glyphs are generated from the provided TTF/OTF data instead of being limited to fixed primitive bitmaps.
- **Standalone 2D Graphics and Animated GIF Engine (`Solar.Captcha.Raster`)**: Includes a dependency-free 2D canvas, TrueType font measurement and rasterization at arbitrary scales, streaming GIF89a / LZW encoder, and specialized challenge renderers such as animated analog clock faces.
- **DI-First Architecture (No Static Classes)**: Rendering and captcha flows are fully DI-driven (`ICaptchaImageRenderer`, `SessionBasedCaptcha`, `StatelessCaptcha`, shared-key flow). No static entry points are required.
- **Injectable Random for Deterministic Tests**: The renderer accepts an injected `System.Random` (default: `Random.Shared`) so tests can use a deterministic random source while production keeps thread-safe shared randomness.
- **Nullable Enabled**: The codebase is maintained with nullable reference types enabled to reduce null-related runtime defects and improve API correctness.
- **.NET 11 Preview Targeting and Stabilization Plan**: Current preview packages target `.NET 11` (along with `.NET 10`). The first stable release is planned after the official `.NET 11` GA release date.

---

## Install

NuGet Package Manager
```
Install-Package Solar.Captcha
```

or .NET CLI

```
dotnet add package Solar.Captcha
```

## Glyph Rendering Setup (required)

Every captcha flow below draws through a shared image renderer, and that renderer has to be told what it's allowed to draw. Whichever scenario fits your app, you register **one** glyph source and **one** glyph set before registering any captcha flow.

The glyph set is built once, while the host starts. If a character can't be resolved — a typo in the font path, a charset the font doesn't cover — the application **fails to start** with a clear error, instead of failing on the first captcha request. There is no default source and no default charset.

In all three scenarios, `AddGlyphSet` declares the characters your flows will generate. It must cover the `Letters` of every `AddSessionBasedCaptcha` / `AddStatelessCaptcha` / `AddSharedKeyStatelessCaptcha` call:

```csharp
services.AddGlyphSet(options => options.Charset = "2346789ABCDEFGHJKLMNPRTUVWXYZ");
```

`Charset` is compared case-insensitively and normalised to upper case. Optionally set `GlyphSetOptions.FallbackGlyph` to substitute a glyph for characters the source can't resolve; when it's `null` (the default), an unresolvable character fails start-up.

### Scenario 1: Static Glyphs (No External Assets)

Use the hand-authored bitmaps embedded in the package. This covers digits `0-9` and the Latin alphabet `A-Z` (case-insensitive) — no font file needed.

```csharp
services.AddStaticGlyphSource();
services.AddGlyphSet(options => options.Charset = "2346789ABCDEFGHJKLMNPRTUVWXYZ");
```

This is the right choice for plain Latin captchas and for keeping deployment free of font assets. It cannot draw anything outside its authored set — non-Latin scripts or punctuation the package doesn't ship a glyph for will fail start-up.

### Scenario 2: Glyphs from a Font File

Point the renderer at a TrueType/OpenType font file on disk. Use this when you need characters the static glyphs don't cover, such as non-Latin scripts.

```csharp
services.AddFontGlyphSource(options => options.FontPath = "Fonts/arial.ttf");
services.AddGlyphSet(options => options.Charset = "2346789ABCDEFGHJKLMNPRTUVWXYZ");
```

Because the path is configuration-friendly, this is the scenario to choose when the font location comes from `appsettings.json` or an environment variable. `FontPath` must point at an existing file; it is validated at registration and again when the glyph set is built, so a bad path stops start-up.

### Scenario 3: Glyphs from a Stream

Use a `Func<Stream>` factory for fonts that aren't a file on disk: embedded resources, `byte[]` in memory, or a remote stream downloaded at start-up. This is code-only, since a delegate can't be expressed in configuration.

```csharp
services.AddFontGlyphSource(options => options.FontStreamFactory = OpenFontStream);
services.AddGlyphSet(options => options.Charset = "2346789ABCDEFGHJKLMNPRTUVWXYZ");

static Stream OpenFontStream()
{
    // Fresh, readable stream each call — rewindable and not already disposed.
    return typeof(Program).Assembly.GetManifestResourceStream("MyApp.Fonts.arial.ttf")
        ?? throw new InvalidOperationException("Font resource not found.");
}
```

The factory is invoked once, when the renderer is constructed. The library reads the stream to the end and disposes it, so the factory must return a **fresh, readable** stream on every call (don't return a cached, already-disposed, or non-seekable-at-position-0 instance).

### Font Source Rules

`AddFontGlyphSource` (Scenarios 2 and 3) reads the font from **either** `FontPath` **or** `FontStreamFactory`:

- Set exactly one of the two. Setting **both**, or **neither**, throws `ArgumentException` at registration.
- `AddStaticGlyphSource` and `AddFontGlyphSource` are mutually exclusive — only **one** glyph source may be registered. Registering a second throws `InvalidOperationException`.

## Session-Based Captcha (Traditional Approach)

### 1. Register in DI

```csharp
services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(20);
    options.Cookie.HttpOnly = true;
});

services.AddSessionBasedCaptcha();
```

```csharp
// Don't forget to add this line in your `Configure` method.
app.UseSession();
```

or you can customize the options

```csharp
services.AddSessionBasedCaptcha(option =>
{
    option.Letters = "2346789ABCDEFGHJKLMNPRTUVWXYZ";
    option.SessionName = "CaptchaCode";
    option.CodeLength = 4;
});
```

### 2. Generate Image

#### Using MVC Controller

```csharp
private readonly ISessionBasedCaptcha _captcha;

public SomeController(ISessionBasedCaptcha captcha)
{
    _captcha = captcha;
}

[Route("get-captcha-image")]
public IActionResult GetCaptchaImage()
{
    var s = _captcha.GenerateCaptchaImageFileStream(
        HttpContext.Session,
        100,
        36
    );
    return s;
}
```

#### Using Middleware

```csharp
app.UseSession().UseSessionCaptcha(options =>
{
    options.RequestPath = "/captcha-image";
    options.ImageHeight = 36;
    options.ImageWidth = 100;
});
```

### 3. Add CaptchaCode Property to Model

```csharp
[Required]
[StringLength(4)]
public string CaptchaCode { get; set; }
```

### 5. View

```html
<div class="col">
    <div class="input-group">
        <div class="input-group-prepend">
            <img id="img-captcha" src="~/captcha-image" />
        </div>
        <input type="text" 
               asp-for="CommentPostModel.CaptchaCode" 
               class="form-control" 
               placeholder="Captcha Code" 
               autocomplete="off" 
               minlength="4"
               maxlength="4" />
    </div>
    <span asp-validation-for="CommentPostModel.CaptchaCode" class="text-danger"></span>
</div>
```

### 6. Validate Input

```csharp
_captcha.ValidateCaptchaCode(model.CommentPostModel.CaptchaCode, HttpContext.Session)
```

To make your code look more cool, you can also write an Action Filter like this:

```csharp
public class ValidateCaptcha : ActionFilterAttribute
{
    private readonly ISessionBasedCaptcha _captcha;

    public ValidateCaptcha(ISessionBasedCaptcha captcha)
    {
        _captcha = captcha;
    }

    public override void OnActionExecuting(ActionExecutingContext context)
    {
        var captchaedModel =
            context.ActionArguments.Where(p => p.Value is ICaptchable)
                                   .Select(x => x.Value as ICaptchable)
                                   .FirstOrDefault();

        if (null == captchaedModel)
        {
            context.ModelState.AddModelError(nameof(captchaedModel.CaptchaCode), "Captcha Code is required");
            context.Result = new BadRequestObjectResult(context.ModelState);
        }
        else
        {
            if (!_captcha.Validate(captchaedModel.CaptchaCode, context.HttpContext.Session))
            {
                context.ModelState.AddModelError(nameof(captchaedModel.CaptchaCode), "Wrong Captcha Code");
                context.Result = new ConflictObjectResult(context.ModelState);
            }
            else
            {
                base.OnActionExecuting(context);
            }
        }
    }
}
```

and then

```csharp
services.AddScoped<ValidateCaptcha>();
```

and then

```csharp

public class YourModelWithCaptchaCode : ICaptchable
{
    public string YourProperty { get; set; }

    [Required]
    [StringLength(4)]
    public string CaptchaCode { get; set; }
}

[ServiceFilter(typeof(ValidateCaptcha))]
public async Task<IActionResult> SomeAction(YourModelWithCaptchaCode model)
{
    // ....
}
```

## Stateless Captcha (Recommended for Scalable Applications)

**Advantages of Stateless Captcha:**
- ✅ Works in clustered/load-balanced environments
- ✅ No server-side session storage required
- ✅ Built-in expiration through encryption
- ✅ Secure token-based validation
- ✅ Better scalability
- ✅ Single API call for both token and image

### 1. Register in DI

```csharp
services.AddStatelessCaptcha();
```

or with custom options:

```csharp
services.AddStatelessCaptcha(options =>
{
    options.Letters = "2346789ABCDGHKMNPRUVWXYZ";
    options.CodeLength = 4;
    options.TokenExpiration = TimeSpan.FromMinutes(5);
});
```

### 2. Create Model with Token Support

```csharp
public class StatelessHomeModel
{
    [Required]
    [StringLength(4)]
    public string CaptchaCode { get; set; }
    
    public string CaptchaToken { get; set; }
}
```

### 3. Example Controller and View

See: [src\Solar.Captcha.SampleApp\Controllers\StatelessController.cs](src/Solar.Captcha.SampleApp/Controllers/StatelessController.cs) and [src\Solar.Captcha.SampleApp\Views\Stateless\Index.cshtml](src/Solar.Captcha.SampleApp/Views/Stateless/Index.cshtml) for a complete example.

### Cluster/Load Balancer Configuration

⚠️ **Important for Production Deployments**: The stateless captcha uses ASP.NET Core's Data Protection API for token encryption. In clustered environments or behind load balancers, you **must** configure shared data protection keys to ensure captcha tokens can be validated on any server.

#### Option 1: File System (Network Share)
```csharp
public void ConfigureServices(IServiceCollection services)
{
    services.AddDataProtection()
        .PersistKeysToFileSystem(new DirectoryInfo(@"\\shared-network-path\keys"))
        .SetApplicationName("YourAppName"); // Must be consistent across all instances
    
    services.AddStatelessCaptcha(options =>
    {
        // Your captcha configuration
    });
}
```

#### Option 2: Azure Blob Storage
```csharp
public void ConfigureServices(IServiceCollection services)
{
    services.AddDataProtection()
        .PersistKeysToAzureBlobStorage("DefaultEndpointsProtocol=https;AccountName=...", "keys-container", "dataprotection-keys.xml")
        .SetApplicationName("YourAppName");
    
    services.AddStatelessCaptcha(options =>
    {
        // Your captcha configuration
    });
}
```

#### Option 3: Redis
```csharp
public void ConfigureServices(IServiceCollection services)
{
    services.AddDataProtection()
        .PersistKeysToStackExchangeRedis(ConnectionMultiplexer.Connect("your-redis-connection"), "DataProtection-Keys")
        .SetApplicationName("YourAppName");
    
    services.AddStatelessCaptcha(options =>
    {
        // Your captcha configuration
    });
}
```

#### Option 4: SQL Server
```csharp
public void ConfigureServices(IServiceCollection services)
{
    services.AddDataProtection()
        .PersistKeysToDbContext<YourDbContext>()
        .SetApplicationName("YourAppName");
    
    services.AddStatelessCaptcha(options =>
    {
        // Your captcha configuration
    });
}
```

#### Single Server Deployment

For single server deployments, no additional configuration is required. The default Data Protection configuration will work correctly.

#### Testing Cluster Configuration

To verify your cluster configuration is working:

1. Generate a captcha on Server A
2. Submit the form to Server B (or any other server)
3. Validation should succeed

If validation fails with properly entered captcha codes, check your Data Protection configuration.

## Shared Key Stateless Captcha (Recommended for Scalable Applications without DPAPI)

**When to use Shared Key Stateless Captcha:**
- ✅ Full control over encryption keys
- ✅ Works without ASP.NET Core Data Protection API
- ✅ Simpler cluster configuration
- ✅ Custom key rotation strategies
- ✅ Works across different application frameworks
- ✅ No dependency on external storage for keys

### 1. Register in DI with Shared Key

```csharp
services.AddSharedKeyStatelessCaptcha(options =>
{
    options.SharedKey = "your-32-byte-base64-encoded-key"; // Generate securely
    options.FontStyle = FontStyle.Bold;
    options.DrawLines = true;
    options.TokenExpiration = TimeSpan.FromMinutes(5);
});
```

### 2. Generate Secure Shared Key

**Important**: Use a cryptographically secure random key. Here's how to generate one:

```csharp
// Generate a secure 256-bit key (one-time setup)
using (var rng = RandomNumberGenerator.Create())
{
    var keyBytes = new byte[32]; // 256 bits
    rng.GetBytes(keyBytes);
    var base64Key = Convert.ToBase64String(keyBytes);
    Console.WriteLine($"Shared Key: {base64Key}");
}
```

### 3. Configuration Options

#### Configuration File (appsettings.json)
```json
{
  "CaptchaSettings": {
    "SharedKey": "your-generated-base64-key-here",
    "TokenExpirationMinutes": 5
  }
}
```

```csharp
public void ConfigureServices(IServiceCollection services)
{
    var captchaKey = Configuration["CaptchaSettings:SharedKey"];
    var expirationMinutes = Configuration.GetValue<int>("CaptchaSettings:TokenExpirationMinutes", 5);
    
    services.AddSharedKeyStatelessCaptcha(options =>
    {
        options.SharedKey = captchaKey;
        options.TokenExpiration = TimeSpan.FromMinutes(expirationMinutes);
        // Other options...
    });
}
```

### 4. Example Controller and View

See: [src\Solar.Captcha.SampleApp\Controllers\SharedKeyStatelessController.cs](src/Solar.Captcha.SampleApp/Controllers/SharedKeyStatelessController.cs) and [src\Solar.Captcha.SampleApp\Views\SharedKeyStateless\Index.cshtml](src/Solar.Captcha.SampleApp/Views/SharedKeyStateless/Index.cshtml) for a complete example.

---

## 2D Raster Graphics & Animated GIF Rendering (`Solar.Captcha.Raster`)

`Solar.Captcha.Raster` provides a lightweight, allocation-conscious, zero-dependency 2D graphics engine. It replaces heavy external image libraries for applications that require vector outline rendering, linear gradient backgrounds, geometry stamping, and animated GIF output.

### 1. `RasterCanvas` and 2D Primitives

`RasterCanvas` is an in-memory 32-bit RGBA drawing surface.

```csharp
using Solar.Captcha.Raster;

var canvas = new RasterCanvas(240, 240);

// Fill with a two-point linear gradient
var gradient = new LinearGradientBrush(
    new Point(0, 0),
    new Point(240, 240),
    RasterColor.FromHex("#1E1E2F"),
    RasterColor.FromHex("#0F0F17")
);
canvas.FillLinearGradient(gradient);

// Draw stroked lines and shapes with round caps and joins
var pen = new RasterPen(RasterColor.White, width: 3);
canvas.DrawLine(new PointF(20, 20), new PointF(220, 220), pen);
canvas.FillCircle(new Point(120, 120), radius: 5, RasterColor.FromHex("#FFCC00"));

// Encode to PNG bytes
byte[] pngBytes = PngEncoder.Encode(canvas);
```

### 2. Arbitrary-Scale Font Typography (`RasterFont` and `RasterFontResolver`)

Load TrueType/OpenType fonts at any point size to measure and draw text:

```csharp
// Discover a system or local font by family name
string fontPath = RasterFontResolver.ResolveFontPath("Arial");

// Load font at 14px size
var font = RasterFont.FromFile(fontPath, sizePx: 14);

// Measure advance and line metrics
int textWidth = font.MeasureText("12");

// Render text onto a canvas
canvas.DrawText("12", x: 110, baselineY: 35, font, RasterColor.White);
```

### 3. Animated GIF Rendering (`GifEncoder` and `ClockGifRenderer`)

#### Streaming GIF89a Encoding

The `GifEncoder` streams frames directly to an output stream without buffering full-resolution animations in memory:

```csharp
using var outputStream = new MemoryStream();
using var encoder = new GifEncoder(outputStream, width: 240, height: 240);

// Palette is constructed from first frame and optional seeded colors
encoder.Begin(firstFrameCanvas, [RasterColor.White, RasterColor.Red]);

for (int frame = 0; frame < 60; frame++)
{
    // Draw next frame on canvas...
    encoder.WriteFrame(currentFrameCanvas, delayHundredthsOfSecond: 100);
}

encoder.End();
```

#### Ready-to-Use Animated Clock CAPTCHA (`ClockGifRenderer`)

Render a complete 60-second animated clock face with hour/minute hands, rotating second hand, circular dial markings, and numeral layout:

```csharp
var options = new ClockRenderOptions
{
    Hour = 10,
    Minute = 15,
    FontPath = RasterFontResolver.ResolveFontPath("Arial"),
    BackgroundColor = RasterColor.FromHex("#1A1A1A"),
    DialColor = RasterColor.White,
    SecondHandColor = RasterColor.FromHex("#FF3B30")
};

byte[] gifBytes = ClockGifRenderer.Render(options);
```