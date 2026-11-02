#nullable enable

using System;
using System.Text;

namespace Masterly.FluentRegex.Unicode;

/// <summary>
/// A construct that can match more than one UTF-16 code unit, such as a surrogate pair or a
/// grapheme cluster. It renders as a non-capturing group so quantifiers apply to the whole
/// construct, and it refuses to render inside a character class, where it would silently change meaning.
/// </summary>
internal sealed class SequenceToken : Token
{
    private readonly string _regex;
    private readonly string _memberName;

    public SequenceToken(string regex, string memberName)
    {
        _regex = regex;
        _memberName = memberName;
    }

    public override void Render(StringBuilder sb, RenderContext context)
    {
        if (context == RenderContext.CharacterClass)
        {
            throw new InvalidOperationException(
                $"{_memberName} can match more than one UTF-16 code unit, " +
                "so it cannot be used inside Set(...) or NegatedSet(...). " +
                "Combine it with the | operator or OneOf(...) instead.");
        }

        sb.Append("(?:").Append(_regex).Append(')');
    }
}
