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
}
