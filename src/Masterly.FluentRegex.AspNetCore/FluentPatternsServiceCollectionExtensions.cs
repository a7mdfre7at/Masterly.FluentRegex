using System;
using System.Linq;
using Masterly.FluentRegex.AspNetCore;
using Microsoft.AspNetCore.Routing;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>Registers Masterly.FluentRegex patterns with ASP.NET Core.</summary>
public static class FluentPatternsServiceCollectionExtensions
{
    /// <summary>The route constraint name: <c>{id:pattern(sku)}</c>.</summary>
    public const string ConstraintName = "pattern";

    /// <summary>
    /// Registers a <see cref="PatternRegistry"/> singleton and the <c>pattern</c> route constraint.
    /// Calling it again adds more patterns to the same registry.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Adds the named patterns.</param>
    /// <returns>The service collection, for chaining.</returns>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    /// <example>
    /// builder.Services.AddFluentPatterns(registry => registry.Add("sku", Patterns.Sku));
    /// app.MapGet("/items/{id:pattern(sku)}", (string id) => id);
    /// </example>
    public static IServiceCollection AddFluentPatterns(this IServiceCollection services, Action<PatternRegistry> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        PatternRegistry registry = ExistingRegistry(services) ?? RegisterRegistry(services);

        // Registered separately from the registry: an app may have added its own PatternRegistry instance first.
        RegisterConstraintOnce(services);
        configure(registry);
        return services;
    }

    private static PatternRegistry? ExistingRegistry(IServiceCollection services) =>
        services.Where(descriptor => descriptor.ServiceType == typeof(PatternRegistry) && !descriptor.IsKeyedService)
            .Select(descriptor => descriptor.ImplementationInstance)
            .OfType<PatternRegistry>()
            .FirstOrDefault();

    private static PatternRegistry RegisterRegistry(IServiceCollection services)
    {
        PatternRegistry registry = new PatternRegistry();
        services.AddSingleton(registry);
        return registry;
    }

    private static void RegisterConstraintOnce(IServiceCollection services)
    {
        if (services.Any(descriptor => descriptor.ServiceType == typeof(ConstraintRegistered)))
        {
            return;
        }

        services.AddSingleton<ConstraintRegistered>();
        services.Configure<RouteOptions>(options => options.SetParameterPolicy<PatternRouteConstraint>(ConstraintName));
    }

    /// <summary>Marks a service collection whose route options already know the <c>pattern</c> constraint.</summary>
    private sealed class ConstraintRegistered
    {
    }
}
