using System;
using Microsoft.EntityFrameworkCore;

namespace Masterly.FluentRegex.EntityFrameworkCore;

/// <summary>
/// <c>EF.Functions</c> entry points for pattern translation: <c>EF.Functions.Matches(u.Email, Patterns.Email)</c>.
/// They translate exactly like <see cref="PatternFunctions"/> and can only be used inside a LINQ query.
/// </summary>
public static class FluentRegexDbFunctionsExtensions
{
    private const string ClientEvaluationMessage =
        "This method can only be used inside a LINQ to Entities query translated by Entity Framework Core. " +
        "Use PatternFunctions for code that also runs in .NET.";

    /// <summary>True when <paramref name="pattern"/> matches anywhere in <paramref name="value"/>; NULL never matches.</summary>
    /// <param name="functions">The <see cref="EF.Functions"/> instance.</param>
    /// <param name="value">The text to search.</param>
    /// <param name="pattern">The pattern.</param>
    /// <returns>True when the pattern matches.</returns>
    /// <exception cref="InvalidOperationException">The method was evaluated in .NET instead of being translated.</exception>
    public static bool Matches(this DbFunctions functions, string? value, Pattern pattern) =>
        throw new InvalidOperationException(ClientEvaluationMessage);

    /// <summary>The text captured by the named group in the first match, or NULL (PostgreSQL and SQL Server 2025).</summary>
    /// <param name="functions">The <see cref="EF.Functions"/> instance.</param>
    /// <param name="value">The text to search.</param>
    /// <param name="pattern">The pattern.</param>
    /// <param name="groupName">The group name.</param>
    /// <returns>The captured text, or null.</returns>
    /// <exception cref="InvalidOperationException">The method was evaluated in .NET instead of being translated.</exception>
    public static string? Group(this DbFunctions functions, string? value, Pattern pattern, string groupName) =>
        throw new InvalidOperationException(ClientEvaluationMessage);

    /// <summary>The text captured by the numbered group in the first match, or NULL (PostgreSQL and SQL Server 2025).</summary>
    /// <param name="functions">The <see cref="EF.Functions"/> instance.</param>
    /// <param name="value">The text to search.</param>
    /// <param name="pattern">The pattern.</param>
    /// <param name="groupNumber">The group number; 0 is the whole match.</param>
    /// <returns>The captured text, or null.</returns>
    /// <exception cref="InvalidOperationException">The method was evaluated in .NET instead of being translated.</exception>
    public static string? Group(this DbFunctions functions, string? value, Pattern pattern, int groupNumber) =>
        throw new InvalidOperationException(ClientEvaluationMessage);

    /// <summary>Replaces every match with <paramref name="replacement"/> in .NET syntax (PostgreSQL, MySQL, MariaDB, SQL Server 2025).</summary>
    /// <param name="functions">The <see cref="EF.Functions"/> instance.</param>
    /// <param name="value">The text.</param>
    /// <param name="pattern">The pattern.</param>
    /// <param name="replacement">The replacement, in .NET syntax.</param>
    /// <returns>The replaced text, or null.</returns>
    /// <exception cref="InvalidOperationException">The method was evaluated in .NET instead of being translated.</exception>
    public static string? Replace(this DbFunctions functions, string? value, Pattern pattern, string replacement) =>
        throw new InvalidOperationException(ClientEvaluationMessage);
}
