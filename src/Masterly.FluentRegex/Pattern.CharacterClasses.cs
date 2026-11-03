using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Masterly.FluentRegex
{
    /// <summary>
    /// The general Unicode category groups, matched with <c>\p{L}</c>, <c>\p{N}</c> and so on.
    /// </summary>
    public enum UnicodeGroup
    {
        /// <summary>Any letter: <c>\p{L}</c>.</summary>
        Letter,

        /// <summary>Any combining mark: <c>\p{M}</c>.</summary>
        Mark,

        /// <summary>Any number: <c>\p{N}</c>.</summary>
        Number,

        /// <summary>Any punctuation: <c>\p{P}</c>.</summary>
        Punctuation,

        /// <summary>Any symbol: <c>\p{S}</c>.</summary>
        Symbol,

        /// <summary>Any separator: <c>\p{Z}</c>.</summary>
        Separator,

        /// <summary>Control, format, surrogate, private use and unassigned characters: <c>\p{C}</c>.</summary>
        Other
    }

    /// <summary>
    /// Character class members: ranges, explicit character lists, Unicode categories and extra anchors.
    /// </summary>
    public partial class Pattern
    {
        /// <summary>
        /// Matches one character in the inclusive range. Renders <c>[a-f]</c> on its own and
        /// <c>a-f</c> inside a <see cref="Set"/>, so ranges can be combined.
        /// </summary>
        /// <example>
        /// Pattern.With.Set(Pattern.With.Range('a', 'f').Range('A', 'F').Digit)  // [a-fA-F\d]
        /// </example>
        /// <exception cref="ArgumentException"><paramref name="from"/> is greater than <paramref name="to"/>.</exception>
        public Pattern Range(char from, char to)
        {
            if (from > to)
                throw new ArgumentException("The range start '" + from + "' is greater than the range end '" + to + "'.", nameof(from));
            string body = RangeBoundary(from) + "-" + RangeBoundary(to);
            return Append(ClassToken.Bracketed(body));
        }

        // .NET does not let an escaped hyphen start a range, so boundaries that need escaping
        // use the \uXXXX form, which is accepted on both sides.
        private static string RangeBoundary(char c)
        {
            if (c == '\\' || c == ']' || c == '[' || c == '^' || c == '-' || char.IsControl(c) || char.IsWhiteSpace(c))
                return @"\u" + ((int)c).ToString("X4", CultureInfo.InvariantCulture);
            return c.ToString();
        }

        /// <summary>
        /// Matches a hexadecimal digit. Renders <c>[0-9a-fA-F]</c> on its own and <c>0-9a-fA-F</c> inside a set.
        /// </summary>
        public Pattern HexDigit => Append(ClassToken.Bracketed("0-9a-fA-F"));

        /// <summary>
        /// Matches any one of the given characters: <c>[abc]</c>. Every character is taken literally,
        /// including '-', ']' and '^'.
        /// </summary>
        /// <param name="characters">The characters to accept. Must not be empty.</param>
        /// <exception cref="ArgumentNullException"><paramref name="characters"/> is null.</exception>
        /// <exception cref="ArgumentException"><paramref name="characters"/> is empty.</exception>
        public Pattern AnyOf(string characters)
        {
            if (characters == null)
                throw new ArgumentNullException(nameof(characters));
            if (characters.Length == 0)
                throw new ArgumentException("At least one character is required.", nameof(characters));
            return Set(With.Literal(characters));
        }

        /// <summary>
        /// Matches any character except the given ones: <c>[^abc]</c>. Every character is taken literally.
        /// </summary>
        /// <param name="characters">The characters to reject. Must not be empty.</param>
        /// <exception cref="ArgumentNullException"><paramref name="characters"/> is null.</exception>
        /// <exception cref="ArgumentException"><paramref name="characters"/> is empty.</exception>
        public Pattern NoneOf(string characters)
        {
            if (characters == null)
                throw new ArgumentNullException(nameof(characters));
            if (characters.Length == 0)
                throw new ArgumentException("At least one character is required.", nameof(characters));
            return NegatedSet(With.Literal(characters));
        }

        /// <summary>
        /// Matches a character in the given Unicode category, for example <c>\p{Lu}</c>.
        /// Works inside and outside a <see cref="Set"/>.
        /// </summary>
        public Pattern Unicode(UnicodeCategory category) => Append(ClassToken.Shorthand(@"\p{" + CategoryCode(category) + "}"));

        /// <summary>
        /// Matches a character that is not in the given Unicode category, for example <c>\P{Lu}</c>.
        /// </summary>
        public Pattern NotUnicode(UnicodeCategory category) => Append(ClassToken.Shorthand(@"\P{" + CategoryCode(category) + "}"));

        /// <summary>
        /// Matches a character in the given general Unicode group, for example <c>\p{L}</c> for any letter in any script.
        /// </summary>
        public Pattern Unicode(UnicodeGroup group) => Append(ClassToken.Shorthand(@"\p{" + GroupCode(group) + "}"));

        /// <summary>
        /// Matches a character that is not in the given general Unicode group, for example <c>\P{L}</c>.
        /// </summary>
        public Pattern NotUnicode(UnicodeGroup group) => Append(ClassToken.Shorthand(@"\P{" + GroupCode(group) + "}"));

        /// <summary>
        /// Matches a character in a named Unicode block, for example <c>\p{IsGreek}</c> or <c>\p{IsCyrillic}</c>.
        /// </summary>
        /// <param name="blockName">
        /// The .NET block name, such as <c>Greek</c> or <c>IsGreek</c>. The "Is" prefix may be written in any case.
        /// </param>
        /// <exception cref="ArgumentException">The name is empty, malformed, or not a block that .NET knows.</exception>
        public Pattern UnicodeBlock(string blockName)
        {
            if (string.IsNullOrEmpty(blockName))
                throw new ArgumentException("A block name is required.", nameof(blockName));
            foreach (char c in blockName)
            {
                if (!(char.IsLetterOrDigit(c) || c == '-' || c == '_'))
                    throw new ArgumentException("Invalid Unicode block name '" + blockName + "'.", nameof(blockName));
            }
            string name = blockName.StartsWith("Is", StringComparison.OrdinalIgnoreCase)
                ? "Is" + blockName.Substring(2)
                : "Is" + blockName;
            if (!IsKnownUnicodeBlock(name))
                throw new ArgumentException(
                    "Unknown Unicode block '" + blockName + "'. Use a .NET block name such as 'Greek', 'IsGreek' or 'BasicLatin'.",
                    nameof(blockName));
            return Append(ClassToken.Shorthand(@"\p{" + name + "}"));
        }

        // Fails here, when the pattern is built, rather than at its first match.
        private static bool IsKnownUnicodeBlock(string name)
        {
            try
            {
                _ = new Regex(@"\p{" + name + "}", RegexOptions.CultureInvariant);
                return true;
            }
            catch (ArgumentException)
            {
                return false;
            }
        }

        /// <summary>Matches a position that is not a word boundary: <c>\B</c>.</summary>
        public Pattern NonWordBoundary => Append(new RawToken(@"\B"));

        /// <summary>Matches the start of the input, regardless of multiline mode: <c>\A</c>.</summary>
        public Pattern StartOfInput => Append(new RawToken(@"\A"));

        /// <summary>Matches the very end of the input, regardless of multiline mode: <c>\z</c>.</summary>
        public Pattern EndOfInput => Append(new RawToken(@"\z"));

        internal static string CategoryCode(UnicodeCategory category)
        {
            switch (category)
            {
                case UnicodeCategory.UppercaseLetter: return "Lu";
                case UnicodeCategory.LowercaseLetter: return "Ll";
                case UnicodeCategory.TitlecaseLetter: return "Lt";
                case UnicodeCategory.ModifierLetter: return "Lm";
                case UnicodeCategory.OtherLetter: return "Lo";
                case UnicodeCategory.NonSpacingMark: return "Mn";
                case UnicodeCategory.SpacingCombiningMark: return "Mc";
                case UnicodeCategory.EnclosingMark: return "Me";
                case UnicodeCategory.DecimalDigitNumber: return "Nd";
                case UnicodeCategory.LetterNumber: return "Nl";
                case UnicodeCategory.OtherNumber: return "No";
                case UnicodeCategory.SpaceSeparator: return "Zs";
                case UnicodeCategory.LineSeparator: return "Zl";
                case UnicodeCategory.ParagraphSeparator: return "Zp";
                case UnicodeCategory.Control: return "Cc";
                case UnicodeCategory.Format: return "Cf";
                case UnicodeCategory.Surrogate: return "Cs";
                case UnicodeCategory.PrivateUse: return "Co";
                case UnicodeCategory.ConnectorPunctuation: return "Pc";
                case UnicodeCategory.DashPunctuation: return "Pd";
                case UnicodeCategory.OpenPunctuation: return "Ps";
                case UnicodeCategory.ClosePunctuation: return "Pe";
                case UnicodeCategory.InitialQuotePunctuation: return "Pi";
                case UnicodeCategory.FinalQuotePunctuation: return "Pf";
                case UnicodeCategory.OtherPunctuation: return "Po";
                case UnicodeCategory.MathSymbol: return "Sm";
                case UnicodeCategory.CurrencySymbol: return "Sc";
                case UnicodeCategory.ModifierSymbol: return "Sk";
                case UnicodeCategory.OtherSymbol: return "So";
                case UnicodeCategory.OtherNotAssigned: return "Cn";
                default: throw new ArgumentOutOfRangeException(nameof(category), category, "Unknown Unicode category.");
            }
        }

        internal static string GroupCode(UnicodeGroup group)
        {
            switch (group)
            {
                case UnicodeGroup.Letter: return "L";
                case UnicodeGroup.Mark: return "M";
                case UnicodeGroup.Number: return "N";
                case UnicodeGroup.Punctuation: return "P";
                case UnicodeGroup.Symbol: return "S";
                case UnicodeGroup.Separator: return "Z";
                case UnicodeGroup.Other: return "C";
                default: throw new ArgumentOutOfRangeException(nameof(group), group, "Unknown Unicode group.");
            }
        }
    }
}
