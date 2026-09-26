using System;
using System.Collections.Generic;
using System.IO;
using Solar.Captcha.Fonts;

namespace Solar.Captcha.Raster;

/// <summary>
/// Finds a font file on disk by font family name, so configuration can name a family instead of
/// hard-coding a path that differs between operating systems and containers.
/// </summary>
/// <remarks>
/// <para>
/// Resolution scans the platform's font directories and matches the family name recorded inside each
/// font file, falling back to a case-insensitive file-name match for fonts whose family table is
/// missing. Scanning means parsing every candidate font, so callers should resolve once at start-up
/// rather than per request.
/// </para>
/// <para>
/// A host that knows the exact file can bypass resolution entirely by supplying a path.
/// </para>
/// </remarks>
public static class RasterFontResolver
{
    private static readonly string[] SupportedExtensions = [".ttf", ".otf"];

    /// <summary>
    /// Gets the directories searched for fonts, in order.
    /// </summary>
    /// <remarks>
    /// The application's own <c>fonts</c> directory comes first, so a host can ship a font with the
    /// application and override anything installed on the machine.
    /// </remarks>
    public static IReadOnlyList<string> DefaultDirectories { get; } = BuildDefaultDirectories();

    /// <summary>
    /// Tries to find a font file for a family name in the default directories.
    /// </summary>
    /// <param name="familyName">The font family name, for example <c>Arial</c>.</param>
    /// <param name="fontPath">Receives the resolved font file path when the call succeeds.</param>
    /// <returns><see langword="true"/> when a font was found; otherwise <see langword="false"/>.</returns>
    /// <exception cref="ArgumentNullException">When <paramref name="familyName"/> is <see langword="null"/>.</exception>
    public static bool TryResolveFamilyName(string familyName, out string fontPath) =>
        TryResolveFamilyName(familyName, DefaultDirectories, out fontPath);

    /// <summary>
    /// Tries to find a font file for a family name in the given directories.
    /// </summary>
    /// <param name="familyName">The font family name, for example <c>Arial</c>.</param>
    /// <param name="directories">The directories to search, in order.</param>
    /// <param name="fontPath">Receives the resolved font file path when the call succeeds.</param>
    /// <returns><see langword="true"/> when a font was found; otherwise <see langword="false"/>.</returns>
    /// <exception cref="ArgumentNullException">
    /// When <paramref name="familyName"/> or <paramref name="directories"/> is <see langword="null"/>.
    /// </exception>
    public static bool TryResolveFamilyName(string familyName, IReadOnlyList<string> directories, out string fontPath)
    {
        ArgumentNullException.ThrowIfNull(familyName);
        ArgumentNullException.ThrowIfNull(directories);

        fontPath = string.Empty;

        var wanted = familyName.Trim();
        if (wanted.Length == 0)
        {
            return false;
        }

        string? fileNameMatch = null;

        foreach (var directory in directories)
        {
            if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
            {
                continue;
            }

            foreach (var file in EnumerateFontFiles(directory))
            {
                if (MatchesFamilyName(file, wanted))
                {
                    fontPath = file;
                    return true;
                }

                // Remember a file-name match, but keep looking for a real family-name match first.
                fileNameMatch ??= string.Equals(
                    Path.GetFileNameWithoutExtension(file),
                    wanted,
                    StringComparison.OrdinalIgnoreCase)
                        ? file
                        : null;
            }
        }

        if (fileNameMatch is not null)
        {
            fontPath = fileNameMatch;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Finds a font file for a family name, or throws when there is none.
    /// </summary>
    /// <param name="familyName">The font family name, for example <c>Arial</c>.</param>
    /// <returns>The resolved font file path.</returns>
    /// <exception cref="FileNotFoundException">When no installed font matches the family name.</exception>
    public static string ResolveFamilyName(string familyName)
    {
        if (TryResolveFamilyName(familyName, out var path))
        {
            return path;
        }

        throw new FileNotFoundException(
            $"No installed font matches the family name '{familyName}'. "
            + "Searched: " + string.Join(", ", DefaultDirectories) + ".",
            familyName);
    }

    /// <summary>
    /// Resolves a font source, preferring an explicit path when one is given.
    /// </summary>
    /// <param name="fontPath">An explicit font file path, or <see langword="null"/> or empty to resolve by name.</param>
    /// <param name="familyName">The family name to fall back to.</param>
    /// <returns>The font file path to load.</returns>
    /// <exception cref="FileNotFoundException">
    /// When the explicit path does not exist, or the family name cannot be resolved.
    /// </exception>
    public static string Resolve(string? fontPath, string familyName)
    {
        if (!string.IsNullOrWhiteSpace(fontPath))
        {
            if (!File.Exists(fontPath))
            {
                throw new FileNotFoundException(
                    $"The configured font file '{fontPath}' does not exist.",
                    fontPath);
            }

            return fontPath;
        }

        return ResolveFamilyName(familyName);
    }

    private static IEnumerable<string> EnumerateFontFiles(string directory)
    {
        IEnumerable<string> files;
        try
        {
            files = Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            yield break;
        }

        foreach (var file in files)
        {
            var extension = Path.GetExtension(file);
            if (SupportedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
            {
                yield return file;
            }
        }
    }

    private static bool MatchesFamilyName(string file, string wanted)
    {
        try
        {
            var font = TrueTypeFont.Load(file);
            return string.Equals(font.FamilyName?.Trim(), wanted, StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex) when (ex is GlyphRenderer.GlyphRendererException or IOException)
        {
            // A font this library cannot parse simply does not match; the caller may still find
            // what it needs in another file.
            return false;
        }
    }

    private static string[] BuildDefaultDirectories()
    {
        var directories = new List<string> { Path.Combine(AppContext.BaseDirectory, "fonts") };

        if (OperatingSystem.IsWindows())
        {
            var windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            if (!string.IsNullOrEmpty(windows))
            {
                directories.Add(Path.Combine(windows, "Fonts"));
            }
        }
        else
        {
            directories.Add("/usr/share/fonts");
            directories.Add("/usr/local/share/fonts");

            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (!string.IsNullOrEmpty(home))
            {
                directories.Add(Path.Combine(home, ".fonts"));
                directories.Add(Path.Combine(home, ".local", "share", "fonts"));
            }
        }

        return [.. directories];
    }
}
