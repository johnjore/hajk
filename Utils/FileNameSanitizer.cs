using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

public static class FileNameSanitizer
{
    // Set of reserved device names
    private static readonly string[] ReservedNames = {
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
    };

    //Unsafe filenames
    static readonly char[] UnsafeFileNameChars = new[] {
        '<', '>', ':', '"', '/', '\\', '|', '?', '*','\0', '\t', '\n', '\r'
    };

    public static string Sanitize(string input, string fallback = "filename")
    {
        if (string.IsNullOrWhiteSpace(input))
            return fallback;

        // Remove path separators
        input = input.Replace("/", "_").Replace("\\", "_");

        // Remove extension if present
        input = Path.GetFileNameWithoutExtension(input);

        // Remove invalid characters
        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = string.Join("_", input.Split(invalidChars, StringSplitOptions.RemoveEmptyEntries));

        var builder = new StringBuilder(input.Length);
        foreach (var ch in input)
        {
            if (Array.IndexOf(UnsafeFileNameChars, ch) >= 0)
                builder.Append("");
            else
                builder.Append(ch);
        }
        sanitized = builder.ToString();


        // Remove extra spaces, dots, or dashes at beginning/end
        sanitized = sanitized.Trim('.', ' ', '-');

        // Avoid hidden files
        if (sanitized.StartsWith("."))
            sanitized = sanitized.TrimStart('.');

        // Prevent reserved names
        if (Array.Exists(ReservedNames, name => name.Equals(sanitized, StringComparison.OrdinalIgnoreCase)))
            sanitized = $"_{sanitized}";

        // Fallback if result is empty
        if (string.IsNullOrEmpty(sanitized))
            return fallback;

        // Limit length (e.g., 100 chars to be safe, not 255)
        if (sanitized.Length > 100)
            sanitized = sanitized.Substring(0, 100);

        return sanitized;
    }
}
