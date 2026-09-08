using System.Text.RegularExpressions;

namespace TecNM.Residency.Common;

public static class StringSanitizer
{
    private static readonly Regex MultipleSpacesRegex = new(@"\s+", RegexOptions.Compiled);

    /// <summary>
    /// Removes ALL whitespace characters (spaces, tabs, newlines, non-breaking spaces) and converts to uppercase.
    /// Ideal for Control Numbers (Matrículas) and CURP.
    /// </summary>
    public static string SanitizeControlNumber(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;
        var clean = MultipleSpacesRegex.Replace(input, string.Empty);
        return clean.Trim().ToUpperInvariant();
    }

    /// <summary>
    /// Removes ALL whitespace characters and converts to lowercase.
    /// Ideal for email addresses.
    /// </summary>
    public static string SanitizeEmail(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;
        var clean = MultipleSpacesRegex.Replace(input, string.Empty);
        return clean.Trim().ToLowerInvariant();
    }

    /// <summary>
    /// Trims leading and trailing whitespace, and collapses multiple consecutive spaces into a single space.
    /// Ideal for names, surnames, and general titles.
    /// </summary>
    public static string SanitizeText(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;
        var collapsed = MultipleSpacesRegex.Replace(input.Trim(), " ");
        return collapsed.Trim();
    }

    /// <summary>
    /// Removes all whitespace and converts to uppercase for CURP.
    /// </summary>
    public static string? SanitizeCurp(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return null;
        var clean = MultipleSpacesRegex.Replace(input, string.Empty).Trim().ToUpperInvariant();
        return string.IsNullOrWhiteSpace(clean) ? null : clean;
    }
}
