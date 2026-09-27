using Microsoft.AspNetCore.DataProtection;
using Moq;
using NUnit.Framework;
using Solar.Captcha.Raster;
using System;
using System.IO;

namespace Solar.Captcha.Tests.Raster;

[TestFixture]
public class ClockCaptchaTests
{
    private Mock<IDataProtectionProvider> _mockDataProtectionProvider = null!;
    private Mock<IDataProtector> _mockDataProtector = null!;
    private Mock<IClockCaptchaImageRenderer> _mockClockRenderer = null!;
    private StatelessClockCaptchaOptions _statelessOptions = null!;
    private SessionBasedClockCaptchaOptions _sessionOptions = null!;

    private string _testFontPath = null!;

    [SetUp]
    public void SetUp()
    {
        _testFontPath = Path.Combine(TestContext.CurrentContext.TestDirectory, "Fonts", "arial.ttf");

        _mockDataProtectionProvider = new Mock<IDataProtectionProvider>();
        _mockDataProtector = new Mock<IDataProtector>();

        _mockDataProtectionProvider
            .Setup(x => x.CreateProtector("Solar.Captcha.Stateless"))
            .Returns(_mockDataProtector.Object);

        _mockClockRenderer = new Mock<IClockCaptchaImageRenderer>();

        _statelessOptions = new StatelessClockCaptchaOptions();
        _statelessOptions.Clock.FontPath = _testFontPath;

        _sessionOptions = new SessionBasedClockCaptchaOptions();
        _sessionOptions.Clock.FontPath = _testFontPath;
    }

    #region Code Formatting & Invariants

    [Test]
    public void ClockCaptchaCode_Format_ReturnsCorrectCanonicalString()
    {
        var code = ClockCaptchaCode.Format(3, 45);
        Assert.That(code, Is.EqualTo("3:45"));
    }

    [Test]
    public void ClockCaptchaCode_Parse_ReturnsCorrectHoursAndMinutes()
    {
        var (hours, minutes) = ClockCaptchaCode.Parse("10:05");
        Assert.Multiple(() =>
        {
            Assert.That(hours, Is.EqualTo(10));
            Assert.That(minutes, Is.EqualTo(5));
        });
    }

    [Test]
    public void ClockCaptchaCodeGenerator_Generate_ProducesCanonicalCodesWithinStepAndRange()
    {
        for (int i = 0; i < 100; i++)
        {
            var code = ClockCaptchaCodeGenerator.Generate(minuteStep: 5);
            Assert.That(code, Is.Not.Null);

            var (hours, minutes) = ClockCaptchaCode.Parse(code);
            Assert.Multiple(() =>
            {
                Assert.That(hours, Is.GreaterThanOrEqualTo(1));
                Assert.That(hours, Is.LessThanOrEqualTo(12));
                Assert.That(minutes, Is.GreaterThanOrEqualTo(0));
                Assert.That(minutes, Is.LessThanOrEqualTo(59));
                Assert.That(minutes % 5, Is.EqualTo(0));
            });
        }
    }

    #endregion

    #region Normalization & Validation Rules

    [TestCase("3:45", true, 3, 45)]
    [TestCase(" 3:45  ", true, 3, 45)]
    [TestCase("03:45", true, 3, 45)]
    [TestCase("03.45", true, 3, 45)]
    [TestCase("3.45", true, 3, 45)]
    [TestCase("3-45", true, 3, 45)]
    [TestCase("3 45", true, 3, 45)]
    [TestCase("345", true, 3, 45)]
    [TestCase("1015", true, 10, 15)]
    [TestCase("1200", true, 12, 00)]
    [TestCase("1345", false, 0, 0)]
    [TestCase("3:65", false, 0, 0)]
    [TestCase("invalid", false, 0, 0)]
    [TestCase("", false, 0, 0)]
    [TestCase(null, false, 0, 0)]
    public void ClockCaptchaCode_TryNormalize_HandlesVariousSpellings(string? input, bool expectedSuccess, int expectedHours, int expectedMinutes)
    {
        var success = ClockCaptchaCode.TryNormalize(input, out var hours, out var minutes);

        Assert.That(success, Is.EqualTo(expectedSuccess));
        if (expectedSuccess)
        {
            Assert.Multiple(() =>
            {
                Assert.That(hours, Is.EqualTo(expectedHours));
                Assert.That(minutes, Is.EqualTo(expectedMinutes));
            });
        }
    }

    [TestCase("3:45", "3:45", true)]
    [TestCase("03.45", "3:45", true)]
    [TestCase("345", "3:45", true)]
    [TestCase("3 45", "3:45", true)]
    [TestCase("3:40", "3:45", false)]
    public void ClockCaptchaCode_Matches_ValidatesNormalizations(string? userInput, string expectedCode, bool expectedMatch)
    {
        var matches = ClockCaptchaCode.Matches(userInput, expectedCode);
        Assert.That(matches, Is.EqualTo(expectedMatch));
    }

    #endregion

    #region Options Validation & Startup Fail Fast

    [Test]
    public void ClockCaptchaOptions_WithNegativeGeometry_ThrowsOutOfRange()
    {
        var options = new ClockCaptchaOptions
        {
            FontPath = _testFontPath,
            ClockRadius = -10
        };

        Assert.Throws<ArgumentOutOfRangeException>(() => options.Validate());
    }

    [Test]
    public void ClockCaptchaOptions_WithUnresolvableFont_ThrowsInvalidOperation()
    {
        var options = new ClockCaptchaOptions
        {
            FontFamily = "DefinetlyNonExistingFontFamily12345",
            FontPath = null
        };

        Assert.Throws<InvalidOperationException>(() => options.Validate());
    }

    [Test]
    public void ClockCaptchaOptions_WithInvalidMinuteStep_ThrowsOutOfRange()
    {
        var options = new ClockCaptchaOptions
        {
            FontPath = _testFontPath,
            MinuteStep = 7 // Does not divide 60
        };

        Assert.Throws<ArgumentOutOfRangeException>(() => options.Validate());
    }

    #endregion
}