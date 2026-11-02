#nullable enable
using System;
using Masterly.FluentRegex.Export;

namespace Masterly.FluentRegex;

/// <summary>
/// Export: translate a pattern to JavaScript, JSON Schema or a database regex dialect so the same
/// rule runs in the browser, in Node.js, in API schemas and in SQL.
/// </summary>
public partial class Pattern
{
    /// <summary>
    /// Translates this pattern, including its <see cref="Options"/>, into another regex dialect.
    /// </summary>
    /// <param name="flavor">The target dialect.</param>
    /// <param name="options">
    /// Exactness settings for <see cref="RegexFlavor.JavaScript"/> and <see cref="RegexFlavor.JsonSchema"/>,
    /// or null for the defaults (exact translation). Ignored by the SQL flavors, which always preserve
    /// the .NET meaning and report any remaining difference as a warning.
    /// </param>
    /// <returns>The translated pattern, its flags, its group numbers and every semantic difference as a warning.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="flavor"/> is not defined.</exception>
    /// <exception cref="NotSupportedException">
    /// The pattern uses constructs the dialect cannot express, such as atomic groups, conditionals,
    /// balancing groups, <c>\G</c> or <see cref="System.Text.RegularExpressions.RegexOptions.RightToLeft"/>
    /// (and, per engine, lookarounds, backreferences, Unicode word boundaries or large repeat bounds).
    /// The message lists all of them with their positions.
    /// </exception>
    /// <example>
    /// FlavorConversion js = Pattern.With.StartOfLine.Digit.Repeat.Times(4).ToFlavor(RegexFlavor.JavaScript);
    /// js.ToLiteral(); // "/^\p{Nd}{4}/u"
    /// FlavorConversion pg = Pattern.FromRegex(@"^[0-9]+$").ToFlavor(RegexFlavor.PostgreSql);
    /// pg.Pattern;     // @"\A[0-9]+(?=\n?\Z)"
    /// </example>
    public FlavorConversion ToFlavor(RegexFlavor flavor, FlavorOptions? options = null)
    {
        switch (flavor)
        {
            case RegexFlavor.JavaScript:
            case RegexFlavor.JsonSchema:
                return EcmaScriptTranslator.Translate(ParseTree(), _options, flavor, options ?? new FlavorOptions());
            case RegexFlavor.PostgreSql:
            case RegexFlavor.MySql:
            case RegexFlavor.MariaDb:
            case RegexFlavor.SqlServer:
                return SqlRegexTranslator.Translate(ParseTree(), _options, flavor);
            case RegexFlavor.Sqlite:
                return SqliteTranslator.Translate(ParseTree(), _options);
            default:
                throw new ArgumentOutOfRangeException(nameof(flavor), flavor, "Unknown regex flavor.");
        }
    }

    /// <summary>
    /// Translates this pattern into a JavaScript regex literal with exact .NET semantics,
    /// such as <c>/^[\p{L}\p{Mn}\p{Nd}\p{Pc}]+(?=\n?$)/u</c>.
    /// </summary>
    /// <returns>The JavaScript regex literal.</returns>
    /// <exception cref="NotSupportedException">The pattern uses constructs JavaScript cannot express.</exception>
    public string ToJavaScript() => ToFlavor(RegexFlavor.JavaScript).ToLiteral();

    /// <summary>
    /// Writes a boolean SQL condition that tests a column against this pattern with the database's
    /// regex operator, for raw SQL or Dapper: <c>"Email" ~ '...'</c> (PostgreSQL),
    /// <c>REGEXP_LIKE(Email, '...')</c> (MySQL, SQL Server) or <c>Email REGEXP '...'</c> (MariaDB, SQLite).
    /// </summary>
    /// <param name="columnSql">The column expression, inserted verbatim; quote it yourself.</param>
    /// <param name="flavor">The database's regex flavor.</param>
    /// <returns>The condition text, with the pattern as a quoted literal from <see cref="FlavorConversion.ToLiteral"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="columnSql"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="flavor"/> is not a SQL flavor.</exception>
    /// <exception cref="NotSupportedException">The pattern uses constructs the engine cannot express.</exception>
    /// <example>
    /// Pattern.FromRegex(@"^[0-9]+$").ToSqlCondition("\"Email\"", RegexFlavor.PostgreSql);
    /// // "Email" ~ '\A[0-9]+(?=\n?\Z)'
    /// </example>
    public string ToSqlCondition(string columnSql, RegexFlavor flavor)
    {
        if (columnSql == null)
        {
            throw new ArgumentNullException(nameof(columnSql));
        }

        if (!SqlDialect.IsSql(flavor))
        {
            throw new ArgumentException($"{flavor} is not a SQL flavor; use ToFlavor for it.", nameof(flavor));
        }

        return SqlDialect.Condition(columnSql, ToFlavor(flavor).ToLiteral(), flavor);
    }
}
