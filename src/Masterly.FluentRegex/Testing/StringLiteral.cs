#nullable enable
using System.Globalization;
using System.Text;

namespace Masterly.FluentRegex.Testing;

/// <summary>
/// Renders strings as quoted C# literals so whitespace and control characters are visible in messages.
/// </summary>
internal static class StringLiteral
{
    private const char FirstPrintable = ' ';

    public static string Quote(string value)
    {
        StringBuilder builder = new StringBuilder(value.Length + 2);
        builder.Append('"');
        foreach (char c in value)
        {
            AppendEscaped(builder, c);
        }

        builder.Append('"');
        return builder.ToString();
    }

    private static void AppendEscaped(StringBuilder builder, char c)
    {
        switch (c)
        {
            case '"':
                builder.Append("\\\"");
                return;
            case '\\':
                builder.Append("\\\\");
                return;
            case '\0':
                builder.Append("\\0");
                return;
            case '\n':
                builder.Append("\\n");
                return;
            case '\r':
                builder.Append("\\r");
                return;
            case '\t':
                builder.Append("\\t");
                return;
        }

        if (c < FirstPrintable || char.IsControl(c))
        {
            builder.Append("\\u").Append(((int)c).ToString("X4", CultureInfo.InvariantCulture));
            return;
        }

        builder.Append(c);
    }
}
