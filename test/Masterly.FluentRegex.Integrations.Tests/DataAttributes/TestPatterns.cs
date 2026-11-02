namespace Masterly.FluentRegex.Integrations.Tests.DataAttributes;

/// <summary>Patterns exposed in every supported way for the data attributes.</summary>
public static class TestPatterns
{
    public const string FiveDigitsRegex = @"^\d{5}$";

    public static readonly Pattern FiveDigitsField = Pattern.FromRegex(FiveDigitsRegex);

    public static readonly string NotAPatternField = "not a pattern";

    public static Pattern HexColor => CommonPatterns.HexColor();

    public static Pattern? NullPattern => null;

    public static Pattern IsoDate() => CommonPatterns.DateIso();

    public static Pattern RequiresArgument(int length) => Pattern.FromRegex($"^a{{{length}}}$");

    public static Pattern SingleValue => Pattern.FromRegex("^x$");
}

/// <summary>Inherits its pattern members, to check that base-class members resolve.</summary>
public class DerivedPatternSource : BasePatternSource
{
}

/// <summary>Declares a pattern for <see cref="DerivedPatternSource"/>.</summary>
public class BasePatternSource
{
    public static Pattern Lowercase => Pattern.FromRegex("^[a-z]{3}$");
}
