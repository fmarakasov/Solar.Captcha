using System;
using System.IO;
using NUnit.Framework;
using Solar.Captcha.Raster;

namespace Solar.Captcha.Tests.Raster;

[TestFixture]
public class RasterFontResolverTests
{
    private string _testFontDir = null!;

    [SetUp]
    public void SetUp()
    {
        _testFontDir = Path.Combine(TestContext.CurrentContext.TestDirectory, "Fonts");
        Assert.That(Directory.Exists(_testFontDir), Is.True, $"Fonts dir not found at '{_testFontDir}'.");
    }

    [Test]
    public void TryResolveFamilyName_InKnownDirectory_FindsArial()
    {
        var ok = RasterFontResolver.TryResolveFamilyName("Arial", [_testFontDir], out var path);

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.True);
            Assert.That(File.Exists(path), Is.True);
            Assert.That(Path.GetFileName(path), Is.EqualTo("arial.ttf").IgnoreCase);
        });
    }

    [Test]
    public void TryResolveFamilyName_WithMissingFamily_ReturnsFalse()
    {
        var ok = RasterFontResolver.TryResolveFamilyName("NonExistentFamily12345", [_testFontDir], out var path);

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.False);
            Assert.That(path, Is.Empty);
        });
    }

    [Test]
    public void Resolve_WithExplicitExistingPath_ReturnsThatPathDirectly()
    {
        var expected = Path.Combine(_testFontDir, "arial.ttf");

        var resolved = RasterFontResolver.Resolve(expected, "IgnoredFamily");

        Assert.That(resolved, Is.EqualTo(expected));
    }

    [Test]
    public void Resolve_WithExplicitMissingPath_ThrowsFileNotFoundException()
    {
        Assert.Throws<FileNotFoundException>(
            () => RasterFontResolver.Resolve("c:\\nonexistent\\missing.ttf", "Arial"));
    }
}
