using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Masterly.FluentRegex.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Masterly.FluentRegex.EntityFrameworkCore;

/// <summary>Query operators that filter rows by a <see cref="Pattern"/>.</summary>
public static class FluentRegexQueryableExtensions
{
    private static readonly MethodInfo MatchesMethod =
        ReflectionHelper.GetMethod(typeof(PatternFunctions), nameof(PatternFunctions.Matches), typeof(string), typeof(Pattern));

    private static readonly MethodInfo IsCandidateMethod =
        ReflectionHelper.GetMethod(typeof(PrefilterFunctions), nameof(PrefilterFunctions.IsCandidate), typeof(string), typeof(Pattern));

    /// <summary>
    /// Filters to the rows whose <paramref name="selector"/> value matches <paramref name="pattern"/>, entirely in SQL.
    /// The pattern is a compile-time constant of the query, so index hints apply. Throws like the translator when the
    /// dialect cannot express the pattern; use <see cref="MatchingAsync{T}(IQueryable{T}, Expression{Func{T, string}}, Pattern, CancellationToken)"/> then.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="source">The query.</param>
    /// <param name="selector">Selects the string to match.</param>
    /// <param name="pattern">The pattern.</param>
    /// <returns>The filtered query.</returns>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public static IQueryable<T> WhereMatches<T>(this IQueryable<T> source, Expression<Func<T, string?>> selector, Pattern pattern)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(selector);
        ArgumentNullException.ThrowIfNull(pattern);

        return source.Where(Predicate(selector, pattern, MatchesMethod));
    }

    /// <summary>
    /// Streams the rows whose <paramref name="selector"/> value matches <paramref name="pattern"/> on every database:
    /// SQL applies a proven superset of the pattern (the exact dialect regex when the translation is exact, otherwise
    /// a superset <c>LIKE</c> and length bound, otherwise only a NULL check) and .NET applies the exact pattern.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="source">The query.</param>
    /// <param name="selector">Selects the string to match.</param>
    /// <param name="pattern">The pattern.</param>
    /// <param name="cancellationToken">Cancels the enumeration.</param>
    /// <returns>The matching rows, in query order.</returns>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public static IAsyncEnumerable<T> MatchingAsync<T>(
        this IQueryable<T> source,
        Expression<Func<T, string?>> selector,
        Pattern pattern,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(selector);
        ArgumentNullException.ThrowIfNull(pattern);

        return MatchingCore(source, selector, pattern, pattern.IsMatch, cancellationToken);
    }

    /// <summary>
    /// Streams the rows whose <paramref name="selector"/> value matches <paramref name="pattern"/> and satisfies
    /// every <c>Must</c> rule. SQL applies a proven superset of the underlying pattern; the rules run in .NET.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="source">The query.</param>
    /// <param name="selector">Selects the string to match.</param>
    /// <param name="pattern">The constrained pattern.</param>
    /// <param name="cancellationToken">Cancels the enumeration.</param>
    /// <returns>The matching rows, in query order.</returns>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public static IAsyncEnumerable<T> MatchingAsync<T>(
        this IQueryable<T> source,
        Expression<Func<T, string?>> selector,
        ConstrainedPattern pattern,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(selector);
        ArgumentNullException.ThrowIfNull(pattern);

        return MatchingCore(source, selector, pattern.Pattern, pattern.IsMatch, cancellationToken);
    }

    /// <summary>
    /// Loads the rows whose <paramref name="selector"/> value matches <paramref name="pattern"/>; see
    /// <see cref="MatchingAsync{T}(IQueryable{T}, Expression{Func{T, string}}, Pattern, CancellationToken)"/>.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="source">The query.</param>
    /// <param name="selector">Selects the string to match.</param>
    /// <param name="pattern">The pattern.</param>
    /// <param name="cancellationToken">Cancels the query.</param>
    /// <returns>The matching rows, in query order.</returns>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public static async Task<List<T>> ToMatchingListAsync<T>(
        this IQueryable<T> source,
        Expression<Func<T, string?>> selector,
        Pattern pattern,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(selector);
        ArgumentNullException.ThrowIfNull(pattern);

        List<T> results = new List<T>();
        await foreach (T item in MatchingCore(source, selector, pattern, pattern.IsMatch, cancellationToken).ConfigureAwait(false))
        {
            results.Add(item);
        }

        return results;
    }

    /// <summary>
    /// Loads the rows whose <paramref name="selector"/> value matches <paramref name="pattern"/> and satisfies
    /// every <c>Must</c> rule; see <see cref="MatchingAsync{T}(IQueryable{T}, Expression{Func{T, string}}, ConstrainedPattern, CancellationToken)"/>.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="source">The query.</param>
    /// <param name="selector">Selects the string to match.</param>
    /// <param name="pattern">The constrained pattern.</param>
    /// <param name="cancellationToken">Cancels the query.</param>
    /// <returns>The matching rows, in query order.</returns>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public static async Task<List<T>> ToMatchingListAsync<T>(
        this IQueryable<T> source,
        Expression<Func<T, string?>> selector,
        ConstrainedPattern pattern,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(selector);
        ArgumentNullException.ThrowIfNull(pattern);

        List<T> results = new List<T>();
        await foreach (T item in MatchingCore(source, selector, pattern.Pattern, pattern.IsMatch, cancellationToken).ConfigureAwait(false))
        {
            results.Add(item);
        }

        return results;
    }

    private static async IAsyncEnumerable<T> MatchingCore<T>(
        IQueryable<T> source,
        Expression<Func<T, string?>> selector,
        Pattern pattern,
        Func<string, bool> isMatch,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        IQueryable<T> candidates = source.Where(Predicate(selector, pattern, IsCandidateMethod));
        Func<T, string?> read = selector.Compile();
        await foreach (T item in candidates.AsAsyncEnumerable().WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            string? value = read(item);
            if (value != null && isMatch(value))
            {
                yield return item;
            }
        }
    }

    /// <summary>Builds <c>x => method(selector(x), pattern)</c> with the pattern embedded as a constant.</summary>
    private static Expression<Func<T, bool>> Predicate<T>(Expression<Func<T, string?>> selector, Pattern pattern, MethodInfo method)
    {
        MethodCallExpression call = Expression.Call(method, selector.Body, Expression.Constant(pattern, typeof(Pattern)));
        return Expression.Lambda<Func<T, bool>>(call, selector.Parameters);
    }
}
