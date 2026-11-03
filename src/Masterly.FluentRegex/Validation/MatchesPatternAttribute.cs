#if NETCOREAPP
// System.ComponentModel.DataAnnotations is not part of netstandard2.1, so the attribute ships for .NET 8+.
#nullable enable
using System;
using System.Collections.Concurrent;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace Masterly.FluentRegex;

/// <summary>
/// Validates that a string property matches a <see cref="Pattern"/> or <see cref="ConstrainedPattern"/>
/// exposed by a public static property, field or parameterless method.
/// </summary>
/// <remarks>
/// <para>
/// The <em>whole</em> value must match, like <see cref="RegularExpressionAttribute"/>. Null and empty
/// strings are valid; combine with <see cref="RequiredAttribute"/> to reject them. Values that are not
/// strings are reported as validation errors.
/// </para>
/// <para>
/// The pattern is resolved once per type and member and cached. A missing member or a member of the
/// wrong type throws <see cref="InvalidOperationException"/> on first validation.
/// </para>
/// </remarks>
/// <example>
/// public sealed class Order
/// {
///     [MatchesPattern(typeof(Patterns), nameof(Patterns.Sku))]
///     public string? Sku { get; set; }
/// }
/// </example>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter, AllowMultiple = false)]
public sealed class MatchesPatternAttribute : ValidationAttribute
{
    private const string DefaultErrorMessage = "The field {0} does not match the required pattern.";
    private const string DefaultDisplayName = "value";
    private const BindingFlags MemberFlags = BindingFlags.Public | BindingFlags.Static;
    private const DynamicallyAccessedMemberTypes PatternMembers =
        DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.PublicFields |
        DynamicallyAccessedMemberTypes.PublicMethods;

    private static readonly ConcurrentDictionary<(Type Type, string Member), ConstrainedPattern> Cache =
        new ConcurrentDictionary<(Type Type, string Member), ConstrainedPattern>();

    private ConstrainedPattern? _pattern;

    /// <summary>
    /// Creates the attribute.
    /// </summary>
    /// <param name="patternType">The type that declares the pattern.</param>
    /// <param name="memberName">
    /// The name of a public static property, field or parameterless method of <paramref name="patternType"/>
    /// that returns a <see cref="Pattern"/> or <see cref="ConstrainedPattern"/>.
    /// </param>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="memberName"/> is empty.</exception>
    public MatchesPatternAttribute(
        [DynamicallyAccessedMembers(PatternMembers)] Type patternType,
        string memberName)
        : base(DefaultErrorMessage)
    {
        if (memberName == null)
        {
            throw new ArgumentNullException(nameof(memberName));
        }

        if (memberName.Trim().Length == 0)
        {
            throw new ArgumentException("The member name must not be empty.", nameof(memberName));
        }

        PatternType = patternType ?? throw new ArgumentNullException(nameof(patternType));
        MemberName = memberName;
    }

    /// <summary>Gets the type that declares the pattern.</summary>
    [DynamicallyAccessedMembers(PatternMembers)]
    public Type PatternType { get; }

    /// <summary>Gets the name of the member that provides the pattern.</summary>
    public string MemberName { get; }

    /// <summary>Validates <paramref name="value"/> against the whole pattern and its rules.</summary>
    /// <param name="value">The value to validate.</param>
    /// <param name="validationContext">The validation context, or null.</param>
    /// <returns><see cref="ValidationResult.Success"/> or a result describing the failure.</returns>
    /// <exception cref="InvalidOperationException">The pattern member cannot be resolved.</exception>
    protected override ValidationResult? IsValid(object? value, ValidationContext? validationContext)
    {
        if (value == null)
        {
            return ValidationResult.Success;
        }

        if (!(value is string text))
        {
            string message = $"The field {DisplayName(validationContext)} must be a string to be matched against a pattern.";
            return Failure(message, validationContext);
        }

        if (text.Length == 0)
        {
            return ValidationResult.Success;
        }

        ConstraintResult result = ResolvePattern().Check(text);
        return result.IsValid ? ValidationResult.Success : Failure(MessageFor(result, validationContext), validationContext);
    }

    private ConstrainedPattern ResolvePattern()
    {
        ConstrainedPattern? pattern = _pattern;
        if (pattern != null)
        {
            return pattern;
        }

        (Type Type, string Member) key = (PatternType, MemberName);
        if (!Cache.TryGetValue(key, out pattern))
        {
            pattern = Cache.GetOrAdd(key, ToConstrained(ReadMember()));
        }

        _pattern = pattern;
        return pattern;
    }

    private object? ReadMember()
    {
        try
        {
            return ReadMemberCore();
        }
        catch (TargetInvocationException ex)
        {
            throw new InvalidOperationException($"'{Describe()}' threw while providing the pattern.", ex.InnerException ?? ex);
        }
    }

    private object? ReadMemberCore()
    {
        PropertyInfo? property = PatternType.GetProperty(MemberName, MemberFlags);
        if (property != null && property.GetIndexParameters().Length == 0)
        {
            return property.GetValue(null);
        }

        FieldInfo? field = PatternType.GetField(MemberName, MemberFlags);
        if (field != null)
        {
            return field.GetValue(null);
        }

        MethodInfo? method = PatternType.GetMethod(MemberName, MemberFlags, null, Type.EmptyTypes, null);
        if (method != null)
        {
            return method.Invoke(null, null);
        }

        throw new InvalidOperationException(
            $"The type '{PatternType.FullName}' has no public static property, field or parameterless method named '{MemberName}'.");
    }

    private ConstrainedPattern ToConstrained(object? member)
    {
        switch (member)
        {
            case ConstrainedPattern constrained:
                return constrained;
            case Pattern pattern:
                return new ConstrainedPattern(pattern);
            case null:
                throw new InvalidOperationException($"'{Describe()}' returned null instead of a Pattern.");
            default:
                throw new InvalidOperationException(
                    $"'{Describe()}' returns '{member.GetType().FullName}', but a Pattern or ConstrainedPattern is required.");
        }
    }

    private string MessageFor(ConstraintResult result, ValidationContext? validationContext)
    {
        bool customMessage = ErrorMessage != null || ErrorMessageResourceType != null;
        if (result.FailedRule == null || customMessage)
        {
            return FormatErrorMessage(DisplayName(validationContext));
        }

        return $"The field {DisplayName(validationContext)} is invalid: {result.FailedRule}.";
    }

    private static ValidationResult Failure(string message, ValidationContext? validationContext)
    {
        string? member = validationContext?.MemberName;
        return member == null ? new ValidationResult(message) : new ValidationResult(message, new[] { member });
    }

    private static string DisplayName(ValidationContext? validationContext) =>
        validationContext?.DisplayName ?? DefaultDisplayName;

    private string Describe() => $"{PatternType.FullName}.{MemberName}";
}
#endif
