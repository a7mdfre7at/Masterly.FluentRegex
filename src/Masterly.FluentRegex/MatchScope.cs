#nullable enable

namespace Masterly.FluentRegex;

/// <summary>Which set of strings a language fact or a LIKE translation describes.</summary>
public enum MatchScope
{
    /// <summary>
    /// The strings the pattern matches entirely, as in language algebra: as if the pattern were
    /// wrapped in <c>\A(?:...)\z</c>, where an explicit <c>$</c> keeps accepting a final newline.
    /// </summary>
    FullString,

    /// <summary>
    /// The strings for which <see cref="Pattern.IsMatch(string)"/> returns true: those that contain a
    /// match anywhere, unless the pattern is anchored at one or both ends.
    /// </summary>
    Substring
}
