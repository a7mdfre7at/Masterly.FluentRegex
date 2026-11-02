#nullable enable
using System;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Masterly.FluentRegex;

/// <summary>
/// How a <see cref="Redactor"/> rewrites a sensitive value.
/// </summary>
/// <remarks>
/// Styles are immutable and thread-safe. Masking styles work per Unicode code point, so a
/// supplementary character is replaced by a single <c>*</c>.
/// </remarks>
public abstract class RedactionStyle
{
    private const char MaskChar = '*';

    private protected RedactionStyle(string name)
    {
        Name = name;
    }

    /// <summary>
    /// Gets the style that replaces every letter and digit with <c>*</c> and keeps separators and
    /// punctuation, so <c>555-123-4567</c> becomes <c>***-***-****</c>.
    /// </summary>
    public static RedactionStyle Mask { get; } = new MaskStyle("Mask", 0, 0);

    /// <summary>
    /// Gets the style that keeps the first character of an e-mail address and its domain:
    /// <c>alice@example.com</c> becomes <c>a***@example.com</c>. The length of the local part is hidden.
    /// Values without an <c>@</c> are masked like <see cref="Mask"/>.
    /// </summary>
    public static RedactionStyle Email { get; } = new EmailStyle();

    /// <summary>Gets the name of the style as reported in a <see cref="RedactionReport"/>.</summary>
    public string Name { get; }

    /// <summary>Gets the label text for a <see cref="Label(string)"/> style, otherwise null.</summary>
    public virtual string? LabelText => null;

    /// <summary>
    /// Creates a style that masks letters and digits except the last <paramref name="count"/> of them:
    /// <c>KeepLast(4)</c> turns <c>4111-1111-1111-1111</c> into <c>****-****-****-1111</c>.
    /// A value with no more than <paramref name="count"/> letters and digits is masked completely,
    /// so a redaction never reveals a whole value.
    /// </summary>
    /// <param name="count">How many trailing letters and digits stay visible.</param>
    /// <returns>The style.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="count"/> is negative.</exception>
    public static RedactionStyle KeepLast(int count) =>
        new MaskStyle($"KeepLast({FormatCount(count)})", 0, RequireCount(count));

    /// <summary>
    /// Creates a style that masks letters and digits except the first <paramref name="count"/> of them.
    /// A value with no more than <paramref name="count"/> letters and digits is masked completely.
    /// </summary>
    /// <param name="count">How many leading letters and digits stay visible.</param>
    /// <returns>The style.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="count"/> is negative.</exception>
    public static RedactionStyle KeepFirst(int count) =>
        new MaskStyle($"KeepFirst({FormatCount(count)})", RequireCount(count), 0);

    /// <summary>Creates a style that replaces the whole value with <paramref name="label"/>, such as <c>[EMAIL]</c>.</summary>
    /// <param name="label">The replacement text.</param>
    /// <returns>The style.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="label"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="label"/> is empty.</exception>
    public static RedactionStyle Label(string label)
    {
        if (label == null)
        {
            throw new ArgumentNullException(nameof(label));
        }

        if (label.Length == 0)
        {
            throw new ArgumentException("The label must not be empty.", nameof(label));
        }

        return new LabelStyle(label);
    }

    /// <summary>Creates a style that computes the replacement from the match, for example to keep a group.</summary>
    /// <param name="replacement">Returns the replacement text for a match. It must not return null.</param>
    /// <returns>The style.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="replacement"/> is null.</exception>
    public static RedactionStyle Custom(Func<Match, string> replacement)
    {
        if (replacement == null)
        {
            throw new ArgumentNullException(nameof(replacement));
        }

        return new CustomStyle(replacement);
    }

    /// <summary>Returns <see cref="Name"/>.</summary>
    public override string ToString() => Name;

    /// <summary>Computes the redacted replacement for a match.</summary>
    internal abstract string Apply(Match match);

    private static int RequireCount(int count)
    {
        if (count < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(count), "The count must not be negative.");
        }

        return count;
    }

    private static string FormatCount(int count) => count.ToString(CultureInfo.InvariantCulture);

    private static string MaskAlphanumerics(string value, int keepFirst, int keepLast)
    {
        int total = CountAlphanumerics(value);
        bool revealsAll = keepFirst + keepLast >= total;
        StringBuilder sb = new StringBuilder(value.Length);
        int seen = 0;
        for (int i = 0; i < value.Length; i += CodePointLength(value, i))
        {
            int length = CodePointLength(value, i);
            if (!char.IsLetterOrDigit(value, i))
            {
                sb.Append(value, i, length);
                continue;
            }

            bool visible = !revealsAll && (seen < keepFirst || seen >= total - keepLast);
            if (visible)
            {
                sb.Append(value, i, length);
            }
            else
            {
                sb.Append(MaskChar);
            }

            seen++;
        }

        return sb.ToString();
    }

    private static int CountAlphanumerics(string value)
    {
        int count = 0;
        for (int i = 0; i < value.Length; i += CodePointLength(value, i))
        {
            if (char.IsLetterOrDigit(value, i))
            {
                count++;
            }
        }

        return count;
    }

    private static int CodePointLength(string value, int index) => char.IsSurrogatePair(value, index) ? 2 : 1;

    private sealed class MaskStyle : RedactionStyle
    {
        private readonly int _keepFirst;
        private readonly int _keepLast;

        public MaskStyle(string name, int keepFirst, int keepLast) : base(name)
        {
            _keepFirst = keepFirst;
            _keepLast = keepLast;
        }

        internal override string Apply(Match match) => MaskAlphanumerics(match.Value, _keepFirst, _keepLast);
    }

    private sealed class EmailStyle : RedactionStyle
    {
        private const string HiddenLocalPart = "***";

        public EmailStyle() : base("Email")
        {
        }

        internal override string Apply(Match match)
        {
            string value = match.Value;
            int at = value.LastIndexOf('@');
            if (at <= 0)
            {
                return MaskAlphanumerics(value, 0, 0);
            }

            string first = value.Substring(0, CodePointLength(value, 0));
            return $"{first}{HiddenLocalPart}{value.Substring(at)}";
        }
    }

    private sealed class LabelStyle : RedactionStyle
    {
        private readonly string _label;

        public LabelStyle(string label) : base("Label")
        {
            _label = label;
        }

        public override string? LabelText => _label;

        internal override string Apply(Match match) => _label;
    }

    private sealed class CustomStyle : RedactionStyle
    {
        private readonly Func<Match, string> _replacement;

        public CustomStyle(Func<Match, string> replacement) : base("Custom")
        {
            _replacement = replacement;
        }

        internal override string Apply(Match match) =>
            _replacement(match) ?? throw new InvalidOperationException("The custom redaction style returned null.");
    }
}
