namespace Masterly.FluentRegex.Tests;

public class ExtendedCommonPatternsTests
{
    public static IEnumerable<object[]> Cases() => new (string Name, Func<Pattern> Factory, string[] Valid, string[] Invalid)[]
    {
        ("Email.Loose", () => CommonPatterns.Email(PatternStrictness.Loose),
            new[] { "a@b.c", "weird+chars!@sub.domain.museum", "üñî@exämple.de" },
            new[] { "no-at-sign", "a@b", "a b@c.d", "a@@b.c", "a@b..c" }),
        ("Email.Standard", () => CommonPatterns.Email(PatternStrictness.Standard),
            new[] { "user@example.com", "first.last@sub.example.org" },
            new[] { "user@example", "@example.com" }),
        ("Email.Strict", () => CommonPatterns.Email(PatternStrictness.Strict),
            new[] { "user@example.com", "o'brien+tag@mail.example.co.uk", "x@a-b.io" },
            new[] { "a..b@example.com", ".a@example.com", "a@-example.com", "a@example-.com", "a@example.c0m", new string('a', 65) + "@example.com" }),
        ("Url.Loose", () => CommonPatterns.Url(PatternStrictness.Loose),
            new[] { "ftp://files.example.com/a", "custom+app://x", "https://example.com" },
            new[] { "example.com", "://x", "http:// spaced.com" }),
        ("Url.Strict", () => CommonPatterns.Url(PatternStrictness.Strict),
            new[] { "https://example.com", "http://localhost:8080/api?q=1#top", "https://192.168.0.1/x", "https://a.b.example.org/p/a-t_h;x=1" },
            new[] { "ftp://example.com", "https://example", "https://example.com:70000", "https://ex ample.com", "https://300.1.1.1/" }),
        ("PhoneNumberE164", CommonPatterns.PhoneNumberE164,
            new[] { "+14155552671", "+442071838750", "+8613800138000" },
            new[] { "14155552671", "+0123456", "+1234567890123456", "+1 415 555 2671" }),
        ("Iban", CommonPatterns.Iban,
            new[] { "DE89370400440532013000", "GB82 WEST 1234 5698 7654 32", "FR1420041010050500013M02606" },
            new[] { "DE89", "de89370400440532013000", "DE8937040044053201300012345678901234567" }),
        ("SemVer", CommonPatterns.SemVer,
            new[] { "1.0.0", "0.1.2-alpha", "1.0.0-alpha.1+build.5", "10.20.30-rc.1+exp.sha.5114f85" },
            new[] { "1.0", "01.0.0", "1.0.0-", "1.0.0+", "1.0.0-01", "v1.0.0" }),
        ("Jwt", CommonPatterns.Jwt,
            new[] { "eyJhbGciOiJIUzI1NiJ9.eyJzdWIiOiIxMjMifQ.SflKxwRJSMeKKF2QT4fwpMeJf36POk6yJV_adQssw5c", "eyJhbGciOiJub25lIn0.eyJ4IjoxfQ." },
            new[] { "abc.def.ghi", "eyJ.eyJ", "eyJa.eyJb.c=d" }),
        ("MacAddress", CommonPatterns.MacAddress,
            new[] { "00:1A:2B:3C:4D:5E", "00-1a-2b-3c-4d-5e" },
            new[] { "00:1A-2B:3C:4D:5E", "001A2B3C4D5E", "00:1A:2B:3C:4D", "GG:1A:2B:3C:4D:5E" }),
        ("Base64", CommonPatterns.Base64,
            new[] { "TWFu", "TWE=", "TQ==", "SGVsbG8gV29ybGQh" },
            new[] { "", "TWF", "TQ=", "TQ===", "T@==" }),
        ("Slug", CommonPatterns.Slug,
            new[] { "hello", "hello-world-2024", "a-b-c" },
            new[] { "Hello", "hello--world", "-hello", "hello-", "hello world" }),
        ("DateTimeIso", CommonPatterns.DateTimeIso,
            new[] { "2024-05-17T10:30", "2024-05-17T10:30:59Z", "2024-05-17T23:59:59.1234567+02:00", "2024-12-31 00:00:00-0530" },
            new[] { "2024-13-17T10:30", "2024-05-32T10:30", "2024-05-17T24:00", "2024-05-17", "2024-05-17T10:60" }),
    }.Select(c => new object[] { c.Name, c.Factory, c.Valid, c.Invalid });

    [Theory]
    [MemberData(nameof(Cases))]
    public void ValidAndInvalidExamples(string name, Func<Pattern> factory, string[] valid, string[] invalid)
    {
        Pattern pattern = factory();
        foreach (string v in valid) pattern.IsMatch(v).ShouldBeTrue($"{name} should accept '{v}'");
        foreach (string i in invalid) pattern.IsMatch(i).ShouldBeFalse($"{name} should reject '{i}'");
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void IsSafe_AndGeneratesSamples(string name, Func<Pattern> factory, string[] valid, string[] invalid)
    {
        Pattern pattern = factory();
        SafetyReport report = pattern.AnalyzeSafety();
        report.IsSafe.ShouldBeTrue(name + "\n" + report);

        string[] samples = pattern.GenerateMatching(10, seed: 21);
        samples.Length.ShouldBe(10, name);
        foreach (string s in samples) pattern.IsMatch(s).ShouldBeTrue($"{name}: {s}");
        _ = valid;
        _ = invalid;
    }

    [Fact]
    public void Email_DefaultOverload_IsStandard()
    {
        CommonPatterns.Email(PatternStrictness.Standard).ToString().ShouldBe(CommonPatterns.Email().ToString());
        CommonPatterns.Url(PatternStrictness.Standard).ToString().ShouldBe(CommonPatterns.Url().ToString());
    }

    public record SemanticVersion(int Major, int Minor, int Patch, string? Prerelease, string? BuildMetadata);

    [Fact]
    public void SemVer_ExtractsIntoRecord()
    {
        SemanticVersion version = CommonPatterns.SemVer().Extract<SemanticVersion>("2.11.4-beta.3+sha.abc");
        version.ShouldBe(new SemanticVersion(2, 11, 4, "beta.3", "sha.abc"));

        CommonPatterns.SemVer().Extract<SemanticVersion>("1.0.0").Prerelease.ShouldBeNull();
    }

    public record UrlParts(string Scheme, string Host, int? Port, string Path, string? Query);

    [Fact]
    public void StrictUrl_ExtractsParts()
    {
        UrlParts parts = CommonPatterns.Url(PatternStrictness.Strict).Extract<UrlParts>("https://api.example.com:8443/v1/users?active=true");
        parts.ShouldBe(new UrlParts("https", "api.example.com", 8443, "/v1/users", "active=true"));
    }

    public record Link(string Text, string Url, string? Title);

    [Fact]
    public void MarkdownLink_FindsLinksButNotImages()
    {
        const string markdown = "See [the docs](https://example.com/docs \"Docs\") and ![logo](logo.png) or [home](/).";

        var links = CommonPatterns.MarkdownLink().ExtractAll<Link>(markdown).ToList();

        links.ShouldBe(new[] { new Link("the docs", "https://example.com/docs", "Docs"), new Link("home", "/", null) });
    }

    public record IsoStamp(int Year, int Month, int Day, int Hour, int Minute, string? Offset);

    [Fact]
    public void DateTimeIso_ExtractsComponents()
    {
        CommonPatterns.DateTimeIso().Extract<IsoStamp>("2024-05-17T10:30:00+02:00")
            .ShouldBe(new IsoStamp(2024, 5, 17, 10, 30, "+02:00"));
    }
}
