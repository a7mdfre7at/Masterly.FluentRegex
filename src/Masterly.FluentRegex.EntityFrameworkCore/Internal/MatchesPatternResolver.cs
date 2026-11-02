using System;
using System.Reflection;

namespace Masterly.FluentRegex.EntityFrameworkCore.Infrastructure;

/// <summary>
/// Resolves the pattern a <see cref="MatchesPatternAttribute"/> refers to, exactly like the DataAnnotations
/// validator does: a public static property, field or parameterless method that returns a <see cref="Pattern"/>
/// or <see cref="ConstrainedPattern"/>. Only the <see cref="ConstrainedPattern.Pattern"/> part reaches SQL;
/// <c>Must</c> rules run in .NET only.
/// </summary>
internal static class MatchesPatternResolver
{
    private const BindingFlags MemberFlags = BindingFlags.Public | BindingFlags.Static;

    /// <summary>Returns the pattern <paramref name="attribute"/> refers to.</summary>
    /// <exception cref="InvalidOperationException">The member cannot be resolved or has the wrong type.</exception>
    public static Pattern Resolve(MatchesPatternAttribute attribute)
    {
        ArgumentNullException.ThrowIfNull(attribute);

        object? member = ReadMember(attribute);
        switch (member)
        {
            case ConstrainedPattern constrained:
                return constrained.Pattern;
            case Pattern pattern:
                return pattern;
            case null:
                throw new InvalidOperationException($"'{Describe(attribute)}' returned null instead of a Pattern.");
            default:
                throw new InvalidOperationException(
                    $"'{Describe(attribute)}' returns '{member.GetType().FullName}', but a Pattern or ConstrainedPattern is required.");
        }
    }

    private static object? ReadMember(MatchesPatternAttribute attribute)
    {
        try
        {
            return ReadMemberCore(attribute);
        }
        catch (TargetInvocationException ex)
        {
            throw new InvalidOperationException($"'{Describe(attribute)}' threw while providing the pattern.", ex.InnerException ?? ex);
        }
    }

    private static object? ReadMemberCore(MatchesPatternAttribute attribute)
    {
        Type type = attribute.PatternType;
        string name = attribute.MemberName;

        PropertyInfo? property = type.GetProperty(name, MemberFlags);
        if (property != null && property.GetIndexParameters().Length == 0)
        {
            return property.GetValue(null);
        }

        FieldInfo? field = type.GetField(name, MemberFlags);
        if (field != null)
        {
            return field.GetValue(null);
        }

        MethodInfo? method = type.GetMethod(name, MemberFlags, null, Type.EmptyTypes, null);
        if (method != null)
        {
            return method.Invoke(null, null);
        }

        throw new InvalidOperationException(
            $"The type '{type.FullName}' has no public static property, field or parameterless method named '{name}'.");
    }

    private static string Describe(MatchesPatternAttribute attribute) => $"{attribute.PatternType.FullName}.{attribute.MemberName}";
}
