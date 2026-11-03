using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;
using Masterly.FluentRegex.Unicode;

namespace Masterly.FluentRegex
{
    /// <summary>
    /// Overrides the regex group a constructor parameter or property is bound to by
    /// <see cref="Pattern.Extract{T}(string, IFormatProvider)"/>.
    /// </summary>
    /// <example>
    /// public record Version([RegexGroup("major")] int Major, [RegexGroup("minor")] int Minor);
    /// </example>
    [AttributeUsage(AttributeTargets.Parameter | AttributeTargets.Property, AllowMultiple = false)]
    public sealed class RegexGroupAttribute : Attribute
    {
        /// <summary>Binds the member to the named group.</summary>
        public RegexGroupAttribute(string name) => Name = name ?? throw new ArgumentNullException(nameof(name));

        /// <summary>Gets the group name.</summary>
        public string Name { get; }
    }

    /// <summary>
    /// Custom string-to-value converters used by typed extraction, for types beyond the built-in
    /// primitives, enums, dates, <see cref="Guid"/>, <see cref="Uri"/> and <see cref="Version"/>.
    /// </summary>
    public static class ExtractionConverters
    {
        private static readonly ConcurrentDictionary<Type, Func<string, IFormatProvider, object>> Converters =
            new ConcurrentDictionary<Type, Func<string, IFormatProvider, object>>();

        /// <summary>Registers a converter for <typeparamref name="T"/>, replacing any existing one.</summary>
        /// <example>
        /// ExtractionConverters.Register(s =&gt; new Money(decimal.Parse(s)));
        /// </example>
        public static void Register<T>(Func<string, IFormatProvider, T> converter)
        {
            if (converter == null) throw new ArgumentNullException(nameof(converter));
            Converters[typeof(T)] = (text, provider) => converter(text, provider);
        }

        /// <summary>Registers a culture-independent converter for <typeparamref name="T"/>.</summary>
        public static void Register<T>(Func<string, T> converter)
        {
            if (converter == null) throw new ArgumentNullException(nameof(converter));
            Converters[typeof(T)] = (text, _) => converter(text);
        }

        /// <summary>Removes the converter for <typeparamref name="T"/>.</summary>
        public static bool Unregister<T>() => Converters.TryRemove(typeof(T), out _);

        internal static bool TryGet(Type type, out Func<string, IFormatProvider, object> converter) =>
            Converters.TryGetValue(type, out converter);
    }

