#nullable enable

namespace Masterly.FluentRegex;

/// <summary>What the database's collation does to LIKE comparisons.</summary>
public enum LikeCollation
{
    /// <summary>Characters compare by code point (SQL Server <c>_BIN2</c>, PostgreSQL <c>C</c>, MySQL <c>_bin</c>, SQLite with <c>case_sensitive_like</c>).</summary>
    Binary,

    /// <summary>
    /// Letters match regardless of case; everything else compares by code point. The superset relies only
    /// on what every case-insensitive collation folds (an ASCII letter with its ASCII partner, except
    /// <c>i</c>/<c>I</c>); exactness is relative to .NET's case folding
    /// (<see cref="System.Text.RegularExpressions.RegexOptions.IgnoreCase"/>), and a case-sensitive pattern
    /// with cased letters is never exact. Linguistic effects such as ignorable characters, contractions and
    /// accent folding are outside the model (see docs/features/like.md).
    /// </summary>
    CaseInsensitive
}
