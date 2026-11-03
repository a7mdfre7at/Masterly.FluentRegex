namespace Masterly.FluentRegex.EntityFrameworkCore.Infrastructure;

/// <summary>The SQL Server collation under which LIKE compares UTF-16 code units exactly as .NET does.</summary>
internal static class SqlServerCollation
{
    /// <summary>
    /// A binary collation available on every supported SQL Server version. Under it LIKE is case- and accent-sensitive,
    /// <c>_</c> matches one UTF-16 code unit, ranges follow code-point order, nothing is ignorable and combining marks
    /// stay separate characters (verified on SQL Server 2025).
    /// </summary>
    public const string Binary = "Latin1_General_100_BIN2";

    /// <summary>Gets a value indicating whether a column needs <c>COLLATE <see cref="Binary"/></c> before LIKE.</summary>
    /// <param name="dialect">The dialect.</param>
    /// <param name="collation">What the column's collation is declared to do to LIKE.</param>
    public static bool NeedsBinaryCollate(SqlDialect dialect, LikeCollation collation) =>
        (dialect == SqlDialect.SqlServer || dialect == SqlDialect.SqlServer2025) && collation != LikeCollation.Binary;
}
