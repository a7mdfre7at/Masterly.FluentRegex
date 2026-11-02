#nullable enable
using System;
using System.Collections.Generic;
using System.Text;

namespace Masterly.FluentRegex;

/// <summary>
/// A pattern compiled to a SQL LIKE pattern by <see cref="Pattern.ToLike"/>, with a proof of what
/// the LIKE pattern accepts relative to the regex.
/// </summary>
public sealed class LikeConversion
{
    private const char Quote = '\'';
    private const string DoubledQuote = "''";
    private const char Backslash = '\\';
    private const string DoubledBackslash = @"\\";

    internal LikeConversion(
        LikeDialect dialect,
        MatchScope scope,
        LikeCollation collation,
        string expression,
        char? escapeCharacter,
        bool isExact,
        IReadOnlyList<string> warnings,
        string regexText)
    {
        Dialect = dialect;
        Scope = scope;
        Collation = collation;
        Expression = expression;
        EscapeCharacter = escapeCharacter;
        IsExact = isExact;
        Warnings = warnings;
        RegexText = regexText;
    }

    /// <summary>Gets the LIKE syntax the pattern was written for.</summary>
    public LikeDialect Dialect { get; }

    /// <summary>Gets the set of strings the proof is about.</summary>
    public MatchScope Scope { get; }

    /// <summary>Gets the collation the proof assumed.</summary>
    public LikeCollation Collation { get; }

    /// <summary>Gets the LIKE pattern text, unquoted, as the database sees it.</summary>
    public string Expression { get; }

    /// <summary>Gets the escape character declared with <c>ESCAPE</c>, or null when no clause is needed.</summary>
    public char? EscapeCharacter { get; }

    /// <summary>
    /// Gets a value indicating whether LIKE accepts exactly the strings the pattern accepts in
    /// <see cref="Scope"/> under <see cref="Collation"/>, as proven by language algebra. Always false
    /// for a pattern outside the regular subset.
    /// </summary>
    public bool IsExact { get; }

    /// <summary>
    /// Gets a value indicating whether LIKE accepts every string the pattern accepts. Always true:
    /// a LIKE pattern is only returned when it is a superset, so it can safely pre-filter rows before
    /// the regex decides.
    /// </summary>
    public bool IsSuperset => true;

    /// <summary>
    /// Gets the reasons the LIKE pattern is not exact, plus remarks that hold either way (such as a set
    /// that contains non-ASCII characters). Usually empty when <see cref="IsExact"/> is true.
    /// </summary>
    public IReadOnlyList<string> Warnings { get; }

    /// <summary>The .NET regex with the same full-match language as <see cref="Expression"/>, used for the proof.</summary>
    internal string RegexText { get; }

    /// <summary>
    /// Writes the condition for raw SQL, for example <c>[Email] LIKE 'INV-[0-9]%' ESCAPE '\'</c>.
    /// The literal has its quotes doubled, and for <see cref="LikeDialect.MySql"/> its backslashes too.
    /// </summary>
    /// <param name="columnSql">The column expression, inserted verbatim; quote it yourself.</param>
    /// <returns>The condition text.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="columnSql"/> is null.</exception>
    public string ToSql(string columnSql)
    {
        if (columnSql == null)
        {
            throw new ArgumentNullException(nameof(columnSql));
        }

        string condition = $"{columnSql} LIKE {Literal(Expression)}";
        return EscapeCharacter == null ? condition : $"{condition} ESCAPE {Literal(EscapeCharacter.Value.ToString())}";
    }

    /// <summary>Describes the LIKE pattern and whether it is exact, for example <c>INV-[0-9]% (superset)</c>.</summary>
    /// <returns>A one-line description.</returns>
    public override string ToString() => $"{Expression} ({(IsExact ? "exact" : "superset")})";

    private string Literal(string text)
    {
        bool escapeBackslashes = Dialect == LikeDialect.MySql;
        StringBuilder sb = new StringBuilder(text.Length + 2);
        sb.Append(Quote);
        foreach (char c in text)
        {
            if (c == Quote)
            {
                sb.Append(DoubledQuote);
            }
            else if (c == Backslash && escapeBackslashes)
            {
                sb.Append(DoubledBackslash);
            }
            else
            {
                sb.Append(c);
            }
        }

        return sb.Append(Quote).ToString();
    }
}
