using System.Text.RegularExpressions;
using Masterly.FluentRegex;
using Masterly.FluentRegex.Showcase;

// A tour of Masterly.FluentRegex 3.0. Run with: dotnet run --project samples/Masterly.FluentRegex.Showcase

Section("1. Immutable building blocks and operators");
Pattern digits = Pattern.With.Digit.Repeat.OneOrMore;
Pattern price = Pattern.With.Literal("$") + digits + Pattern.With.Literal(".").Digit.Repeat.Times(2);
Pattern animal = Pattern.With.Literal("cat") | Pattern.With.Literal("dog") | Pattern.With.Literal("bird");
Console.WriteLine($"digits is unchanged and reusable: {digits}");
Console.WriteLine($"price:  {price}");
Console.WriteLine($"animal: {animal}");
Console.WriteLine($"lazy:   {Pattern.With.Literal("<").Anything.Repeat.OneOrMore.Lazy.Literal(">")}");
Console.WriteLine($"possessive: {Pattern.With.Digit.Repeat.OneOrMore.Possessive}");

Section("2. Reverse engineering: legacy regex to readable fluent code");
const string legacy = @"^(?<user>[\w.+-]+)@(?<domain>(?:[a-z0-9-]+\.)+[a-z]{2,})$";
Console.WriteLine(legacy);
Console.WriteLine();
Console.WriteLine(Pattern.FromRegex(legacy, RegexOptions.IgnoreCase).ToFluentCode());

Section("3. Typed extraction straight into records");
Pattern logLine = Pattern.With
    .StartOfLine
    .NamedGroup("timestamp", Pattern.With.NonWhitespace.Repeat.OneOrMore)
    .Whitespace.Repeat.OneOrMore
    .NamedGroup("level", Pattern.With.UppercaseLetter.Repeat.OneOrMore)
    .Whitespace.Repeat.OneOrMore
    .NamedGroup("message", Pattern.With.Anything.Repeat.OneOrMore)
    .EndOfLine
    .WithOptions(RegexOptions.Multiline);

const string log = """
    2024-05-17T10:30:00 INFO  Service started
    2024-05-17T10:31:12 WARN  Disk at 91%
    2024-05-17T10:32:40 ERROR Payment gateway timeout
    """;
foreach (LogEntry entry in logLine.ExtractAll<LogEntry>(log))
    Console.WriteLine($"{entry.Timestamp:HH:mm:ss} [{entry.Level,-5}] {entry.Message}");

SemanticVersion version = CommonPatterns.SemVer().Extract<SemanticVersion>("3.0.0-rc.2+build.77");
Console.WriteLine(version);

Section("4. Validated, fluent replacements");
Pattern isoDate = Pattern.With
    .NamedGroup("year", Pattern.With.Digit.Repeat.Times(4)).Literal("-")
    .NamedGroup("month", Pattern.With.Digit.Repeat.Times(2)).Literal("-")
    .NamedGroup("day", Pattern.With.Digit.Repeat.Times(2));
string[] months = System.Globalization.CultureInfo.InvariantCulture.DateTimeFormat.AbbreviatedMonthNames;
Console.WriteLine(isoDate.Replace("Released 2024-05-17, patched 2024-06-02.",
    r => r.Group("month", m => months[int.Parse(m) - 1]).Literal(" ").Group("day", d => d.TrimStart('0')).Literal(", ").Group("year")));
try
{
    isoDate.Replace("2024-05-17", r => r.Group("yaer"));
}
catch (ArgumentException ex)
{
    Console.WriteLine("Typo caught before replacing: " + ex.Message);
}

Section("5. Test data generation");
Console.WriteLine("IPv4 samples:      " + string.Join("  ", CommonPatterns.IPv4().GenerateMatching(5, seed: 42)));
Console.WriteLine("IPv4 near misses:  " + string.Join("  ", CommonPatterns.IPv4().GenerateNonMatching(5, seed: 42)));
Console.WriteLine("Passwords:         " + string.Join("  ", CommonPatterns.StrongPassword().GenerateMatching(3, seed: 7)));

Section("6. ReDoS analysis with proof");
// The build already flags this regex with warning FRX004; it is vulnerable on purpose for the demo.
#pragma warning disable FRX004
SafetyReport report = Pattern.FromRegex(@"^([a-zA-Z0-9]+[._-]?)*@example\.com$").AnalyzeSafety(confirm: true);
#pragma warning restore FRX004
Console.WriteLine(report);
Pattern safeVersion = Pattern.With
    .StartOfLine
    .NonCapturingGroup(Pattern.With.Set(Pattern.With.Letter.Digit).Repeat.OneOrMore.Possessive.AnyOf("._-").Repeat.Optional).Repeat.ZeroOrMore
    .Literal("@example.com")
    .EndOfLine;
Console.WriteLine("With .Possessive the risk is: " + safeVersion.AnalyzeSafety().Risk);

Section("7. Match tracing");
Console.WriteLine(CommonPatterns.DateTimeIso().Trace("2024-05-17T25:00"));

Section("8. Diagrams");
string outputDirectory = Path.Combine(AppContext.BaseDirectory, "diagrams");
Directory.CreateDirectory(outputDirectory);
File.WriteAllText(Path.Combine(outputDirectory, "email.svg"), CommonPatterns.Email().ToRailroadSvg());
File.WriteAllText(Path.Combine(outputDirectory, "semver.svg"), CommonPatterns.SemVer().ToRailroadSvg());
File.WriteAllText(Path.Combine(outputDirectory, "isodate.mmd"), isoDate.ToMermaid());
Console.WriteLine("Railroad diagrams and a Mermaid flowchart written to " + outputDirectory);
Console.WriteLine();
Console.WriteLine(isoDate.ToMermaid());

Section("9. Compile-time generated regexes ([GeneratePattern])");
Console.WriteLine("Evaluated by the compiler: " + Validators.ProductCodeSource);
Console.WriteLine("ABC-1234 valid? " + Validators.ProductCodeRegex().IsMatch("ABC-1234"));
Console.WriteLine("abc-12 valid?   " + Validators.ProductCodeRegex().IsMatch("abc-12"));

Section("10. Allocation-free span APIs");
ReadOnlySpan<char> text = "order 17, order 204, order 3".AsSpan();
Console.Write("Order numbers:");
foreach (ValueMatch match in digits.EnumerateMatches(text))
    Console.Write(" " + text.Slice(match.Index, match.Length).ToString());
Console.WriteLine();
Console.WriteLine("Count: " + digits.Count(text));

static void Section(string title)
{
    Console.WriteLine();
    Console.WriteLine(new string('=', 72));
    Console.WriteLine(title);
    Console.WriteLine(new string('=', 72));
}

namespace Masterly.FluentRegex.Showcase
{
    public enum Level { Debug, Info, Warn, Error }

    public record LogEntry(DateTime Timestamp, Level Level, string Message);

    public record SemanticVersion(int Major, int Minor, int Patch, string? Prerelease, string? BuildMetadata);

    public static partial class Validators
    {
        private static Pattern Prefix => Pattern.With.UppercaseLetter.Repeat.Times(3);

        /// <summary>Evaluated at build time by the FluentRegex source generator.</summary>
        [GeneratePattern]
        public static Pattern ProductCode => Pattern.With.StartOfLine.Then(Prefix).Literal("-").Digit.Repeat.Times(4).EndOfLine;
    }
}
