using System;
using System.Security.Cryptography;

namespace Solar.Captcha;

/// <summary>
/// Formats and normalizes the answerable time of a clock captcha.
/// </summary>
/// <remarks>
/// The canonical code is <c>H:mm</c> — hour without a leading zero, two-digit minute, for example
/// <c>3:45</c>. Answers are normalized rather than matched verbatim, so <c>3:45</c>, <c>03.45</c>,
/// <c>345</c> and <c>3 45</c> all describe the same time.
/// </remarks>
public static class ClockCaptchaCode
{
    /// <summary>Formats a time as the canonical clock code.</summary>
    public static string Format(int hours, int minutes) => $"{hours}:{minutes:00}";

    /// <summary>Parses a canonical clock code back into its hour and minute.</summary>
    public static (int Hours, int Minutes) Parse(string code)
    {
        var separator = code.IndexOf(':');
        return (int.Parse(code.AsSpan(0, separator)), int.Parse(code.AsSpan(separator + 1)));
    }

    /// <summary>
    /// Normalizes a submitted answer into an hour and minute, or reports it as unparseable.
    /// </summary>
    /// <remarks>
    /// Surrounding whitespace is trimmed; <c>:</c>, <c>.</c>, <c>-</c> and space are treated as
    /// equivalent separators; a leading zero on the hour is ignored; and a three- or four-digit
    /// number with no separator is read as compact <c>Hmm</c>/<c>HHmm</c>.
    /// </remarks>
    public static bool TryNormalize(string? input, out int hours, out int minutes)
    {
        hours = 0;
        minutes = 0;

        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        var value = input.Trim();

        int hour;
        int minute;

        if (IsAllDigits(value))
        {
            if (value.Length is not (3 or 4))
            {
                return false;
            }

            hour = int.Parse(value.AsSpan(0, value.Length - 2));
            minute = int.Parse(value.AsSpan(value.Length - 2));
        }
        else
        {
            var parts = value.Split([':', '.', '-', ' '], StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 2 || !int.TryParse(parts[0], out hour) || !int.TryParse(parts[1], out minute))
            {
                return false;
            }
        }

        if (hour is < 1 or > 12 || minute is < 0 or > 59)
        {
            return false;
        }

        hours = hour;
        minutes = minute;
        return true;
    }

    /// <summary>
    /// Reports whether a submitted answer describes the same time as a canonical code.
    /// </summary>
    public static bool Matches(string? userInput, string expectedCode)
    {
        if (!TryNormalize(userInput, out var inputHours, out var inputMinutes))
        {
            return false;
        }

        if (!TryNormalize(expectedCode, out var expectedHours, out var expectedMinutes))
        {
            return false;
        }

        return inputHours == expectedHours && inputMinutes == expectedMinutes;
    }

    private static bool IsAllDigits(string value)
    {
        foreach (var c in value)
        {
            if (!char.IsDigit(c))
            {
                return false;
            }
        }

        return value.Length > 0;
    }
}

/// <summary>
/// Generates clock captcha codes: an hour and a minute on the configured step, drawn from a
/// cryptographically secure source.
/// </summary>
public static class ClockCaptchaCodeGenerator
{
    /// <summary>Generates a canonical clock code with the given minute resolution.</summary>
    /// <param name="minuteStep">The minute step; must divide 60.</param>
    public static string Generate(int minuteStep)
    {
        var hours = RandomNumberGenerator.GetInt32(1, 13);

        var stepCount = 60 / minuteStep;
        var minute = RandomNumberGenerator.GetInt32(0, stepCount) * minuteStep;

        return ClockCaptchaCode.Format(hours, minute);
    }
}