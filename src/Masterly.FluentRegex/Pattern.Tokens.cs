using System.Collections.Generic;
using System.Text;

namespace Masterly.FluentRegex
{
    /// <summary>
    /// The rendering context of a token. Characters that are special inside a character class
    /// (such as '-') differ from those that are special outside of one.
    /// </summary>
    internal enum RenderContext
    {
        Normal,
        CharacterClass,

        // Outside a character class with RegexOptions.IgnorePatternWhitespace: the engine ignores unescaped
        // whitespace and reads '#' as the start of a comment, so literals must escape them too.
        FreeSpacing
    }

    internal static class RenderContexts
    {
        /// <summary>The context for a pattern nested outside a character class, keeping free-spacing mode.</summary>
        public static RenderContext Nested(RenderContext outer) =>
            outer == RenderContext.FreeSpacing ? RenderContext.FreeSpacing : RenderContext.Normal;
    }

    /// <summary>
    /// How a quantifier treats backtracking.
    /// </summary>
    internal enum QuantifierMode
    {
        Greedy,
        Lazy,
        Possessive
    }

    /// <summary>
    /// A single immutable building block of a <see cref="Pattern"/>.
    /// </summary>
    internal abstract class Token
    {
        public abstract void Render(StringBuilder sb, RenderContext context);

        public string Render(RenderContext context)
        {
            var sb = new StringBuilder();
            Render(sb, context);
            return sb.ToString();
        }
    }

    /// <summary>Raw, unescaped regex text supplied by the user.</summary>
    internal sealed class RawToken : Token
    {
        public RawToken(string text) => Text = text ?? string.Empty;

        public string Text { get; }

        public override void Render(StringBuilder sb, RenderContext context) => sb.Append(Text);
    }

    /// <summary>A single literal character, escaped according to the rendering context.</summary>
    internal sealed class LiteralToken : Token
    {
        // Characters with special meaning outside a character class.
        private const string NormalSpecialChars = @"^$.|{}[]()*+?\";

        // Characters with special meaning inside a character class. The legacy set is kept
        // so that existing output stays stable, and '-' is added so it never forms a range.
        private const string ClassSpecialChars = @"^$.|{}[]()*+?\-";

        public LiteralToken(char value) => Value = value;

        public char Value { get; }

        public override void Render(StringBuilder sb, RenderContext context) => AppendEscaped(sb, Value, context);

        public static void AppendEscaped(StringBuilder sb, char c, RenderContext context)
        {
            string specials = context == RenderContext.CharacterClass ? ClassSpecialChars : NormalSpecialChars;
            if (specials.IndexOf(c) >= 0)
            {
                sb.Append('\\').Append(c);
                return;
            }

            switch (c)
            {
                case '\t': sb.Append(@"\t"); return;
                case '\n': sb.Append(@"\n"); return;
                case '\r': sb.Append(@"\r"); return;
                case '\f': sb.Append(@"\f"); return;
                case '\v': sb.Append(@"\v"); return;
                case '\u001B': sb.Append(@"\e"); return;
            }

            if (context == RenderContext.FreeSpacing && (c == '#' || char.IsWhiteSpace(c)))
            {
                sb.Append('\\').Append(c);
                return;
            }

            if (char.IsControl(c))
            {
                sb.Append(@"\u").Append(((int)c).ToString("X4"));
                return;
            }

            sb.Append(c);
        }

        public static string Escape(char c, RenderContext context)
        {
            var sb = new StringBuilder(2);
            AppendEscaped(sb, c, context);
            return sb.ToString();
        }
    }

    /// <summary>
    /// A character class item. It renders bare inside a character class (for example <c>a-z</c>)
    /// and wrapped in brackets when it stands alone (for example <c>[a-z]</c>).
    /// </summary>
    internal sealed class ClassToken : Token
    {
        public ClassToken(string classBody, string standalone)
        {
            ClassBody = classBody;
            Standalone = standalone;
        }

        /// <summary>Creates a class item that renders identically in both contexts, such as <c>\d</c>.</summary>
        public static ClassToken Shorthand(string text) => new ClassToken(text, text);

        /// <summary>Creates a class item that needs brackets when standalone, such as <c>a-z</c>.</summary>
        public static ClassToken Bracketed(string body) => new ClassToken(body, "[" + body + "]");

        public string ClassBody { get; }

        public string Standalone { get; }

