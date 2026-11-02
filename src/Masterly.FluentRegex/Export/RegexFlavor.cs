#nullable enable

namespace Masterly.FluentRegex;

/// <summary>The regular expression dialects a <see cref="Pattern"/> can be exported to.</summary>
public enum RegexFlavor
{
    /// <summary>
    /// ECMAScript 2018+ as used by JavaScript and TypeScript, with the <c>u</c> flag
    /// (needed for <c>\p{...}</c>, named groups and lookbehind).
    /// </summary>
    JavaScript,

    /// <summary>
    /// The <c>pattern</c> keyword of JSON Schema (and OpenAPI): ECMA-262 syntax without flags, so
    /// case-insensitivity and dot-all are expanded into character classes. The output is written to
    /// be valid with the <c>u</c> flag, which validators such as Ajv use by default.
    /// </summary>
    JsonSchema
}
