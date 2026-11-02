using System.Text.RegularExpressions;

namespace Masterly.FluentRegex.EntityFrameworkCore.Tests;

/// <summary>
/// Patterns used across the tests. The container tests only use ASCII constructs that mean the same thing in
/// .NET, PostgreSQL, ICU, PCRE and RE2, because the dialect adapter is still a shim.
/// </summary>
public static class TestPatterns
{
    public const string SkuRegex = "^[A-Z]{3}-[0-9]{4}$";

    public static readonly Pattern Sku = Pattern.FromRegex(SkuRegex);

    public static Pattern SkuProperty => Sku;

    public static readonly Pattern Digits = Pattern.FromRegex("[0-9]+");

    public static readonly Pattern SkuParts = Pattern.FromRegex("^([A-Z]{3})-([0-9]{4})$");

    public static readonly Pattern NamedSkuParts = Pattern.FromRegex("^(?<letters>[A-Z]{3})-(?<number>[0-9]{4})$");

    public static readonly Pattern AbcSku = Pattern.FromRegex("^ABC-[0-9]{4}$");

    public static readonly Pattern Email = Pattern.FromRegex(@"^[\w.+-]+@[\w-]+(\.[\w-]+)+$");

    public static readonly Pattern NameWithDigit = Pattern.FromRegex(@"^(?=.*[0-9])[A-Za-z0-9 ]+$");

    public static readonly Pattern CaseInsensitiveSku = Sku.WithOptions(RegexOptions.IgnoreCase);
}
