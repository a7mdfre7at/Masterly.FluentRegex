#nullable enable

using System.Collections.Generic;
using System.Text;

namespace Masterly.FluentRegex.Learning;

/// <summary>How finely an example is cut into runs.</summary>
internal enum TokenGranularity
{
    /// <summary>Separate runs of digits, of letters and of whitespace, and one token per other character.</summary>
    Fine,

    /// <summary>Runs of word characters, runs of whitespace, and one token per other character.</summary>
    Coarse
}

/// <summary>The broad category of a token. Tokens can only be aligned with tokens of the same category.</summary>
internal enum TokenCategory
{
    Word,
    Space,
    Symbol
}

/// <summary>A run of characters cut from an example.</summary>
internal sealed class ExampleToken
{
    public ExampleToken(TokenCategory category, string text)
    {
        Category = category;
        Text = text;
    }

    public TokenCategory Category { get; }

    public string Text { get; }
}

/// <summary>
/// Cuts example strings into runs of similar characters, so that examples can be aligned run by run.
/// </summary>
internal static class ExampleTokenizer
{
    private enum RunKind
    {
        Digit,
        Letter,
        Word,
        Space,
        Symbol
    }

    /// <summary>Cuts <paramref name="example"/> into tokens.</summary>
    public static IReadOnlyList<ExampleToken> Tokenize(string example, TokenGranularity granularity)
    {
        List<ExampleToken> tokens = new List<ExampleToken>();
        StringBuilder run = new StringBuilder();
        RunKind? current = null;

        foreach (char c in example)
        {
            RunKind kind = Classify(c, granularity);
            if (current != kind || kind == RunKind.Symbol)
            {
                Flush(tokens, run, current);
                current = kind;
            }

            run.Append(c);
        }

        Flush(tokens, run, current);
        return tokens;
    }

    /// <summary>
    /// Returns a key that is equal for examples whose tokens can be aligned position by position.
    /// </summary>
    public static string SignatureOf(IReadOnlyList<ExampleToken> tokens)
    {
        StringBuilder signature = new StringBuilder(tokens.Count);
        foreach (ExampleToken token in tokens)
        {
            signature.Append(SignatureChar(token.Category));
        }

        return signature.ToString();
    }

    private static char SignatureChar(TokenCategory category)
    {
        switch (category)
        {
            case TokenCategory.Word:
                return 'w';
            case TokenCategory.Space:
                return 's';
            default:
                return 'y';
        }
    }

    private static RunKind Classify(char c, TokenGranularity granularity)
    {
        if (LearnedClasses.IsSpace(c))
        {
            return RunKind.Space;
        }

        if (granularity == TokenGranularity.Coarse)
        {
            return LearnedClasses.IsWordChar(c) ? RunKind.Word : RunKind.Symbol;
        }

        if (LearnedClasses.IsDigit(c))
        {
            return RunKind.Digit;
        }

        return LearnedClasses.IsLetter(c) ? RunKind.Letter : RunKind.Symbol;
    }

    private static void Flush(List<ExampleToken> tokens, StringBuilder run, RunKind? kind)
    {
        if (run.Length == 0 || kind == null)
        {
            return;
        }

        tokens.Add(new ExampleToken(CategoryOf(kind.Value), run.ToString()));
        run.Clear();
    }

    private static TokenCategory CategoryOf(RunKind kind)
    {
        switch (kind)
        {
            case RunKind.Space:
                return TokenCategory.Space;
            case RunKind.Symbol:
                return TokenCategory.Symbol;
            default:
                return TokenCategory.Word;
        }
    }
}
