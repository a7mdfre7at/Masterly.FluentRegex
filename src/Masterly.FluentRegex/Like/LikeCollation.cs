#nullable enable

namespace Masterly.FluentRegex;

/// <summary>What the database's collation does to LIKE comparisons.</summary>
public enum LikeCollation
{
    /// <summary>Characters compare by code point (SQL Server <c>_BIN2</c>, PostgreSQL <c>C</c>, MySQL <c>_bin</c>, SQLite with <c>case_sensitive_like</c>).</summary>
    Binary,

    /// <summary>
    /// Letters match regardless of case; everything else compares by code point. LIKE is modeled as a
    /// regex with <see cref="System.Text.RegularExpressions.RegexOptions.IgnoreCase"/>, so exactness is
    /// relative to .NET's case folding, and a case-sensitive pattern with cased letters is never exact.
    /// </summary>
    CaseInsensitive
}
