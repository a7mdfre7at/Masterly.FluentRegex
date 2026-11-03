using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace Masterly.FluentRegex.AspNetCore.Tests;

public sealed class FluentPatternsServiceCollectionExtensionsTests
{
    private static readonly Pattern Digits = Pattern.With.Digit.Repeat.OneOrMore;

    [Fact]
    public void ConstraintName_IsPattern()
    {
        string name = FluentPatternsServiceCollectionExtensions.ConstraintName;

        name.ShouldBe("pattern");
    }

    [Fact]
    public void AddFluentPatterns_ReturnsTheSameServiceCollection()
    {
        ServiceCollection services = new ServiceCollection();

        IServiceCollection result = services.AddFluentPatterns(registry => registry.Add("digits", Digits));

        result.ShouldBeSameAs(services);
    }

    [Fact]
    public void AddFluentPatterns_RegistersTheRegistryAsASingletonInstance()
    {
        ServiceCollection services = new ServiceCollection();
        PatternRegistry? configured = null;

        services.AddFluentPatterns(registry => configured = registry.Add("digits", Digits));
        using ServiceProvider provider = services.BuildServiceProvider();

        ServiceDescriptor descriptor = services.Single(item => item.ServiceType == typeof(PatternRegistry));
        descriptor.Lifetime.ShouldBe(ServiceLifetime.Singleton);
        descriptor.ImplementationInstance.ShouldBeSameAs(configured);
        provider.GetRequiredService<PatternRegistry>().ShouldBeSameAs(configured);
        provider.GetRequiredService<PatternRegistry>().ShouldBeSameAs(provider.GetRequiredService<PatternRegistry>());
    }

    [Fact]
    public void AddFluentPatterns_CalledTwice_PassesTheSameRegistryToBothCallbacks()
    {
        ServiceCollection services = new ServiceCollection();
        PatternRegistry? first = null;
        PatternRegistry? second = null;

        services.AddFluentPatterns(registry => first = registry);
        services.AddFluentPatterns(registry => second = registry);

        first.ShouldNotBeNull();
        second.ShouldBeSameAs(first);
    }

    [Fact]
    public void AddFluentPatterns_CalledTwice_RegistersTheConstraintOnce()
    {
        ServiceCollection services = new ServiceCollection();

        services.AddFluentPatterns(registry => registry.Add("digits", Digits));
        services.AddFluentPatterns(registry => registry.Add("more", Digits));

        services.Count(descriptor => descriptor.ServiceType == typeof(IConfigureOptions<RouteOptions>)).ShouldBe(1);
    }

    [Fact]
    public void AddFluentPatterns_ExistingKeyedRegistry_IsIgnoredAndLeftUntouched()
    {
        ServiceCollection services = new ServiceCollection();
        PatternRegistry keyed = new PatternRegistry().Add("keyed", Digits);
        services.AddKeyedSingleton("tenant", keyed);

        services.AddFluentPatterns(registry => registry.Add("digits", Digits));
        using ServiceProvider provider = services.BuildServiceProvider();

        PatternRegistry registry = provider.GetRequiredService<PatternRegistry>();
        registry.ShouldNotBeSameAs(keyed);
        registry.Names.ShouldBe(new[] { "digits" });
        provider.GetRequiredKeyedService<PatternRegistry>("tenant").Names.ShouldBe(new[] { "keyed" });
    }

    [Fact]
    public void AddFluentPatterns_ConfigureThrows_PropagatesTheException()
    {
        ServiceCollection services = new ServiceCollection();

        InvalidOperationException exception = Should.Throw<InvalidOperationException>(
            () => services.AddFluentPatterns(_ => throw new InvalidOperationException("boom")));

        exception.Message.ShouldBe("boom");
    }

    [Fact]
    public void AddFluentPatterns_DuplicateNameAcrossCalls_ThrowsArgumentException()
    {
        ServiceCollection services = new ServiceCollection();
        services.AddFluentPatterns(registry => registry.Add("digits", Digits));

        ArgumentException exception = Should.Throw<ArgumentException>(
            () => services.AddFluentPatterns(registry => registry.Add("Digits", Digits)));

        exception.ParamName.ShouldBe("name");
        exception.Message.ShouldStartWith("A pattern named 'Digits' is already registered.");
    }

    [Fact]
    public void AddFluentPatterns_DoesNotReplaceOtherRouteConstraints()
    {
        ServiceCollection services = new ServiceCollection();

        services.AddFluentPatterns(registry => registry.Add("digits", Digits));
        using ServiceProvider provider = services.BuildServiceProvider();

        RouteOptions options = provider.GetRequiredService<IOptions<RouteOptions>>().Value;
        options.ConstraintMap.ShouldContainKey("int");
        options.ConstraintMap.ShouldContainKey("regex");
    }
}
