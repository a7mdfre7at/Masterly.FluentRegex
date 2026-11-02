using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Threading.Tasks;
using Xunit;
using Xunit.Sdk;
using Xunit.v3;

namespace Masterly.FluentRegex.Xunit;

/// <summary>
/// Base class for theory data attributes that generate string samples from a <see cref="Pattern"/>
/// exposed by a public static property, field or parameterless method.
/// </summary>
/// <remarks>
/// Generation is seeded (default seed 0), so every run, and every test discovery, produces the same rows.
/// Each row is a single <see cref="string"/> argument, which the test runner shows in the row's display name.
/// </remarks>
public abstract class PatternDataAttribute : DataAttribute
{
    /// <summary>The member kinds the pattern can be read from, kept for trimming.</summary>
    protected const DynamicallyAccessedMemberTypes PatternMembers =
        DynamicallyAccessedMemberTypes.PublicProperties
        | DynamicallyAccessedMemberTypes.PublicFields
        | DynamicallyAccessedMemberTypes.PublicMethods;

    private const int DefaultCount = 25;
    private const BindingFlags StaticMembers = BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy;

    /// <summary>Initializes a new instance that reads the pattern from <paramref name="memberName"/> on <paramref name="patternSource"/>.</summary>
    /// <param name="patternSource">The type that declares the pattern member.</param>
    /// <param name="memberName">The name of a public static property, field or parameterless method returning <see cref="Pattern"/>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="patternSource"/> or <paramref name="memberName"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="memberName"/> is empty or whitespace.</exception>
    protected PatternDataAttribute([DynamicallyAccessedMembers(PatternMembers)] Type patternSource, string memberName)
    {
        ArgumentNullException.ThrowIfNull(patternSource);
        ArgumentException.ThrowIfNullOrWhiteSpace(memberName);
        PatternSource = patternSource;
        MemberName = memberName;
    }

    /// <summary>Gets the type that declares the pattern member.</summary>
    [DynamicallyAccessedMembers(PatternMembers)]
    public Type PatternSource { get; }

    /// <summary>Gets the name of the member that returns the pattern.</summary>
    public string MemberName { get; }

    /// <summary>
    /// Gets or sets how many rows to generate. Fewer rows are produced when the pattern has fewer
    /// distinct samples. Defaults to 25.
    /// </summary>
    public int Count { get; set; } = DefaultCount;

    /// <summary>Gets or sets the generation seed. Defaults to 0, so runs are reproducible.</summary>
    public int Seed { get; set; }

    /// <summary>Gets the word used for the samples in error messages, such as "matching".</summary>
    protected abstract string SampleKind { get; }

    /// <inheritdoc />
    public override ValueTask<IReadOnlyCollection<ITheoryDataRow>> GetData(MethodInfo testMethod, DisposalTracker disposalTracker)
    {
        ArgumentNullException.ThrowIfNull(testMethod);
        EnsureCountIsPositive();
        EnsureAcceptsString(testMethod);

        Pattern pattern = ResolvePattern();
        string[] samples = Generate(pattern, Count, Seed);
        if (samples.Length == 0)
        {
            throw new InvalidOperationException(
                $"{Describe()} produced no {SampleKind} samples for pattern '{pattern}', so theory '{testMethod.Name}' has no data.");
        }

        return new ValueTask<IReadOnlyCollection<ITheoryDataRow>>(ToRows(samples));
    }

    /// <inheritdoc />
    /// <remarks>Rows are deterministic, so they are safe to enumerate during discovery.</remarks>
    public override bool SupportsDiscoveryEnumeration() => true;

    /// <summary>Generates the samples for the theory.</summary>
    /// <param name="pattern">The resolved pattern.</param>
    /// <param name="count">The requested number of samples.</param>
    /// <param name="seed">The generation seed.</param>
    /// <returns>The generated samples.</returns>
    protected abstract string[] Generate(Pattern pattern, int count, int seed);

    private static IReadOnlyCollection<ITheoryDataRow> ToRows(string[] samples)
    {
        List<ITheoryDataRow> rows = new List<ITheoryDataRow>(samples.Length);
        foreach (string sample in samples)
        {
            rows.Add(new TheoryDataRow<string>(sample));
        }

        return rows;
    }

    private void EnsureCountIsPositive()
    {
        if (Count < 1)
        {
            throw new InvalidOperationException($"{Describe()} has Count = {Count}; it must be at least 1.");
        }
    }

    private void EnsureAcceptsString(MethodInfo testMethod)
    {
        ParameterInfo[] parameters = testMethod.GetParameters();
        if (parameters.Length == 0 || !parameters[0].ParameterType.IsAssignableFrom(typeof(string)))
        {
            throw new InvalidOperationException(
                $"{Describe()} supplies one string per row, but theory '{testMethod.Name}' does not take a string as its first parameter.");
        }
    }

    private Pattern ResolvePattern()
    {
        object? value = ReadMember(out Type? memberType);
        if (memberType == null)
        {
            throw new InvalidOperationException(
                $"{Describe()}: type '{PatternSource.FullName}' has no public static property, field or parameterless method named '{MemberName}'.");
        }

        if (!typeof(Pattern).IsAssignableFrom(memberType))
        {
            throw new InvalidOperationException(
                $"{Describe()}: member '{PatternSource.FullName}.{MemberName}' is of type '{memberType.FullName}', not '{typeof(Pattern).FullName}'.");
        }

        return value as Pattern
            ?? throw new InvalidOperationException($"{Describe()}: member '{PatternSource.FullName}.{MemberName}' returned null.");
    }

    private object? ReadMember(out Type? memberType)
    {
        PropertyInfo? property = PatternSource.GetProperty(MemberName, StaticMembers);
        if (property?.GetMethod != null && property.GetIndexParameters().Length == 0)
        {
            memberType = property.PropertyType;
            return IsPattern(memberType) ? property.GetValue(null) : null;
        }

        FieldInfo? field = PatternSource.GetField(MemberName, StaticMembers);
        if (field != null)
        {
            memberType = field.FieldType;
            return IsPattern(memberType) ? field.GetValue(null) : null;
        }

        MethodInfo? method = PatternSource.GetMethod(MemberName, StaticMembers, binder: null, Type.EmptyTypes, modifiers: null);
        memberType = method?.ReturnType;
        return method != null && IsPattern(method.ReturnType) ? method.Invoke(null, null) : null;
    }

    private static bool IsPattern(Type type) => typeof(Pattern).IsAssignableFrom(type);

    private string Describe() => $"[{GetType().Name.Replace("Attribute", string.Empty, StringComparison.Ordinal)}({PatternSource.Name}.{MemberName})]";
}
