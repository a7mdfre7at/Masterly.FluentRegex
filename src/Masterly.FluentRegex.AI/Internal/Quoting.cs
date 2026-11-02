using System.Globalization;
using System.Text;

namespace Masterly.FluentRegex.AI;

/// <summary>
/// Quotes strings JSON-style so whitespace and control characters are unambiguous, without escaping
/// printable characters such as <c>+</c> or <c>&lt;</c> that matter in regex examples.
/// </summary>
internal static class Quoting
{
    private const char FirstPrintable = ' ';

    public static string Quote(string value)
    {
        StringBuilder builder = new StringBuilder(value.Length + 2);
        builder.Append('"');
        foreach (char c in value)
        {
            builder.Append(Escape(c));
        }

        builder.Append('"');
        return builder.ToString();
    }

    private static string Escape(char c)
    {
        switch (c)
        {
            case '"':
                return "\\\"";
            case '\\':
                return "\\\\";
            case '\n':
                return "\\n";
            case '\r':
                return "\\r";
            case '\t':
                return "\\t";
        }

        if (c < FirstPrintable || char.IsControl(c))
        {
            return $"\\u{((int)c).ToString("X4", CultureInfo.InvariantCulture)}";
        }

        return c.ToString();
    }
}
