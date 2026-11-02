using System;
using System.Text.RegularExpressions;

namespace Masterly.FluentRegex.EntityFrameworkCore.Infrastructure;

/// <summary>
/// Wraps a pattern as <c>\A(?:pattern)\z</c> so that it accepts an input only when it consumes all of it,
/// which is what a CHECK constraint must enforce.
/// </summary>
internal static class WholeStringPattern
{
    internal const string Prefix = @"\A(?:";

    internal const string Suffix = @")\z";

    private const string CommentTerminator = "\n";

    /// <summary>Returns the whole-string form of <paramref name="pattern"/> with the same options and timeout.</summary>
    public static Pattern Wrap(Pattern pattern)
    {
        ArgumentNullException.ThrowIfNull(pattern);

        // In IgnorePatternWhitespace mode a trailing "# comment" would swallow the closing parenthesis.
        bool ignoresWhitespace = (pattern.Options & RegexOptions.IgnorePatternWhitespace) != 0;
        string suffix = ignoresWhitespace ? CommentTerminator + Suffix : Suffix;
        return Pattern.With.RegEx(Prefix).Then(pattern).RegEx(suffix).WithOptions(pattern.Options).WithTimeout(pattern.Timeout);
    }
}
