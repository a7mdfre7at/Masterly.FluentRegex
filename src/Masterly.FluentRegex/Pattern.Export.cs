#nullable enable
using System;
using Masterly.FluentRegex.Export;

namespace Masterly.FluentRegex;

/// <summary>
/// Export: translate a pattern to JavaScript or JSON Schema so the same rule runs in the browser,
/// in Node.js and in API schemas.
/// </summary>
public partial class Pattern
{
    /// <summary>
    /// Translates this pattern, including its <see cref="Options"/>, into another regex dialect.
    /// </summary>
    /// <param name="flavor">The target dialect.</param>
    /// <param name="options">Exactness settings, or null for the defaults (exact translation).</param>
    /// <returns>The translated pattern, its flags and every semantic difference as a warning.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="flavor"/> is not defined.</exception>
    /// <exception cref="NotSupportedException">
    /// The pattern uses constructs the dialect cannot express, such as atomic groups, conditionals,
    /// balancing groups, <c>\G</c> or <see cref="System.Text.RegularExpressions.RegexOptions.RightToLeft"/>.
    /// The message lists all of them with their positions.
    /// </exception>
    /// <example>
    /// FlavorConversion js = Pattern.With.StartOfLine.Digit.Repeat.Times(4).ToFlavor(RegexFlavor.JavaScript);
    /// js.ToLiteral(); // "/^\p{Nd}{4}/u"
    /// </example>
    public FlavorConversion ToFlavor(RegexFlavor flavor, FlavorOptions? options = null)
    {
        if (flavor != RegexFlavor.JavaScript && flavor != RegexFlavor.JsonSchema)
        {
            throw new ArgumentOutOfRangeException(nameof(flavor), flavor, "Unknown regex flavor.");
        }

        return EcmaScriptTranslator.Translate(ParseTree(), _options, flavor, options ?? new FlavorOptions());
    }

    /// <summary>
    /// Translates this pattern into a JavaScript regex literal with exact .NET semantics,
    /// such as <c>/^[\p{L}\p{Mn}\p{Nd}\p{Pc}]+(?=\n?$)/u</c>.
    /// </summary>
    /// <returns>The JavaScript regex literal.</returns>
    /// <exception cref="NotSupportedException">The pattern uses constructs JavaScript cannot express.</exception>
    public string ToJavaScript() => ToFlavor(RegexFlavor.JavaScript).ToLiteral();
}
