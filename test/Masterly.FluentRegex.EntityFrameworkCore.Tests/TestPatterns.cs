using System.Text.RegularExpressions;

namespace Masterly.FluentRegex.EntityFrameworkCore.Tests;

/// <summary>
/// Patterns used across the tests, with the dialect regexes the core library translates them to. The dialect texts
/// keep the .NET meaning: <c>$</c> also accepts a final newline, and MySQL/MariaDB get <c>(?-i)</c> because their
/// collations are usually case-insensitive.
/// </summary>
public static class TestPatterns
{
    public const string SkuRegex = "^[A-Z]{3}-[0-9]{4}$";

    public const string SkuPostgreSql = @"\A[A-Z]{3}-[0-9]{4}(?=\n?\Z)";

    public const string SkuSqlServer = @"\A[A-Z]{3}-[0-9]{4}\n?\z";

    /// <summary>The MySQL and MariaDB regex as it appears inside a SQL literal, with backslashes doubled.</summary>
    public const string SkuMySqlLiteral = @"(?-i)\\A[A-Z]{3}-[0-9]{4}(?=\\n?\\z)";

    public static readonly Pattern Sku = Pattern.FromRegex(SkuRegex);

    public static Pattern SkuProperty => Sku;

    public static readonly Pattern Digits = Pattern.FromRegex("[0-9]+");

    public static readonly Pattern SkuParts = Pattern.FromRegex("^([A-Z]{3})-([0-9]{4})$");

    public static readonly Pattern NamedSkuParts = Pattern.FromRegex("^(?<letters>[A-Z]{3})-(?<number>[0-9]{4})$");

    public static readonly Pattern AbcSku = Pattern.FromRegex("^ABC-[0-9]{4}$");

    /// <summary>.NET numbers the unnamed group 1 and "year" 2; every database numbers them the other way round.</summary>
    /// <summary>
    /// Ends with \z: Group and Replace refuse '$' on SQL Server 2025, whose translation consumes the final newline that
    /// .NET leaves outside the match.
    /// </summary>
    public static readonly Pattern StrictSkuParts = Pattern.FromRegex(@"^([A-Z]{3})-([0-9]{4})\z");

    public static readonly Pattern StrictNamedSkuParts = Pattern.FromRegex(@"^(?<letters>[A-Z]{3})-(?<number>[0-9]{4})\z");

    /// <summary>.NET group 1 is the unnamed digits; the databases number the named letters 1 and the digits 2.</summary>
    public static readonly Pattern NamedLettersUnnamedDigits = Pattern.FromRegex(@"^(?<letters>[A-Z]{3})-([0-9]{4})\z");

    public static readonly Pattern YearMonth = Pattern.FromRegex("(?<year>[0-9]{4})-([0-9]{2})");

    /// <summary>Ends with \z instead of $, so a binary-collation LIKE translation is exact.</summary>
    public static readonly Pattern StrictSku = Pattern.FromRegex(@"^[A-Z]{3}-[0-9]{4}\z");

    public static readonly Pattern Email = Pattern.FromRegex(@"^[\w.+-]+@[\w-]+(\.[\w-]+)+$");

    public static readonly Pattern NameWithDigit = Pattern.FromRegex(@"^(?=.*[0-9])[A-Za-z0-9 ]+$");

    public static readonly Pattern CaseInsensitiveSku = Sku.WithOptions(RegexOptions.IgnoreCase);
}
