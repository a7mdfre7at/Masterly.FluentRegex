#nullable enable
using System;

namespace Masterly.FluentRegex;

/// <summary>
/// Value equality. Two patterns are equal when they render to the same regex text with the same
/// <see cref="Options"/> and <see cref="Timeout"/>, regardless of how they were built.
/// </summary>
/// <remarks>
/// EF Core caches compiled queries by comparing the constants captured in an expression tree with
/// <see cref="Equals(object)"/>; without value equality every query that embeds a freshly built pattern
/// would compile again. Hash codes are consistent with equality, so patterns can key dictionaries.
/// The <c>==</c> and <c>!=</c> operators are deliberately not overloaded and keep reference semantics.
/// </remarks>
public partial class Pattern : IEquatable<Pattern>
{
    /// <summary>Tests whether <paramref name="other"/> renders to the same regex with the same options and timeout.</summary>
    /// <param name="other">The pattern to compare with.</param>
    /// <returns>True when both patterns are equal values.</returns>
    public bool Equals(Pattern? other)
    {
        if (other is null)
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        return other.GetType() == GetType() &&
               _options == other._options &&
               _timeout == other._timeout &&
               string.Equals(ToString(), other.ToString(), StringComparison.Ordinal);
    }

    /// <inheritdoc />
    public override bool Equals(object? obj) => Equals(obj as Pattern);

    /// <inheritdoc />
    public override int GetHashCode()
    {
        unchecked
        {
            int hash = StringComparer.Ordinal.GetHashCode(ToString());
            hash = (hash * 397) ^ (int)_options;
            hash = (hash * 397) ^ _timeout.GetHashCode();
            return hash;
        }
    }
}
