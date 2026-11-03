using System.Globalization;
using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace Masterly.FluentRegex.AspNetCore.Tests;

public sealed class PatternRouteConstraintTests
{
    private static readonly Pattern Sku = Pattern.With.UppercaseLetter.Repeat.Times(3).Literal("-").Digit.Repeat.Times(4);

    private static readonly ConstrainedPattern Card = Pattern.With.Digit.Repeat.Times(16).Must(Validators.Luhn, "Luhn checksum");

    private static async Task<WebApplication> StartAsync(Action<PatternRegistry> configure, Action<WebApplication> map)
    {
        WebApplicationBuilder builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddFluentPatterns(configure);
        WebApplication app = builder.Build();
        map(app);
        await app.StartAsync(TestContext.Current.CancellationToken);
        return app;
    }

    private static Task<WebApplication> StartCatalogAsync() => StartAsync(
        registry => registry.Add("sku", Sku).Add("card", Card),
        app =>
        {
            app.MapGet("/items/{id:pattern(sku)}", (string id) => $"item {id}").WithName("item");
            app.MapGet("/cards/{number:pattern(CARD)}", (string number) => "card");
        });

    [Theory]
    [InlineData("/items/ABC-1234", HttpStatusCode.OK)]
    [InlineData("/items/ABC-12345", HttpStatusCode.NotFound)]
    [InlineData("/items/XABC-1234", HttpStatusCode.NotFound)]
    [InlineData("/items/abc-1234", HttpStatusCode.NotFound)]
    [InlineData("/cards/4111111111111111", HttpStatusCode.OK)]
    [InlineData("/cards/4111111111111112", HttpStatusCode.NotFound)]
    public async Task Get_RouteValue_IsRoutedOnlyWhenTheWholeValueMatches(string path, HttpStatusCode expected)
    {
        await using WebApplication app = await StartCatalogAsync();
        using HttpClient client = app.GetTestClient();

        HttpResponseMessage response = await client.GetAsync(path, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(expected);
    }

    [Fact]
    public async Task Get_RegistryRegisteredBeforeAddFluentPatterns_StillRegistersTheConstraint()
    {
        WebApplicationBuilder builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseTestServer();
        PatternRegistry ownRegistry = new PatternRegistry();
        builder.Services.AddSingleton(ownRegistry);
        builder.Services.AddFluentPatterns(registry => registry.Add("sku", Sku));
        await using WebApplication app = builder.Build();
        app.MapGet("/items/{id:pattern(sku)}", (string id) => $"item {id}");
        await app.StartAsync(TestContext.Current.CancellationToken);
        using HttpClient client = app.GetTestClient();

        string body = await client.GetStringAsync("/items/ABC-1234", TestContext.Current.CancellationToken);

        body.ShouldBe("item ABC-1234");
        ownRegistry.Contains("sku").ShouldBeTrue();
    }

    [Fact]
    public async Task Get_MatchingValue_BindsTheParameter()
    {
        await using WebApplication app = await StartCatalogAsync();
        using HttpClient client = app.GetTestClient();

        string body = await client.GetStringAsync("/items/XYZ-0042", TestContext.Current.CancellationToken);

        body.ShouldBe("item XYZ-0042");
    }

    [Fact]
    public async Task LinkGeneration_InvalidValue_ProducesNoLink()
    {
        await using WebApplication app = await StartCatalogAsync();
        LinkGenerator links = app.Services.GetRequiredService<LinkGenerator>();

        string? valid = links.GetPathByName("item", new { id = "ABC-1234" });
        string? invalid = links.GetPathByName("item", new { id = "not-a-sku" });

        valid.ShouldBe("/items/ABC-1234");
        invalid.ShouldBeNull();
    }

    [Fact]
    public async Task Get_UnknownPatternName_ThrowsAClearInvalidOperationException()
    {
        await using WebApplication app = await StartAsync(
            registry => registry.Add("sku", Sku),
            application => application.MapGet("/things/{id:pattern(nope)}", (string id) => id));
        using HttpClient client = app.GetTestClient();

        RouteCreationException exception = await Should.ThrowAsync<RouteCreationException>(
            () => client.GetAsync("/things/x", TestContext.Current.CancellationToken));

        Exception cause = exception.GetBaseException();
        cause.ShouldBeOfType<InvalidOperationException>();
        cause.Message.ShouldStartWith("No fluent pattern named 'nope' is registered (registered: sku)");
    }

    [Fact]
    public void AddFluentPatterns_CalledTwice_SharesOneRegistry()
    {
        ServiceCollection services = new ServiceCollection();

        services.AddFluentPatterns(registry => registry.Add("sku", Sku));
        services.AddFluentPatterns(registry => registry.Add("card", Card));
        using ServiceProvider provider = services.BuildServiceProvider();

        services.Count(descriptor => descriptor.ServiceType == typeof(PatternRegistry)).ShouldBe(1);
        provider.GetRequiredService<PatternRegistry>().Names.ShouldBe(new[] { "card", "sku" });
    }

    [Fact]
    public void AddFluentPatterns_RegistersThePatternConstraint()
    {
        ServiceCollection services = new ServiceCollection();

        services.AddFluentPatterns(registry => registry.Add("sku", Sku));
        using ServiceProvider provider = services.BuildServiceProvider();

        RouteOptions options = provider.GetRequiredService<IOptions<RouteOptions>>().Value;
        options.ConstraintMap["pattern"].ShouldBe(typeof(PatternRouteConstraint));
    }

    [Fact]
    public void AddFluentPatterns_NullArguments_Throw()
    {
        Should.Throw<ArgumentNullException>(() => ((IServiceCollection)null!).AddFluentPatterns(_ => { })).ParamName.ShouldBe("services");
        Should.Throw<ArgumentNullException>(() => new ServiceCollection().AddFluentPatterns(null!)).ParamName.ShouldBe("configure");
    }

    [Theory]
    [InlineData("ABC-1234", true)]
    [InlineData("ABC-1234x", false)]
    [InlineData("", false)]
    public void Match_StringValue_RequiresAWholeMatch(string value, bool expected)
    {
        PatternRouteConstraint constraint = new PatternRouteConstraint(new PatternRegistry().Add("sku", Sku), "sku");
        RouteValueDictionary values = new RouteValueDictionary { ["id"] = value };

        bool result = constraint.Match(null, null, "id", values, RouteDirection.IncomingRequest);

        result.ShouldBe(expected);
    }

    [Fact]
    public void Match_NonStringValue_IsFormattedInvariantly()
    {
        PatternRouteConstraint constraint = new PatternRouteConstraint(new PatternRegistry().Add("number", Pattern.With.Digit.Repeat.OneOrMore), "number");
        RouteValueDictionary values = new RouteValueDictionary { ["id"] = 42 };

        bool result = constraint.Match(null, null, "id", values, RouteDirection.UrlGeneration);

        result.ShouldBeTrue();
    }

    [Fact]
    public void Match_MissingOrNullValue_IsRejected()
    {
        PatternRouteConstraint constraint = new PatternRouteConstraint(new PatternRegistry().Add("sku", Sku), "sku");
        RouteValueDictionary values = new RouteValueDictionary { ["other"] = "ABC-1234", ["empty"] = null };

        bool missing = constraint.Match(null, null, "id", values, RouteDirection.IncomingRequest);
        bool empty = constraint.Match(null, null, "empty", values, RouteDirection.IncomingRequest);

        missing.ShouldBeFalse();
        empty.ShouldBeFalse();
    }

    [Fact]
    public void Constructor_ExposesTheResolvedPattern()
    {
        PatternRegistry registry = new PatternRegistry().Add("card", Card);

        PatternRouteConstraint constraint = new PatternRouteConstraint(registry, " card ");

        constraint.Name.ShouldBe("card");
        constraint.Pattern.ShouldBeSameAs(Card);
    }

    [Fact]
    public void Constructor_InvalidArguments_Throw()
    {
        PatternRegistry registry = new PatternRegistry();

        Should.Throw<ArgumentNullException>(() => new PatternRouteConstraint(null!, "sku")).ParamName.ShouldBe("registry");
        Should.Throw<ArgumentNullException>(() => new PatternRouteConstraint(registry, null!)).ParamName.ShouldBe("name");
        Should.Throw<InvalidOperationException>(() => new PatternRouteConstraint(registry, "sku")).Message.ShouldContain("(registered: none)");
    }

    [Theory]
    [InlineData(RouteDirection.IncomingRequest, "ABC-1234", true)]
    [InlineData(RouteDirection.IncomingRequest, "ABC-123", false)]
    [InlineData(RouteDirection.UrlGeneration, "ABC-1234", true)]
    [InlineData(RouteDirection.UrlGeneration, "ABC-123", false)]
    public void Match_BothRouteDirections_ApplyTheSameRule(RouteDirection direction, string value, bool expected)
    {
        PatternRouteConstraint constraint = new PatternRouteConstraint(new PatternRegistry().Add("sku", Sku), "sku");
        RouteValueDictionary values = new RouteValueDictionary { ["id"] = value };

        bool result = constraint.Match(null, null, "id", values, direction);

        result.ShouldBe(expected);
    }

    [Theory]
    [InlineData("4111111111111111", true)]
    [InlineData("4111111111111112", false)]
    [InlineData("411111111111111", false)]
    public void Match_ConstrainedPattern_RequiresTheMatchAndEveryRule(string value, bool expected)
    {
        PatternRouteConstraint constraint = new PatternRouteConstraint(new PatternRegistry().Add("card", Card), "card");
        RouteValueDictionary values = new RouteValueDictionary { ["number"] = value };

        bool result = constraint.Match(null, null, "number", values, RouteDirection.IncomingRequest);

        result.ShouldBe(expected);
    }

    [Fact]
    public void Match_RouteKey_IsLookedUpCaseInsensitively()
    {
        PatternRouteConstraint constraint = new PatternRouteConstraint(new PatternRegistry().Add("sku", Sku), "sku");
        RouteValueDictionary values = new RouteValueDictionary { ["ID"] = "ABC-1234" };

        bool result = constraint.Match(null, null, "id", values, RouteDirection.IncomingRequest);

        result.ShouldBeTrue();
    }

    [Fact]
    public void Match_EmptyRouteValues_IsRejected()
    {
        PatternRouteConstraint constraint = new PatternRouteConstraint(new PatternRegistry().Add("sku", Sku), "sku");

        bool result = constraint.Match(null, null, "id", new RouteValueDictionary(), RouteDirection.UrlGeneration);

        result.ShouldBeFalse();
    }

    [Theory]
    [InlineData(42L)]
    [InlineData(7)]
    [InlineData(true)]
    public void Match_NonStringValue_IsConvertedBeforeMatching(object value)
    {
        PatternRegistry registry = new PatternRegistry().Add("word", Pattern.With.Word.Repeat.OneOrMore);
        PatternRouteConstraint constraint = new PatternRouteConstraint(registry, "word");
        RouteValueDictionary values = new RouteValueDictionary { ["id"] = value };

        bool result = constraint.Match(null, null, "id", values, RouteDirection.IncomingRequest);

        result.ShouldBeTrue();
    }

    [Fact]
    public void Match_FormattableValue_IsFormattedWithTheInvariantCulture()
    {
        PatternRegistry registry = new PatternRegistry().Add("invariant", Pattern.With.Literal("invariant"));
        PatternRouteConstraint constraint = new PatternRouteConstraint(registry, "invariant");
        RouteValueDictionary values = new RouteValueDictionary { ["id"] = new CultureReportingValue() };

        bool result = constraint.Match(null, null, "id", values, RouteDirection.UrlGeneration);

        result.ShouldBeTrue();
    }

    [Fact]
    public void Match_DecimalValueUnderACommaDecimalCulture_UsesAnInvariantDecimalSeparator()
    {
        PatternRegistry registry = new PatternRegistry().Add("price", Pattern.With.Digit.Repeat.OneOrMore.Literal(".").Digit.Repeat.OneOrMore);
        PatternRouteConstraint constraint = new PatternRouteConstraint(registry, "price");
        RouteValueDictionary values = new RouteValueDictionary { ["id"] = 12.5m };
        using CultureScope culture = new CultureScope("de-DE");

        bool result = constraint.Match(null, null, "id", values, RouteDirection.UrlGeneration);

        result.ShouldBeTrue();
    }

    [Theory]
    [InlineData(RouteDirection.IncomingRequest)]
    [InlineData(RouteDirection.UrlGeneration)]
    public void Match_NullValueForAPatternThatAcceptsEmptyText_IsRejected(RouteDirection direction)
    {
        PatternRegistry registry = new PatternRegistry().Add("any", Pattern.With.Anything.Repeat.ZeroOrMore);
        PatternRouteConstraint constraint = new PatternRouteConstraint(registry, "any");
        RouteValueDictionary values = new RouteValueDictionary { ["id"] = null };

        bool result = constraint.Match(null, null, "id", values, direction);

        result.ShouldBeFalse();
    }

    [Fact]
    public void Match_MissingValueForAPatternThatAcceptsEmptyText_IsRejected()
    {
        PatternRegistry registry = new PatternRegistry().Add("any", Pattern.With.Anything.Repeat.ZeroOrMore);
        PatternRouteConstraint constraint = new PatternRouteConstraint(registry, "any");
        RouteValueDictionary values = new RouteValueDictionary { ["other"] = string.Empty };

        bool result = constraint.Match(null, null, "id", values, RouteDirection.UrlGeneration);

        result.ShouldBeFalse();
    }

    [Fact]
    public void Match_EmptyStringForAPatternThatAcceptsEmptyText_IsAccepted()
    {
        PatternRegistry registry = new PatternRegistry().Add("any", Pattern.With.Anything.Repeat.ZeroOrMore);
        PatternRouteConstraint constraint = new PatternRouteConstraint(registry, "any");
        RouteValueDictionary values = new RouteValueDictionary { ["id"] = string.Empty };

        bool result = constraint.Match(null, null, "id", values, RouteDirection.UrlGeneration);

        result.ShouldBeTrue();
    }

    [Fact]
    public void Match_ValueWhoseTextIsNull_IsRejected()
    {
        PatternRegistry registry = new PatternRegistry().Add("any", Pattern.With.Anything.Repeat.ZeroOrMore);
        PatternRouteConstraint constraint = new PatternRouteConstraint(registry, "any");
        RouteValueDictionary values = new RouteValueDictionary { ["id"] = new NullTextValue() };

        bool result = constraint.Match(null, null, "id", values, RouteDirection.UrlGeneration);

        result.ShouldBeFalse();
    }

    [Fact]
    public void Match_NullRouteKey_ThrowsArgumentNullException()
    {
        PatternRouteConstraint constraint = new PatternRouteConstraint(new PatternRegistry().Add("sku", Sku), "sku");

        ArgumentNullException exception = Should.Throw<ArgumentNullException>(
            () => constraint.Match(null, null, null!, new RouteValueDictionary(), RouteDirection.IncomingRequest));

        exception.ParamName.ShouldBe("routeKey");
    }

    [Fact]
    public void Match_NullValues_ThrowsArgumentNullException()
    {
        PatternRouteConstraint constraint = new PatternRouteConstraint(new PatternRegistry().Add("sku", Sku), "sku");

        ArgumentNullException exception = Should.Throw<ArgumentNullException>(
            () => constraint.Match(null, null, "id", null!, RouteDirection.IncomingRequest));

        exception.ParamName.ShouldBe("values");
    }

    [Fact]
    public void Constructor_NameInDifferentCase_ResolvesThePatternAndKeepsTheGivenName()
    {
        PatternRegistry registry = new PatternRegistry().Add("sku", Sku);

        PatternRouteConstraint constraint = new PatternRouteConstraint(registry, "SKU");

        constraint.Name.ShouldBe("SKU");
        constraint.Pattern.Pattern.ShouldBeSameAs(Sku);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_BlankName_ThrowsInvalidOperationException(string name)
    {
        PatternRegistry registry = new PatternRegistry().Add("sku", Sku);

        InvalidOperationException exception = Should.Throw<InvalidOperationException>(() => new PatternRouteConstraint(registry, name));

        exception.Message.ShouldStartWith("No fluent pattern named '' is registered (registered: sku).");
    }

    [Fact]
    public void Constructor_UnknownName_ListsTheRegisteredNames()
    {
        PatternRegistry registry = new PatternRegistry().Add("sku", Sku).Add("card", Card);

        InvalidOperationException exception = Should.Throw<InvalidOperationException>(() => new PatternRouteConstraint(registry, "zip"));

        exception.Message.ShouldStartWith("No fluent pattern named 'zip' is registered (registered: card, sku).");
    }

    [Fact]
    public void Constructor_PatternAddedLater_DoesNotChangeAnExistingConstraint()
    {
        PatternRegistry registry = new PatternRegistry().Add("sku", Sku);
        PatternRouteConstraint constraint = new PatternRouteConstraint(registry, "sku");

        registry.Add("card", Card);

        constraint.Pattern.Pattern.ShouldBeSameAs(Sku);
    }

    [Theory]
    [InlineData("/opt", "none")]
    [InlineData("/opt/ABC-1234", "ABC-1234")]
    public async Task Get_OptionalParameter_MissingValueIsAllowed(string path, string expected)
    {
        await using WebApplication app = await StartAsync(
            registry => registry.Add("sku", Sku),
            application => application.MapGet("/opt/{id:pattern(sku)?}", (string? id) => id ?? "none"));
        using HttpClient client = app.GetTestClient();

        string body = await client.GetStringAsync(path, TestContext.Current.CancellationToken);

        body.ShouldBe(expected);
    }

    [Fact]
    public async Task Get_OptionalParameter_InvalidValueIsNotRouted()
    {
        await using WebApplication app = await StartAsync(
            registry => registry.Add("sku", Sku),
            application => application.MapGet("/opt/{id:pattern(sku)?}", (string? id) => id ?? "none"));
        using HttpClient client = app.GetTestClient();

        HttpResponseMessage response = await client.GetAsync("/opt/bad", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Get_PercentEncodedValue_IsMatchedAfterDecoding()
    {
        await using WebApplication app = await StartCatalogAsync();
        using HttpClient client = app.GetTestClient();

        string body = await client.GetStringAsync("/items/ABC%2D1234", TestContext.Current.CancellationToken);

        body.ShouldBe("item ABC-1234");
    }

    [Fact]
    public async Task Get_PatternNameWithSurroundingSpaces_IsTrimmed()
    {
        await using WebApplication app = await StartAsync(
            registry => registry.Add("sku", Sku),
            application => application.MapGet("/spaced/{id:pattern( sku )}", (string id) => id));
        using HttpClient client = app.GetTestClient();

        string body = await client.GetStringAsync("/spaced/ABC-1234", TestContext.Current.CancellationToken);

        body.ShouldBe("ABC-1234");
    }

    [Fact]
    public async Task Get_ConstraintCombinedWithAnotherConstraint_RequiresBoth()
    {
        await using WebApplication app = await StartAsync(
            registry => registry.Add("digits", Pattern.With.Digit.Repeat.OneOrMore),
            application => application.MapGet("/short/{id:pattern(digits):maxlength(3)}", (string id) => id));
        using HttpClient client = app.GetTestClient();

        HttpResponseMessage accepted = await client.GetAsync("/short/123", TestContext.Current.CancellationToken);
        HttpResponseMessage tooLong = await client.GetAsync("/short/1234", TestContext.Current.CancellationToken);

        accepted.StatusCode.ShouldBe(HttpStatusCode.OK);
        tooLong.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task LinkGeneration_NonStringValue_ProducesALink()
    {
        await using WebApplication app = await StartAsync(
            registry => registry.Add("number", Pattern.With.Digit.Repeat.OneOrMore),
            application => application.MapGet("/numbers/{id:pattern(number)}", (string id) => id).WithName("number"));
        LinkGenerator links = app.Services.GetRequiredService<LinkGenerator>();

        string? path = links.GetPathByName("number", new { id = 42 });

        path.ShouldBe("/numbers/42");
    }

    [Fact]
    public async Task LinkGeneration_MissingValue_ProducesNoLink()
    {
        await using WebApplication app = await StartCatalogAsync();
        LinkGenerator links = app.Services.GetRequiredService<LinkGenerator>();

        string? path = links.GetPathByName("item", new { other = "ABC-1234" });

        path.ShouldBeNull();
    }

    [Fact]
    public async Task LinkGeneration_RuleViolation_ProducesNoLink()
    {
        await using WebApplication app = await StartAsync(
            registry => registry.Add("card", Card),
            application => application.MapGet("/cards/{number:pattern(card)}", (string number) => number).WithName("card"));
        LinkGenerator links = app.Services.GetRequiredService<LinkGenerator>();

        string? valid = links.GetPathByName("card", new { number = "4111111111111111" });
        string? invalid = links.GetPathByName("card", new { number = "4111111111111112" });

        valid.ShouldBe("/cards/4111111111111111");
        invalid.ShouldBeNull();
    }

    [Fact]
    public async Task Get_ConstraintWithoutAPatternName_FailsToBuildTheRoute()
    {
        await using WebApplication app = await StartAsync(
            registry => registry.Add("sku", Sku),
            application => application.MapGet("/bare/{id:pattern}", (string id) => id));
        using HttpClient client = app.GetTestClient();

        RouteCreationException exception = await Should.ThrowAsync<RouteCreationException>(
            () => client.GetAsync("/bare/ABC-1234", TestContext.Current.CancellationToken));

        exception.Message.ShouldContain(nameof(PatternRouteConstraint));
    }

    private sealed class CultureReportingValue : IFormattable
    {
        public string ToString(string? format, IFormatProvider? formatProvider) =>
            ReferenceEquals(formatProvider, CultureInfo.InvariantCulture) ? "invariant" : "culture-specific";

        public override string ToString() => "culture-specific";
    }

    private sealed class NullTextValue
    {
        public override string? ToString() => null;
    }

    private sealed class CultureScope : IDisposable
    {
        private readonly CultureInfo _previous = CultureInfo.CurrentCulture;

        public CultureScope(string name) => CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(name);

        public void Dispose() => CultureInfo.CurrentCulture = _previous;
    }
}
