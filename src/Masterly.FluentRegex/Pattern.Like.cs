#nullable enable
using System;
using Masterly.FluentRegex.Like;

namespace Masterly.FluentRegex;

/// <summary>
/// LIKE compilation: the best SQL LIKE pattern for this pattern, with a proof of whether it is exact.
/// </summary>
public partial class Pattern
{
    /// <summary>
    /// Compiles this pattern to a SQL LIKE pattern that accepts every string the pattern accepts, and
    /// proves whether it accepts exactly those strings.
    /// </summary>
    /// <param name="dialect">The LIKE syntax of the database; only <see cref="LikeDialect.SqlServer"/> has sets.</param>
    /// <param name="scope">
    /// <see cref="MatchScope.Substring"/> (the default) for the strings <see cref="IsMatch(string)"/> accepts,
    /// or <see cref="MatchScope.FullString"/> for the strings matched entirely.
    /// </param>
    /// <param name="collation">
    /// What the database's collation does to LIKE. With <see cref="LikeCollation.CaseInsensitive"/> the
    /// LIKE pattern is modeled with <see cref="System.Text.RegularExpressions.RegexOptions.IgnoreCase"/>,
    /// so a case-sensitive pattern with cased letters is never exact.
    /// </param>
    /// <returns>
    /// The LIKE pattern. <see cref="LikeConversion.IsExact"/> is true only when language algebra proved the
    /// LIKE pattern equivalent to the scoped pattern; otherwise the result is the tightest proven superset
    /// (<c>prefix%suffix</c> at worst, or <c>%</c>). Never throws for a valid pattern.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">An enum argument is not defined.</exception>
    /// <example>
    /// Pattern.FromRegex(@"^INV-[0-9]{4}-[0-9]{4}\z").ToLike(LikeDialect.SqlServer).ToSql("[Number]");
    /// // [Number] LIKE 'INV-[0-9][0-9][0-9][0-9]-[0-9][0-9][0-9][0-9]'   (IsExact: true)
    /// Pattern.FromRegex(@"^INV-\d{4}-\d{4}\z").ToLike(LikeDialect.PostgreSql).Expression;
    /// // "INV-%"   (IsExact: false)
    /// </example>
    public LikeConversion ToLike(LikeDialect dialect, MatchScope scope = MatchScope.Substring, LikeCollation collation = LikeCollation.Binary)
    {
        if (dialect < LikeDialect.Standard || dialect > LikeDialect.Sqlite)
        {
            throw new ArgumentOutOfRangeException(nameof(dialect), dialect, "Unknown LIKE dialect.");
        }

        if (scope != MatchScope.FullString && scope != MatchScope.Substring)
        {
            throw new ArgumentOutOfRangeException(nameof(scope), scope, "Unknown match scope.");
        }

        if (collation != LikeCollation.Binary && collation != LikeCollation.CaseInsensitive)
        {
            throw new ArgumentOutOfRangeException(nameof(collation), collation, "Unknown LIKE collation.");
        }

        return LikeTranslator.Translate(this, dialect, scope, collation);
    }
}
