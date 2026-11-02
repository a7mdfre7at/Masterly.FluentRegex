using System;
using System.Globalization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Masterly.FluentRegex.AspNetCore;

/// <summary>
/// A route constraint that accepts a route value only when a registered fluent pattern matches
/// the <em>whole</em> value and all of its semantic rules pass: <c>/items/{id:pattern(sku)}</c>.
/// </summary>
/// <remarks>
/// Registered under the name <c>pattern</c> by
/// <see cref="Microsoft.Extensions.DependencyInjection.FluentPatternsServiceCollectionExtensions.AddFluentPatterns"/>.
/// ASP.NET Core creates the constraint with the registry from dependency injection and the text
/// between the parentheses as the pattern name, when the route table is built.
/// </remarks>
public sealed class PatternRouteConstraint : IRouteConstraint
{
    /// <summary>Creates the constraint for a registered pattern.</summary>
    /// <param name="registry">The application's pattern registry.</param>
    /// <param name="name">The registered pattern name.</param>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    /// <exception cref="InvalidOperationException">No pattern named <paramref name="name"/> is registered.</exception>
    public PatternRouteConstraint(PatternRegistry registry, string name)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(name);
        Name = name.Trim();
        Pattern = registry.Get(Name);
    }

    /// <summary>Gets the registered pattern name.</summary>
    public string Name { get; }

    /// <summary>Gets the pattern route values must match.</summary>
    public ConstrainedPattern Pattern { get; }

    /// <summary>Tests whether the route value matches the whole pattern and satisfies its rules.</summary>
    /// <param name="httpContext">The current request, if any.</param>
    /// <param name="route">The router, if any.</param>
    /// <param name="routeKey">The name of the route parameter.</param>
    /// <param name="values">The route values.</param>
    /// <param name="routeDirection">Whether the route is matched or a URL is generated.</param>
    /// <returns><c>true</c> when the value is valid.</returns>
    public bool Match(HttpContext? httpContext, IRouter? route, string routeKey, RouteValueDictionary values, RouteDirection routeDirection)
    {
        ArgumentNullException.ThrowIfNull(routeKey);
        ArgumentNullException.ThrowIfNull(values);
        if (!values.TryGetValue(routeKey, out object? value) || value == null)
        {
            return false;
        }

        string? text = Convert.ToString(value, CultureInfo.InvariantCulture);
        return text != null && Pattern.IsValid(text);
    }
}