        public override void Render(StringBuilder sb, RenderContext context) =>
            sb.Append(context == RenderContext.CharacterClass ? ClassBody : Standalone);
    }

    /// <summary>
    /// A construct that wraps an inner pattern, such as a group, lookaround or character class.
    /// </summary>
    internal sealed class WrapToken : Token
    {
        public WrapToken(string prefix, Pattern inner, string suffix, RenderContext innerContext = RenderContext.Normal)
        {
            Prefix = prefix;
            Inner = inner;
            Suffix = suffix;
            InnerContext = innerContext;
        }

        public string Prefix { get; }

        public Pattern Inner { get; }

        public string Suffix { get; }

        public RenderContext InnerContext { get; }

        public override void Render(StringBuilder sb, RenderContext context)
        {
            sb.Append(Prefix);
            Inner.RenderTo(sb, InnerContext == RenderContext.Normal ? RenderContexts.Nested(context) : InnerContext);
            sb.Append(Suffix);
        }
    }

    /// <summary>An alternation of several patterns wrapped in a group.</summary>
    internal sealed class AlternationToken : Token
    {
        public AlternationToken(string open, IReadOnlyList<Pattern> branches, bool mergeable)
        {
            Open = open;
            Branches = branches;
            Mergeable = mergeable;
        }

        public string Open { get; }

        public IReadOnlyList<Pattern> Branches { get; }

        /// <summary>
        /// True when produced by the <c>|</c> operator, so that <c>a | b | c</c> flattens into one group.
        /// </summary>
        public bool Mergeable { get; }

        public override void Render(StringBuilder sb, RenderContext context)
        {
            sb.Append(Open);
            for (int i = 0; i < Branches.Count; i++)
            {
                if (i > 0) sb.Append('|');
                Branches[i].RenderTo(sb, RenderContexts.Nested(context));
            }
            sb.Append(')');
        }
    }

    /// <summary>A conditional construct: <c>(?(condition)yes|no)</c>.</summary>
    internal sealed class ConditionalToken : Token
    {
        public ConditionalToken(string condition, Pattern yes, Pattern no)
        {
            Condition = condition;
            Yes = yes;
            No = no;
        }

        public string Condition { get; }

        public Pattern Yes { get; }

        public Pattern No { get; }

        public override void Render(StringBuilder sb, RenderContext context)
        {
            sb.Append("(?(").Append(Condition).Append(')');
            Yes.RenderTo(sb, RenderContexts.Nested(context));
            sb.Append('|');
            No.RenderTo(sb, RenderContexts.Nested(context));
            sb.Append(')');
        }
    }

    /// <summary>An atom followed by a quantifier.</summary>
    internal sealed class QuantifiedToken : Token
    {
        public QuantifiedToken(Token atom, string quantifier, QuantifierMode mode)
        {
            Atom = atom;
            Quantifier = quantifier;
            Mode = mode;
        }

        public Token Atom { get; }

        public string Quantifier { get; }

        public QuantifierMode Mode { get; }

        public QuantifiedToken WithMode(QuantifierMode mode) => new QuantifiedToken(Atom, Quantifier, mode);

        public override void Render(StringBuilder sb, RenderContext context)
        {
            if (Mode == QuantifierMode.Possessive)
            {
                // .NET has no possessive quantifier syntax; an atomic group is the exact equivalent.
                sb.Append("(?>");
                Atom.Render(sb, context);
                sb.Append(Quantifier).Append(')');
                return;
            }

            Atom.Render(sb, context);
            sb.Append(Quantifier);
            if (Mode == QuantifierMode.Lazy) sb.Append('?');
        }
    }

    /// <summary>
    /// A node in a persistent (immutable, structurally shared) singly linked list of tokens.
    /// Appending creates a new node that points at the previous one, so patterns can be
    /// extended in O(1) while the original stays unchanged and safe to reuse.
    /// </summary>
    internal sealed class TokenNode
    {
        public TokenNode(Token token, TokenNode previous)
        {
            Token = token;
            Previous = previous;
            Count = (previous?.Count ?? 0) + 1;
        }

        public Token Token { get; }

        public TokenNode Previous { get; }

        public int Count { get; }

        public Token[] ToArray()
        {
            var tokens = new Token[Count];
            var node = this;
            for (int i = Count - 1; i >= 0; i--)
            {
                tokens[i] = node.Token;
                node = node.Previous;
            }
            return tokens;
        }
    }
}