    /// <summary>
    /// Binds regex group values to the constructor parameters and settable properties of a type.
    /// </summary>
    internal static class TypeBinder<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicProperties)] T>
    {
        private static readonly ConcurrentDictionary<string, Plan> Plans = new ConcurrentDictionary<string, Plan>(StringComparer.Ordinal);

        private sealed class Plan
        {
            public ConstructorInfo Constructor;
            public ParameterInfo[] Parameters;
            public string[] ParameterGroups; // null entry: unbound, use default
            public List<KeyValuePair<PropertyInfo, string>> Properties;
        }

        public static T Bind(Match match, Regex regex, IFormatProvider provider) => Bind(match, regex, provider, null);

        /// <summary>
        /// Binds like <see cref="Bind(Match, Regex, IFormatProvider)"/>, but converts group text with
        /// <paramref name="converter"/> when it is given (used by templates for per-placeholder formats).
        /// </summary>
        public static T Bind(Match match, Regex regex, IFormatProvider provider, GroupConverter converter)
        {
            Plan plan = Plans.GetOrAdd(regex.ToString() + "\u0000" + ((int)regex.Options).ToString(CultureInfo.InvariantCulture), _ => CreatePlan(regex));
            provider = provider ?? CultureInfo.InvariantCulture;

            var arguments = new object[plan.Parameters.Length];
            for (int i = 0; i < arguments.Length; i++)
            {
                ParameterInfo parameter = plan.Parameters[i];
                string groupName = plan.ParameterGroups[i];
                if (groupName != null && match.Groups[groupName].Success)
                    arguments[i] = Convert(match.Groups[groupName].Value, parameter.ParameterType, groupName, provider, converter);
                else
                    arguments[i] = parameter.HasDefaultValue ? parameter.DefaultValue : DefaultOf(parameter.ParameterType);
            }

            // Kept boxed while the properties are set: for a struct, setting them on an unboxed T would change a
            // temporary copy each time and lose every value. A struct without a public constructor starts as default.
            object instance = plan.Constructor != null ? plan.Constructor.Invoke(arguments) : default(T);
            foreach (KeyValuePair<PropertyInfo, string> property in plan.Properties)
            {
                Group group = match.Groups[property.Value];
                if (group.Success) property.Key.SetValue(instance, Convert(group.Value, property.Key.PropertyType, property.Value, provider, converter));
            }
            return (T)instance;
        }

        private static Plan CreatePlan(Regex regex)
        {
            var groups = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (string name in regex.GetGroupNames())
            {
                if (!groups.ContainsKey(name)) groups[name] = name;
            }

            Type type = typeof(T);
            ConstructorInfo[] constructors = type.GetConstructors();
            if (constructors.Length == 0 && !type.IsValueType)
                throw new InvalidOperationException("Type '" + type.FullName + "' has no public constructor.");

            ConstructorInfo constructor = SelectConstructor(constructors);

            ParameterInfo[] parameters = constructor?.GetParameters() ?? Array.Empty<ParameterInfo>();
            var parameterGroups = new string[parameters.Length];
            var missing = new List<string>();
            var boundNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < parameters.Length; i++)
            {
                ParameterInfo parameter = parameters[i];
                string wanted = GroupNameOf(type, parameter);
                boundNames.Add(parameter.Name);
                if (groups.TryGetValue(wanted, out string actual)) parameterGroups[i] = actual;
                else if (!parameter.HasDefaultValue && !IsNullable(parameter.ParameterType)) missing.Add(parameter.Name + " (group '" + wanted + "')");
            }

            if (missing.Count > 0)
            {
                throw new InvalidOperationException(
                    "Cannot extract '" + type.Name + "': the pattern has no group for " + string.Join(", ", missing) +
                    ". Available groups: " + string.Join(", ", regex.GetGroupNames()) + ". " +
                    "Add NamedGroup(...) to the pattern, give the parameter a default value, or use [RegexGroup(\"name\")].");
            }

            var properties = new List<KeyValuePair<PropertyInfo, string>>();
            bool hasSettableProperty = false;
            foreach (PropertyInfo property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (boundNames.Contains(property.Name) || !IsSettable(property)) continue;
                hasSettableProperty = true;
                string wanted = GroupNameOf(property);
                if (groups.TryGetValue(wanted, out string actual)) properties.Add(new KeyValuePair<PropertyInfo, string>(property, actual));
            }

            // Only a struct can get here without a constructor. It is built from default and its settable properties.
            if (constructor == null && !hasSettableProperty)
                throw new InvalidOperationException("Value type '" + type.FullName + "' needs a public constructor or settable properties.");

            return new Plan { Constructor = constructor, Parameters = parameters, ParameterGroups = parameterGroups, Properties = properties };
        }

        /// <summary>
        /// Describes the members a match can bind to, using the same constructor selection and
        /// group naming rules as <see cref="Bind(Match, Regex, IFormatProvider)"/>.
        /// </summary>
        public static IReadOnlyList<BindableMember> GetMembers()
        {
            Type type = typeof(T);
            var members = new List<BindableMember>();
            var boundNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            ConstructorInfo constructor = SelectConstructor(type.GetConstructors());
            foreach (ParameterInfo parameter in constructor?.GetParameters() ?? Array.Empty<ParameterInfo>())
            {
                boundNames.Add(parameter.Name);
                bool required = !parameter.HasDefaultValue && !IsNullable(parameter.ParameterType);
                members.Add(new BindableMember(GroupNameOf(type, parameter), parameter.ParameterType, FindProperty(type, parameter.Name), required));
            }

            foreach (PropertyInfo property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (boundNames.Contains(property.Name) || !IsSettable(property)) continue;
                members.Add(new BindableMember(GroupNameOf(property), property.PropertyType, property, false));
            }
            return members;
        }

        private static ConstructorInfo SelectConstructor(ConstructorInfo[] constructors)
        {
            ConstructorInfo constructor = null;
            foreach (ConstructorInfo candidate in constructors)
            {
                if (constructor == null || candidate.GetParameters().Length > constructor.GetParameters().Length) constructor = candidate;
            }
            return constructor;
        }

        private static string GroupNameOf([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] Type type, ParameterInfo parameter) =>
            parameter.GetCustomAttribute<RegexGroupAttribute>()?.Name
            ?? FindProperty(type, parameter.Name)?.GetCustomAttribute<RegexGroupAttribute>()?.Name
            ?? parameter.Name;

        private static string GroupNameOf(PropertyInfo property) => property.GetCustomAttribute<RegexGroupAttribute>()?.Name ?? property.Name;

        private static bool IsSettable(PropertyInfo property) =>
            property.SetMethod != null && property.SetMethod.IsPublic && property.GetIndexParameters().Length == 0;

        private static PropertyInfo FindProperty([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] Type type, string name)
        {
            foreach (PropertyInfo property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase)) return property;
            }
            return null;
        }

        private static bool IsNullable(Type type) => !type.IsValueType || Nullable.GetUnderlyingType(type) != null;

        private static object DefaultOf(Type type) => type.IsValueType && Nullable.GetUnderlyingType(type) == null ? CreateDefault(type) : null;

        [UnconditionalSuppressMessage("Trimming", "IL2067", Justification = "Value-type defaults never need trimmed members.")]
        private static object CreateDefault(Type type) => Activator.CreateInstance(type);

        internal static object Convert(string text, Type type, string groupName, IFormatProvider provider, GroupConverter converter = null)
        {
            try
            {
                return converter != null ? converter(groupName, text, type, provider) : ValueConverter.Convert(text, type, provider);
            }
            catch (Exception ex) when (ex is FormatException || ex is OverflowException || ex is ArgumentException)
            {
                throw new FormatException("Group '" + groupName + "' value \"" + text + "\" cannot be converted to " + type.Name + ": " + ex.Message, ex);
            }
        }
    }

    /// <summary>Converts the text of a named group to a member value.</summary>
    internal delegate object GroupConverter(string groupName, string text, Type type, IFormatProvider provider);

    /// <summary>A constructor parameter or settable property that a named group can bind to.</summary>
    internal sealed class BindableMember
    {
        public BindableMember(string groupName, Type type, PropertyInfo property, bool isRequired)
        {
            GroupName = groupName;
            Type = type;
            Property = property;
            IsRequired = isRequired;
        }

        /// <summary>Gets the group name the member binds to (after <see cref="RegexGroupAttribute"/>).</summary>
        public string GroupName { get; }

        /// <summary>Gets the member type.</summary>
        public Type Type { get; }

        /// <summary>Gets the public property that exposes the member's value, or null.</summary>
        public PropertyInfo Property { get; }

        /// <summary>Gets a value indicating whether a constructor parameter has no default and is not nullable.</summary>
        public bool IsRequired { get; }
    }

    internal static class ValueConverter
    {
        /// <summary>
        /// Converts group text to <paramref name="type"/>. Integer and floating-point parsing first maps
        /// Unicode decimal digits (which <c>\d</c> matches) to ASCII, so "٢٠٢٦" converts to 2026.
        /// </summary>
        public static object Convert(string text, Type type, IFormatProvider provider)
        {
            if (ExtractionConverters.TryGet(type, out Func<string, IFormatProvider, object> custom)) return custom(text, provider);

            Type underlying = Nullable.GetUnderlyingType(type);
            if (underlying != null)
            {
                if (text.Length == 0) return null;
                type = underlying;
                if (ExtractionConverters.TryGet(type, out custom)) return custom(text, provider);
            }

            if (type == typeof(string) || type == typeof(object)) return text;
            if (type.IsEnum) return Enum.Parse(type, text.Trim(), ignoreCase: true);
            if (type == typeof(int)) return int.Parse(UnicodeDigits.ToAscii(text), NumberStyles.Integer, provider);
            if (type == typeof(long)) return long.Parse(UnicodeDigits.ToAscii(text), NumberStyles.Integer, provider);
            if (type == typeof(short)) return short.Parse(UnicodeDigits.ToAscii(text), NumberStyles.Integer, provider);
            if (type == typeof(byte)) return byte.Parse(UnicodeDigits.ToAscii(text), NumberStyles.Integer, provider);
            if (type == typeof(sbyte)) return sbyte.Parse(UnicodeDigits.ToAscii(text), NumberStyles.Integer, provider);
            if (type == typeof(uint)) return uint.Parse(UnicodeDigits.ToAscii(text), NumberStyles.Integer, provider);
            if (type == typeof(ulong)) return ulong.Parse(UnicodeDigits.ToAscii(text), NumberStyles.Integer, provider);
            if (type == typeof(ushort)) return ushort.Parse(UnicodeDigits.ToAscii(text), NumberStyles.Integer, provider);
            if (type == typeof(decimal)) return decimal.Parse(UnicodeDigits.ToAsciiNumber(text, provider), NumberStyles.Number, provider);
            if (type == typeof(double)) return double.Parse(UnicodeDigits.ToAsciiNumber(text, provider), NumberStyles.Float | NumberStyles.AllowThousands, provider);
            if (type == typeof(float)) return float.Parse(UnicodeDigits.ToAsciiNumber(text, provider), NumberStyles.Float | NumberStyles.AllowThousands, provider);
            if (type == typeof(bool)) return ParseBool(text);
            if (type == typeof(char))
            {
                if (text.Length != 1) throw new FormatException("Expected exactly one character.");
                return text[0];
            }
            if (type == typeof(DateTime)) return DateTime.Parse(text, provider, DateTimeStyles.RoundtripKind);
            if (type == typeof(DateTimeOffset)) return DateTimeOffset.Parse(text, provider, DateTimeStyles.None);
            if (type == typeof(TimeSpan)) return TimeSpan.Parse(text, provider);
            if (type == typeof(Guid)) return Guid.Parse(text);
            if (type == typeof(Uri)) return new Uri(text, UriKind.RelativeOrAbsolute);
            if (type == typeof(Version)) return Version.Parse(text);
#if NET6_0_OR_GREATER
            if (type == typeof(DateOnly)) return DateOnly.Parse(text, provider);
            if (type == typeof(TimeOnly)) return TimeOnly.Parse(text, provider);
#endif
            throw new ArgumentException("No converter for type '" + type.FullName + "'. Register one with ExtractionConverters.Register.");
        }

        private static bool ParseBool(string text)
        {
            switch (text.Trim().ToLowerInvariant())
            {
                case "true":
                case "yes":
                case "y":
                case "on":
                case "1":
                    return true;
                case "false":
                case "no":
                case "n":
                case "off":
                case "0":
                    return false;
                default:
                    throw new FormatException("'" + text + "' is not a boolean.");
            }
        }
    }
}
