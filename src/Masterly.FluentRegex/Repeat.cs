using System;
using System.Globalization;

namespace Masterly.FluentRegex
{
    /// <summary>
    /// Applies a quantifier to the last element of a <see cref="Pattern"/>.
    /// </summary>
    /// <remarks>
    /// Follow any quantifier with <see cref="Pattern.Lazy"/> for non-greedy matching, or with
    /// <see cref="Pattern.Possessive"/> to forbid backtracking into the repetition.
    /// </remarks>
    public class Repeat
    {
        private readonly Pattern _pattern;

        /// <summary>
        /// Creates a quantifier builder for the last element of <paramref name="parentPattern"/>.
        /// </summary>
        public Repeat(Pattern parentPattern) =>
            _pattern = parentPattern ?? throw new ArgumentNullException(nameof(parentPattern));

        private Pattern Apply(string quantifier)
        {
            Token last = _pattern.LastToken;
            if (last == null)
                throw new InvalidOperationException("Repeat must follow an element to repeat, for example Pattern.With.Digit.Repeat.OneOrMore.");

            // A quantifier directly on a quantifier is a .NET syntax error ("nested quantifier"),
            // except for '?' which is the legacy way of writing a lazy quantifier.
            if (last is QuantifiedToken && quantifier != "?")
                last = new WrapToken("(?:", Pattern.With.Append(last), ")");

            return _pattern.ReplaceLast(new QuantifiedToken(last, quantifier, QuantifierMode.Greedy));
        }

        /// <summary>One or more times: <c>+</c>.</summary>
        public Pattern OneOrMore => Apply("+");

        /// <summary>Zero or more times: <c>*</c>.</summary>
        public Pattern ZeroOrMore => Apply("*");

        /// <summary>Zero or one time: <c>?</c>.</summary>
        public Pattern Optional => Apply("?");

        /// <summary>Exactly <paramref name="exactly"/> times: <c>{n}</c>.</summary>
        public Pattern Times(int exactly)
        {
            if (exactly < 0) throw new ArgumentOutOfRangeException(nameof(exactly), "The count cannot be negative.");
            return Apply("{" + exactly.ToString(CultureInfo.InvariantCulture) + "}");
        }

        /// <summary>Between <paramref name="min"/> and <paramref name="max"/> times: <c>{min,max}</c>.</summary>
        public Pattern Times(int min, int max)
        {
            if (min < 0) throw new ArgumentOutOfRangeException(nameof(min), "The minimum cannot be negative.");
            if (max < min) throw new ArgumentOutOfRangeException(nameof(max), "The maximum cannot be less than the minimum.");
            return Apply("{" + min.ToString(CultureInfo.InvariantCulture) + "," + max.ToString(CultureInfo.InvariantCulture) + "}");
        }

        /// <summary>At least <paramref name="x"/> times: <c>{x,}</c>.</summary>
        public Pattern AtLeast(int x)
        {
            if (x < 0) throw new ArgumentOutOfRangeException(nameof(x), "The minimum cannot be negative.");
            return Apply("{" + x.ToString(CultureInfo.InvariantCulture) + ",}");
        }

        /// <summary>
        /// At most <paramref name="x"/> times: <c>{0,x}</c>.
        /// </summary>
        /// <remarks>
        /// .NET does not support the <c>{,x}</c> shorthand (it is matched as literal text),
        /// so the explicit lower bound is always emitted.
        /// </remarks>
        public Pattern AtMost(int x)
        {
            if (x < 0) throw new ArgumentOutOfRangeException(nameof(x), "The maximum cannot be negative.");
            return Apply("{0," + x.ToString(CultureInfo.InvariantCulture) + "}");
        }
    }
}
