using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace Masterly.FluentRegex.AspNetCore;

/// <summary>
/// The named patterns of an application, registered once with
/// <c>services.AddFluentPatterns(registry => registry.Add("sku", Patterns.Sku))</c> and used by the
/// <c>pattern</c> route constraint.
/// </summary>
/// <remarks>Names are case-insensitive. The registry is thread-safe.</remarks>
public sealed class PatternRegistry
{
    private readonly ConcurrentDictionary<string, ConstrainedPattern> _patterns =
        new ConcurrentDictionary<string, ConstrainedPattern>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Gets the registered names in ordinal order.</summary>
    public IReadOnlyList<string> Names => _patterns.Keys.OrderBy(name => name, StringComparer.Ordinal).ToList();

    /// <summary>Registers a pattern. Values must match it completely.</summary>
    /// <param name="name">The name used in route templates, such as <c>sku</c> in <c>{id:pattern(sku)}</c>.</param>
    /// <param name="pattern">The pattern.</param>
    /// <returns>This registry, for chaining.</returns>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    /// <exception cref="ArgumentException">The name is blank or already registered.</exception>
    public PatternRegistry Add(string name, Pattern pattern)
    {
        ArgumentNullException.ThrowIfNull(pattern);
        return Add(name, new ConstrainedPattern(pattern));
    }

    /// <summary>Registers a pattern with semantic rules. Values must match it completely and satisfy every rule.</summary>
    /// <param name="name">The name used in route templates.</param>
    /// <param name="pattern">The constrained pattern, for example <c>CommonPatterns.CreditCard().Must(Validators.Luhn)</c>.</param>
    /// <returns>This registry, for chaining.</returns>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    /// <exception cref="ArgumentException">The name is blank or already registered.</exception>
    public PatternRegistry Add(string name, ConstrainedPattern pattern)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(pattern);
        if (!_patterns.TryAdd(name, pattern))
        {
            throw new ArgumentException($"A pattern named '{name}' is already registered.", nameof(name));
        }

        return this;
    }

    /// <summary>Tests whether a pattern with the given name is registered.</summary>
    /// <param name="name">The pattern name.</param>
    /// <returns><c>true</c> when the name is registered.</returns>
    public bool Contains(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return _patterns.ContainsKey(name);
    }

    /// <summary>Finds a registered pattern.</summary>
    /// <param name="name">The pattern name.</param>
    /// <param name="pattern">The pattern, when found.</param>
    /// <returns><c>true</c> when the name is registered.</returns>
    public bool TryGet(string name, [NotNullWhen(true)] out ConstrainedPattern? pattern)
    {
        ArgumentNullException.ThrowIfNull(name);
        return _patterns.TryGetValue(name, out pattern);
    }

    /// <summary>Gets a registered pattern.</summary>
    /// <param name="name">The pattern name.</param>
    /// <returns>The pattern.</returns>
    /// <exception cref="InvalidOperationException">No pattern with that name is registered.</exception>
    public ConstrainedPattern Get(string name)
    {
        if (TryGet(name, out ConstrainedPattern? pattern))
        {
            return pattern;
        }

        string registered = _patterns.IsEmpty ? "none" : string.Join(", ", Names);
        throw new InvalidOperationException(
            $"No fluent pattern named '{name}' is registered (registered: {registered}). " +
            $"Register it with services.AddFluentPatterns(registry => registry.Add(\"{name}\", pattern)).");
    }
}
