using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;

namespace Masterly.FluentRegex.Tests;

public class DebugViewTests
{
    private static readonly Pattern Phone = Pattern.With
        .NamedGroup("area", Pattern.With.Digit.Repeat.Times(3))
        .Literal("-")
        .NamedGroup("number", Pattern.With.Digit.Repeat.Times(4))
        .WithOptions(RegexOptions.CultureInvariant)
        .WithTimeout(TimeSpan.FromSeconds(2));

    [Fact]
    public void Pattern_DebuggerDisplay_ShowsTheRegexText()
    {
        DebuggerDisplayAttribute? display = typeof(Pattern).GetCustomAttribute<DebuggerDisplayAttribute>();

        display.ShouldNotBeNull();
        display.Value.ShouldBe("{ToString(),nq}");
    }

    [Fact]
    public void Pattern_DebuggerTypeProxy_IsPatternDebugView()
    {
        DebuggerTypeProxyAttribute? proxy = typeof(Pattern).GetCustomAttribute<DebuggerTypeProxyAttribute>();

        proxy.ShouldNotBeNull();
        proxy.ProxyTypeName.ShouldStartWith(typeof(PatternDebugView).FullName!);
    }

    [Fact]
    public void DebugView_ValidPattern_ShowsTextOptionsTimeoutAndGroups()
    {
        PatternDebugView view = new PatternDebugView(Phone);

        view.Regex.ShouldBe(@"(?<area>\d{3})-(?<number>\d{4})");
        view.Options.ShouldBe(RegexOptions.CultureInvariant);
        view.Timeout.ShouldBe(TimeSpan.FromSeconds(2));
        view.GroupNames.ShouldBe(new[] { "area", "number" });
    }

    [Fact]
    public void DebugView_Explanation_ListsTheNonEmptyLinesOfExplain()
    {
        PatternDebugView view = new PatternDebugView(Phone);

        view.Explanation[0].ShouldBe(@"Pattern: (?<area>\d{3})-(?<number>\d{4})");
        view.Explanation.ShouldContain("  - Named capturing group 'area'");
        view.Explanation.ShouldNotContain(string.Empty);
    }

    [Fact]
    public void DebugView_SafePattern_ReportsNoRisk()
    {
        new PatternDebugView(Phone).Safety.ShouldBe("None: no catastrophic backtracking risks found.");
    }

    [Fact]
    public void DebugView_RiskyPattern_ReportsTheMostSevereIssue()
    {
        string safety = new PatternDebugView(Pattern.FromRegex(@"(a+)+$")).Safety;

        safety.ShouldStartWith("High: High NestedQuantifier");
    }

    [Fact]
    public void DebugView_InvalidPattern_NeverThrows()
    {
        PatternDebugView view = new PatternDebugView(Pattern.With.RegEx("(unclosed"));

        view.Regex.ShouldBe("(unclosed");
        view.GroupNames.Single().ShouldStartWith("Unavailable: ");
        view.Safety.ShouldStartWith("Unavailable: ");
        view.Explanation.ShouldNotBeEmpty();
    }

    [Fact]
    public void DebugView_RepeatedAccess_ReturnsTheCachedValue()
    {
        PatternDebugView view = new PatternDebugView(Phone);

        view.GroupNames.ShouldBeSameAs(view.GroupNames);
        view.Explanation.ShouldBeSameAs(view.Explanation);
    }

    [Fact]
    public void DebugView_NullPattern_ThrowsArgumentNull()
    {
        Should.Throw<ArgumentNullException>(() => new PatternDebugView(null!));
    }
}
