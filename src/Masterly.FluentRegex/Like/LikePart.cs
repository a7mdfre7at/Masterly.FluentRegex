#nullable enable
using Masterly.FluentRegex.Syntax;

namespace Masterly.FluentRegex.Like;

/// <summary>The building blocks of a LIKE pattern.</summary>
internal enum LikePartKind
{
    /// <summary>Literal text.</summary>
    Text,

    /// <summary>Exactly one character: <c>_</c>.</summary>
    AnyChar,

    /// <summary>Any number of characters: <c>%</c>.</summary>
    AnyString,

    /// <summary>One character from a set: <c>[...]</c> or <c>[^...]</c> (SQL Server).</summary>
    Set
}

/// <summary>One piece of a LIKE pattern.</summary>
internal sealed class LikePart
{
    private LikePart(LikePartKind kind, string text, CharSet? set)
    {
        Kind = kind;
        Text = text;
        Set = set;
    }

    public LikePartKind Kind { get; }

    /// <summary>The literal text of a <see cref="LikePartKind.Text"/> part.</summary>
    public string Text { get; }

    /// <summary>The members of a <see cref="LikePartKind.Set"/> part.</summary>
    public CharSet? Set { get; }

    public static LikePart AnyChar { get; } = new LikePart(LikePartKind.AnyChar, string.Empty, null);

    public static LikePart AnyString { get; } = new LikePart(LikePartKind.AnyString, string.Empty, null);

    public static LikePart OfText(string text) => new LikePart(LikePartKind.Text, text, null);

    public static LikePart OfSet(CharSet set) => new LikePart(LikePartKind.Set, string.Empty, set);

    /// <summary>Gets a value indicating whether the part matches exactly one character.</summary>
    public bool IsSingleCharacter => Kind == LikePartKind.AnyChar || Kind == LikePartKind.Set || (Kind == LikePartKind.Text && Text.Length == 1);

    /// <summary>The characters a single-character part matches.</summary>
    public CharSet SingleCharacterSet()
    {
        switch (Kind)
        {
            case LikePartKind.AnyChar:
                return CharSet.All;
            case LikePartKind.Set:
                return Set ?? CharSet.Empty;
            default:
                return CharSet.Single(Text[0]);
        }
    }
}
